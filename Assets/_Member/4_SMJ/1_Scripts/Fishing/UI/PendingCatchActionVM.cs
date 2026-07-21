using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 인벤토리 부족으로 보류된 물고기의 선택 UI 상태와 명령을 담당합니다.
    /// </summary>
    public class PendingCatchActionVM : UIViewModelBase
    {
        private FishingSystem m_fishingSystem;
        private ShopSystem m_shopSystem;

        public readonly BindableProperty<bool> IsPendingCatchVisible = new(false);
        public readonly BindableProperty<string> SellPriceText = new(string.Empty);

        public RelayCommand SellPendingCatch { get; private set; }

        public override void Bind()
        {
            m_fishingSystem = SystemManager.GetSystem<FishingSystem>();
            m_shopSystem = SystemManager.GetSystem<ShopSystem>();
            SellPendingCatch = new RelayCommand(
                ExecuteSellPendingCatch,
                CanSellPendingCatch);

            if (m_fishingSystem == null)
            {
                IsPendingCatchVisible.Value = false;
                SellPriceText.Value = string.Empty;
                return;
            }

            m_fishingSystem.OnPendingCatchChanged += RefreshPendingCatchState;
            RefreshPendingCatchState();
        }

        public override void Unbind()
        {
            if (m_fishingSystem != null)
            {
                m_fishingSystem.OnPendingCatchChanged -= RefreshPendingCatchState;
            }

            m_fishingSystem = null;
            m_shopSystem = null;
        }

        private void RefreshPendingCatchState()
        {
            bool hasPendingCatch =
                m_fishingSystem != null && m_fishingSystem.HasPendingCatch;

            IsPendingCatchVisible.Value = hasPendingCatch;

            if (!hasPendingCatch || m_shopSystem == null)
            {
                SellPriceText.Value = string.Empty;
                return;
            }

            int sellGold = m_shopSystem.CalculateSellGold(
                m_fishingSystem.PendingCatch,
                1);

            SellPriceText.Value = $"판매 - {sellGold:N0} G";
        }

        private bool CanSellPendingCatch()
        {
            return m_fishingSystem != null &&
                   m_shopSystem != null &&
                   m_fishingSystem.HasPendingCatch;
        }

        private void ExecuteSellPendingCatch()
        {
            if (m_fishingSystem == null)
            {
                return;
            }

            m_fishingSystem.TrySellPendingCatch(out _);
        }
    }
}
