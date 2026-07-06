using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>
    /// 모든 게임 데이터(ScriptableObject)의 공통 베이스.
    /// 런타임에 변하지 않는 정적 정의값만 담는다. ID/Name은 모든 Data 공통.
    /// </summary>
    public abstract class GameData : ScriptableObject
    {
        [SerializeField] private int m_id;
        [SerializeField] private string m_name;

        // 비주얼 에셋 키 오버라이드(D18). 빈칸이면 규약({클래스명}_{ID})을 사용.
        // 키 파생 로직은 View 계층(AssetKeys)이 담당 — Data는 값만 보유.
        [SerializeField] private string m_assetKey;

        public int ID => m_id;
        public string Name => m_name;
        public string AssetKey => m_assetKey;
    }
}
