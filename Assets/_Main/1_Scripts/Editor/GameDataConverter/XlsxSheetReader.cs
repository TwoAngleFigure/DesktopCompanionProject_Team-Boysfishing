using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// xlsx(=ZIP+XML)를 외부 라이브러리 없이 직접 파싱하는 에디터 전용 리더(§15.8.2).
    /// 수식 셀은 저장 시점의 캐시값(&lt;v&gt;)을 읽는다 — 본 파이프라인에서 수식은
    /// #(표시 전용) 열에만 존재하고 그 열은 변환기가 스킵하므로 문제되지 않는다.
    /// </summary>
    public static class XlsxSheetReader
    {
        private static readonly List<(string sheet, string cell, int column)> s_staleFormulaCells = new();

        /// <summary>
        /// 직전 <see cref="Read"/>에서 발견한 '수식은 있는데 캐시값이 없는' 셀 목록.
        ///
        /// 셀에 캐시값이 없으면 빈 셀과 구분되지 않아 그 필드가 조용히 비어버린다. Excel 없이 파일을
        /// 다시 쓰는 도구(openpyxl 등)는 수식만 남기고 캐시를 지우므로, 그런 파일을 변환하면
        /// 수식으로 채우던 열이 통째로 사라진다. 호출자가 변환 대상 시트·열에 한해 검사해 차단한다.
        /// </summary>
        public static IReadOnlyList<(string sheet, string cell, int column)> StaleFormulaCells => s_staleFormulaCells;

        /// <summary>시트명 → (행번호 → (열인덱스(1-base) → 값 long/double/bool/string)).</summary>
        public static Dictionary<string, SortedDictionary<int, Dictionary<int, object>>> Read(string xlsxPath)
        {
            using FileStream stream = File.OpenRead(xlsxPath);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

            List<string> sharedStrings = ReadSharedStrings(zip);
            Dictionary<string, string> sheetPaths = ReadSheetPaths(zip);

            s_staleFormulaCells.Clear();

            var result = new Dictionary<string, SortedDictionary<int, Dictionary<int, object>>>();
            foreach ((string sheetName, string entryPath) in sheetPaths)
            {
                ZipArchiveEntry entry = zip.GetEntry(entryPath);
                if (entry == null)
                {
                    continue;
                }
                result[sheetName] = ReadSheet(entry, sharedStrings, sheetName);
            }
            return result;
        }

        private static XDocument LoadXml(ZipArchive zip, string entryPath)
        {
            ZipArchiveEntry entry = zip.GetEntry(entryPath);
            if (entry == null)
            {
                throw new FileNotFoundException($"xlsx 내부 항목 없음: {entryPath}");
            }
            using Stream stream = entry.Open();
            return XDocument.Load(stream);
        }

        // xl/workbook.xml(시트명·rId) + xl/_rels/workbook.xml.rels(rId→파일)로 시트 경로 매핑
        private static Dictionary<string, string> ReadSheetPaths(ZipArchive zip)
        {
            XDocument workbook = LoadXml(zip, "xl/workbook.xml");
            XDocument rels = LoadXml(zip, "xl/_rels/workbook.xml.rels");

            var idToTarget = new Dictionary<string, string>();
            foreach (XElement rel in rels.Descendants().Where(e => e.Name.LocalName == "Relationship"))
            {
                string target = rel.Attribute("Target")?.Value;
                if (target == null)
                {
                    continue;
                }
                // Target은 "worksheets/sheet1.xml"(상대) 또는 "/xl/..."(절대) 형태
                target = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
                idToTarget[rel.Attribute("Id")?.Value ?? ""] = target;
            }

            var paths = new Dictionary<string, string>();
            foreach (XElement sheet in workbook.Descendants().Where(e => e.Name.LocalName == "sheet"))
            {
                string name = sheet.Attribute("name")?.Value;
                string rId = sheet.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;
                if (name != null && rId != null && idToTarget.TryGetValue(rId, out string path))
                {
                    paths[name] = path;
                }
            }
            return paths;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var strings = new List<string>();
            ZipArchiveEntry entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return strings;   // 문자열 없는 통합문서
            }
            using Stream s = entry.Open();
            XDocument doc = XDocument.Load(s);
            foreach (XElement si in doc.Root.Elements().Where(e => e.Name.LocalName == "si"))
            {
                // 서식 분할(rich text run) 대비: si 하위의 모든 <t> 텍스트를 이어붙임
                strings.Add(string.Concat(si.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value)));
            }
            return strings;
        }

        private static SortedDictionary<int, Dictionary<int, object>> ReadSheet(
            ZipArchiveEntry entry, List<string> sst, string sheetName)
        {
            var rows = new SortedDictionary<int, Dictionary<int, object>>();
            using Stream s = entry.Open();
            XDocument doc = XDocument.Load(s);

            foreach (XElement row in doc.Descendants().Where(e => e.Name.LocalName == "row"))
            {
                if (!int.TryParse(row.Attribute("r")?.Value, out int rowNum))
                {
                    continue;
                }
                var cells = new Dictionary<int, object>();
                foreach (XElement c in row.Elements().Where(e => e.Name.LocalName == "c"))
                {
                    string cellRef = c.Attribute("r")?.Value;
                    if (cellRef == null)
                    {
                        continue;
                    }
                    object value = ReadCellValue(c, sst);
                    if (value != null)
                    {
                        cells[ColumnIndex(cellRef)] = value;
                    }
                    else if (c.Elements().Any(e => e.Name.LocalName == "f"))
                    {
                        // 수식은 있는데 값이 안 읽혔다 = 캐시 소실. 빈 셀과 구분되지 않으므로 여기서 기록해 둔다.
                        // 열 번호를 함께 남긴다 — 호출자가 헤더를 보고 표시 전용(#) 열을 걸러내야 한다.
                        s_staleFormulaCells.Add((sheetName, cellRef, ColumnIndex(cellRef)));
                    }
                }
                if (cells.Count > 0)
                {
                    rows[rowNum] = cells;
                }
            }
            return rows;
        }

        private static object ReadCellValue(XElement c, List<string> sst)
        {
            string type = c.Attribute("t")?.Value;

            if (type == "inlineStr")
            {
                return string.Concat(c.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value));
            }

            string v = c.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value;
            if (string.IsNullOrEmpty(v))
            {
                return null;
            }

            switch (type)
            {
                case "s":     // 공유 문자열 인덱스
                    return int.TryParse(v, out int index) && index >= 0 && index < sst.Count ? sst[index] : null;
                case "b":     // 불리언
                    return v == "1";
                case "str":   // 수식 결과 문자열(캐시값)
                    return v;
                case "e":     // 수식 에러
                    return null;
                default:      // 숫자
                    if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    {
                        return l;
                    }
                    if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    {
                        // 구글 시트 등 외부 툴이 정수를 "200001.0"으로 저장하는 경우 대응:
                        // 소수부가 없는 실수는 정수(long)로 복원한다(직접 작성 시트의 정수 토큰과 동일 결과).
                        // 이렇게 하지 않으면 이후 로드에서 int 필드에 float 리터럴이 들어가 JsonReaderException이 난다.
                        if (d == Math.Floor(d) && !double.IsInfinity(d))
                        {
                            return (long)d;
                        }
                        return d;
                    }
                    return v;
            }
        }

        // "AB12" → 열 인덱스(1-base)
        private static int ColumnIndex(string cellRef)
        {
            int col = 0;
            foreach (char ch in cellRef)
            {
                if (ch < 'A' || ch > 'Z')
                {
                    break;
                }
                col = col * 26 + (ch - 'A' + 1);
            }
            return col;
        }
    }
}
