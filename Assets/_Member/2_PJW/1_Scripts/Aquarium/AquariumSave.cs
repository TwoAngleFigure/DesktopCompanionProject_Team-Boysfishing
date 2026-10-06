using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 아쿠아리움 영구 상태 DTO. 배치 물고기는 handle·dataId·롤값(Size/Quality)으로 저장해
    /// 복원 시 Entity를 재등록하고, 재료 포인트·보류 생산물은 병렬 리스트로 저장한다.
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

        /// <summary>배치된 물고기 1개체의 저장 항목. 개체 식별·롤값과 생산 주기 진행값을 담는다.</summary>
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
