using UnityEditor;
using UnityEngine;
using DesktopCompanion.Views;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// ItemGradeStyle 전용 인스펙터. 밝기 상한을 0~255 채널값으로 편집하게 하고,
    /// 티어별로 실제 계산된 테두리 색을 스와치와 RGB 값으로 미리보기 한다.
    /// </summary>
    [CustomEditor(typeof(ItemGradeStyle))]
    public class ItemGradeStyleEditor : Editor
    {
        private const string ScriptFieldName = "m_Script";
        private const string ToneFieldName = "m_baseValue";
        private const float SwatchWidth = 44f;
        private const float RowHeight = 18f;

        // 미리보기 범위는 에셋 데이터가 아니므로 인스펙터에만 보관한다.
        private static int s_previewTierCount = 8;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.name == ScriptFieldName)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(property);
                    }
                    continue;
                }

                if (property.name == ToneFieldName)
                {
                    DrawToneSlider(property);
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            DrawTierPreview(target as ItemGradeStyle);
        }

        /// <summary>밝기 상한을 0~255 채널값으로 편집한다. 내부 저장값은 0~1 그대로 유지된다.</summary>
        private static void DrawToneSlider(SerializedProperty property)
        {
            var label = new GUIContent(
                "밝기 상한 (0~255)",
                "티어 색에서 가장 밝은 채널이 가질 값. 255 = 완전한 원색(255,255,0), 188 = 어두운 톤(188,188,0)");

            EditorGUI.BeginChangeCheck();
            int channel = EditorGUILayout.IntSlider(label, ToChannel(property.floatValue), 0, 255);
            if (EditorGUI.EndChangeCheck())
            {
                property.floatValue = channel / 255f;
            }
        }

        /// <summary>티어 1부터 지정 개수만큼 계산된 테두리 색을 스와치와 RGB 값으로 나열한다.</summary>
        private static void DrawTierPreview(ItemGradeStyle style)
        {
            if (style == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("티어 색 미리보기", EditorStyles.boldLabel);
            s_previewTierCount = EditorGUILayout.IntSlider("표시할 티어 수", s_previewTierCount, 1, 20);

            for (int tier = 1; tier <= s_previewTierCount; tier++)
            {
                Color color = style.TierColor(tier);

                Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
                EditorGUI.DrawRect(new Rect(row.x, row.y + 1f, SwatchWidth, RowHeight - 2f), color);

                var textRect = new Rect(row.x + SwatchWidth + 6f, row.y, row.width - SwatchWidth - 6f, row.height);
                EditorGUI.LabelField(textRect,
                    $"T{tier}   ({ToChannel(color.r)}, {ToChannel(color.g)}, {ToChannel(color.b)})");
            }
        }

        private static int ToChannel(float normalized) => Mathf.RoundToInt(Mathf.Clamp01(normalized) * 255f);
    }
}
