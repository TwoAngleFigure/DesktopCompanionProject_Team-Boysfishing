namespace DesktopCompanion.Views
{
    /// <summary>
    /// 재료 상세 팝업. 표시는 공통 헤더(아이콘·이름·티어 뱃지)뿐이다.
    /// 아쿠아리움 진행 상황(현재/필요 포인트·시간당 개수)은 재료 게이지 행이 이미 보여주므로
    /// 툴팁에서 반복하지 않는다. 재료 고유 정보가 생기면 <see cref="ApplyBody"/>에 채운다.
    /// </summary>
    public class MaterialTooltipPanel : ItemTooltipPanelBase
    {
        public override TooltipItemKind Kind => TooltipItemKind.Materials;

        protected override void ApplyBody(ItemTooltipData data)
        {
            // 표시할 재료 전용 항목 없음 — 헤더는 베이스가 채운다.
        }
    }
}
