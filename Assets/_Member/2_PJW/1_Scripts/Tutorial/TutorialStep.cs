namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 튜토리얼 도움말의 식별자. 각 항목은 진입 시점에 1회만 표시되며 서로 독립이다
    /// (순차 진행이 아니므로 순서가 어긋나도 무방하다).
    /// 저장에는 이름 문자열로 기록되므로 중간에 항목을 끼워 넣어도 기존 세이브가 깨지지 않는다.
    /// </summary>
    public enum TutorialStep
    {
        None = 0,

        FishingStart,        // 낚시 시작 → 입질 대기 설명
        Battle,              // 전투 진입 → 체력·제한시간·자동/수동 공격
        CatchSuccess,        // 첫 포획 → 아이템 획득·도감 갱신
        MaterialAcquired,    // 첫 재료 확보 → 조합 안내

        MenuPlayer,          // 플레이어 정보
        MenuInventory,       // 인벤토리
        MenuAquarium,        // 수족관
        MenuMaintenance,     // 정비(상점·강화·조합)
        MenuMap,             // 맵
        MenuCollection,      // 도감
    }
}
