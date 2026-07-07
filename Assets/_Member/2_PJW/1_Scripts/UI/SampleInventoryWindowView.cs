using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 팀원용 예제 ViewModel. System 구독(→BindableProperty)과 명령(→System 호출)을 보여준다.
    /// </summary>
    public class InventoryViewModel : UIViewModelBase
    {
        private InventorySystem m_inventory;

        // System → View: 표시용 상태.
        public readonly BindableProperty<int> UsedFishSlots = new(0);

        // View → System: 사용자 명령(슬롯 from → to 이동).
        public RelayCommand<(int from, int to)> MoveFishCommand { get; private set; }

        public override void Bind()
        {
            m_inventory = SystemManager.GetSystem<InventorySystem>();
            MoveFishCommand = new RelayCommand<(int from, int to)>(MoveFish);

            if (m_inventory != null)
            {
                m_inventory.OnInventoryChanged += Refresh;   // System → View
                Refresh();
            }
        }

        public override void Unbind()
        {
            if (m_inventory != null)
            {
                m_inventory.OnInventoryChanged -= Refresh;
            }
            m_inventory = null;
        }

        // 사용자 명령 → System public 메서드 호출(View→System).
        private void MoveFish((int from, int to) arg)
        {
            m_inventory?.SwapSlots(ItemType.Fish, arg.from, arg.to);
        }

        // System 상태 변화 → 표시용 상태 갱신(System→View).
        private void Refresh()
        {
            if (m_inventory != null)
            {
                UsedFishSlots.Value = m_inventory.GetUsedSlotCount(ItemType.Fish);
            }
        }
    }

    /// <summary>
    /// 팀원용 예제 View(윈도우형). 복사해서 각자 화면으로 바꿔 쓰면 된다.
    /// - 인벤토리는 창이므로 UIWindowBase 상속 → 우클릭 닫기 대상.
    /// - Bind()에서 ViewModel을 생성·주입·Bind하고, VM의 BindableProperty에 위젯을 바인딩한다.
    /// </summary>
    public class SampleInventoryWindowView : UIWindowBase
    {
        private readonly InventoryViewModel m_vm = new();

        public override void Bind()
        {
            m_vm.Inject(SystemManager);
            m_vm.Bind();
            m_vm.UsedFishSlots.Bind(OnUsedFishSlotsChanged);   // 현재값 즉시 + 이후 변경 구독
        }

        public override void Unbind()
        {
            m_vm.UsedFishSlots.Unbind(OnUsedFishSlotsChanged);
            m_vm.Unbind();
        }

        private void OnUsedFishSlotsChanged(int used)
        {
            // TODO: 실제 uGUI 라벨/슬롯 위젯 갱신.
            Debug.Log($"[SampleInventoryWindowView] 물고기 슬롯 사용: {used}");
        }
    }
}
