using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 아쿠아리움 영구 상태 DTO(§15.3). Plan A: 배치 물고기는 인벤토리에서 이동해 온 '개체'이므로
    /// 인벤토리처럼 handle+dataId+롤값(Size/Quality)을 저장해 복원 시 Entity를 재등록한다.
    /// 사전(재료 포인트/보류)은 JSON 친화적으로 병렬 리스트로 저장한다.
    /// </summary>
    [Serializable]
    public class AquariumSave
    {
        public string lastSettleUtc;              // DateTime.UtcNow.ToString("o")
        public int upgradeLevel;

        public List<FishEntry> fish = new();      // 배치된 물고기 개체들

        public List<int> pointMatIds = new();     // 재료 dataId
        public List<int> pointValues = new();     // 누적 포인트 — pointMatIds와 병렬

        public List<int> pendingMatIds = new();   // 보류된 생산물 재료 dataId
        public List<int> pendingCounts = new();   // 보류 개수 — pendingMatIds와 병렬

        /// <summary>배치된 물고기 1개체(인벤토리 슬롯 저장과 동형 + 생산 주기 진행값).</summary>
        [Serializable]
        public class FishEntry
        {
            public string handle;   // EntityHandle 문자열(개체 참조 보존)
            public int dataId;      // ItemData_Fish ID
            public float size;      // 개체 롤값
            public int quality;     // ItemQuality(int)
            public float progress;  // 생산 주기 진행 잔여(초)
        }
    }
}
