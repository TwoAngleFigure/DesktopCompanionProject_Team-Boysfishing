namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 도움말 항목. 처음 겪는 시점에 해금되며 이후 도움말 도감에서 상시 열람한다.
    /// 항목끼리 순서 의존이 없으므로 어느 것이 먼저 해금돼도 무방하다.
    /// 저장에는 이름 문자열로 기록되므로 중간에 항목을 끼워 넣어도 기존 세이브가 깨지지 않는다.
    /// </summary>
    public enum HelpTopic
    {
        None = 0,

        // ── 진행 흐름 (개요 → 낚시 → 전투 → 포획 → 노트) ──
        Overview,         // 게임 개요 — 첫 실행, 카메라 하강 완료 시점
        Fishing,          // 낚시 시작·입질 대기
        Battle,           // 체력·제한시간·자동/수동 공격
        Catch,            // 포획·아이템 획득·도감 갱신
        Note,             // 노트(통합 UI) — 어떤 기능이 들어 있는지

        // ── 노트의 각 기능 (해당 창을 처음 열 때) ──
        MenuInventory,    // 인벤토리
        MenuPlayer,       // 플레이어 정보(장비·스탯)
        MenuReinforce,    // 강화
        MenuMixture,      // 제작
        MenuShop,         // 상점
        MenuMap,          // 지도
        MenuCollection,   // 도감
        MenuAquarium,     // 수족관
        MenuSettings,     // 설정
    }
}
