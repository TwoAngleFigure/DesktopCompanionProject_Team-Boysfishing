using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>
    /// 무작위 보상 후보 묶음. 결과 ID가 아이템이 아니라 이 테이블을 가리킬 때 쓴다.
    /// 조합·낚시 드랍·보상 등 굴리는 쪽이 어디든 테이블 ID로만 참조한다.
    ///
    /// ID에 의미를 인코딩하지 않는다 — 티어·등급 같은 개념은 후보를 어떻게 나열하느냐로만 표현된다.
    /// </summary>
    public class RandomTableData : GameData
    {
        [Tooltip("후보 목록. '타입:ID:가중치'를 ;로 이어 붙인다. 예: Equipment:200001:1;Equipment:200002:2")]
        [SerializeField] private string m_entries;

        public string Entries => m_entries;
    }
}
