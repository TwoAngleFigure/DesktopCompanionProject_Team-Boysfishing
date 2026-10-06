namespace DesktopCompanion.Views
{
    /// <summary>
    /// 재료 상세 팝업 패널. 재료 전용 본문이 없으므로 베이스가 채우는 공통 헤더만 표시한다.
    /// </summary>
    public class MaterialTooltipPanel : ItemTooltipPanelBase
    {
        public override TooltipItemKind Kind => TooltipItemKind.Materials;

        protected override void ApplyBody(ItemTooltipData data)
        {
            // 재료 전용 표시 항목 없음.
        }
    }
}
