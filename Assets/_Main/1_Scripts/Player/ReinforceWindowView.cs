using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 장비 강화 창. 슬롯에 올린 장비의 티어·강화 수치·능력치(현재/증가분)·강화 재료·골드 소모량을 표시한다.
    /// 표시 데이터는 <see cref="ReinforceViewModel.Info"/>가 값 그대로 넘겨주며, 문장 구성은 이 창이 맡는다.
    /// <see cref="IItemTooltipSource"/>를 구현해 장비·재료 슬롯의 hover 팝업 상세를 공급한다.
    /// </summary>
    public class ReinforceWindowView : UIWindowBase, IItemTooltipSource
    {
        [Header("장비 슬롯")]
        [Tooltip("아이콘·티어 테두리·hover 팝업 담당")]
        [SerializeField] private ItemSlotView m_equipmentSlot;
        [Tooltip("드롭·클릭 입력 담당. 장비 슬롯과 같은 오브젝트에 붙인다")]
        [SerializeField] private ReinforceSlotWidget m_slotWidget;
        [SerializeField] private TMP_Text m_nameText;

        [Header("뱃지")]
        [SerializeField] private TMP_Text m_tierText;
        [Tooltip("티어 색으로 칠할 뱃지 배경(선택)")]
        [SerializeField] private Image m_tierBadge;
        [Tooltip("현재 강화 수치. +N 형식")]
        [SerializeField] private TMP_Text m_upgradeText;
        [Tooltip("강화 단계 색으로 칠할 뱃지 배경(선택)")]
        [SerializeField] private Image m_upgradeBadge;

        [Header("본문 구역")]
        [Tooltip("장비가 올라가 있을 때만 켜지는 구역(능력치 표·재료·비용)")]
        [SerializeField] private GameObject m_bodySection;
        [Tooltip("장비가 없을 때 켜지는 안내")]
        [SerializeField] private GameObject m_emptyHint;

        [Header("능력치")]
        [Tooltip("장착 부위 — 표의 '부위' 열(단일 셀)")]
        [SerializeField] private TMP_Text m_mountingAreaText;
        [Tooltip("능력치 행이 생성될 부모")]
        [SerializeField] private Transform m_statRoot;
        [SerializeField] private StatEffectRow m_statRowPrefab;

        [Header("강화 재료")]
        [Tooltip("재료 칸이 생성될 부모")]
        [SerializeField] private Transform m_materialRoot;
        [SerializeField] private MaterialCostRow m_materialRowPrefab;

        [Header("비용")]
        [Tooltip("골드 소모량. 보유 골드가 모자라면 부족 색으로 칠한다")]
        [SerializeField] private TMP_Text m_goldText;
        [SerializeField] private Color m_goldNormalColor = Color.white;
        [SerializeField] private Color m_goldLackColor = new Color(0.85f, 0.25f, 0.25f);
        [SerializeField] private Button m_reinforceBtn;

        [Header("등급 색")]
        [Tooltip("인벤토리 슬롯 프리팹에 넣은 것과 같은 에셋을 할당할 것 — 테두리와 뱃지 색이 어긋나지 않게")]
        [SerializeField] private ItemGradeStyle m_style;

        [Header("Inventory Link")]
        [SerializeField] private ItemPickupController m_itemPickupController;

        private ReinforceViewModel m_viewModel;

        private readonly List<StatEffectRow> m_statRows = new();
        private readonly List<MaterialCostRow> m_materialRows = new();

        // 현재 표시 중인 스냅샷. 골드만 바뀌는 갱신과 툴팁 조립에서 다시 읽는다.
        private ReinforceVD m_current = new();

        public override void Bind()
        {
            if (m_itemPickupController == null)
            {
                m_itemPickupController = GameObject.FindAnyObjectByType<ItemPickupController>();
            }

            m_viewModel = new ReinforceViewModel();
            m_viewModel.Inject(SystemManager, EntityManager);
            m_viewModel.Bind();

            // [MVVM 바인딩] .Bind()를 쓰면 구독과 동시에 현재 값이 콜백으로 즉시 전달되어 초기 UI가 제대로 갱신됩니다.
            m_viewModel.Info.Bind(Refresh);
            m_viewModel.CurrentGold.Bind(RefreshGold);

            // 마우스 드래그 앤 드롭 외에도, '클릭하여 집기(Click-to-Pickup)' 방식을 지원하기 위해
            // 강화 슬롯을 클릭했을 때의 이벤트를 추가로 구독합니다.
            if (m_slotWidget != null)
            {
                m_slotWidget.OnSlotDropped += HandleItemDropped;
                m_slotWidget.OnSlotClicked += HandleSlotClicked;
            }
            if (m_reinforceBtn != null)
            {
                m_reinforceBtn.onClick.AddListener(Reinforce);
            }
        }

        // 강화창 UI가 닫힐 때 호출되며, 메모리 누수를 막기 위해 구독했던 이벤트들을 모두 해제합니다.
        public override void Unbind()
        {
            if (m_slotWidget != null)
            {
                m_slotWidget.OnSlotDropped -= HandleItemDropped;
                m_slotWidget.OnSlotClicked -= HandleSlotClicked;
            }
            if (m_reinforceBtn != null)
            {
                m_reinforceBtn.onClick.RemoveListener(Reinforce);
            }

            if (m_viewModel != null)
            {
                // .Bind()로 연결한 메서드들은 반드시 .Unbind()로 동일하게 해제해주어야 합니다.
                m_viewModel.Info.Unbind(Refresh);
                m_viewModel.CurrentGold.Unbind(RefreshGold);

                m_viewModel.Unbind();
                m_viewModel = null;
            }
        }

        // ── 갱신 ──

        private void Refresh(ReinforceVD vd)
        {
            m_current = vd ?? new ReinforceVD();

            // 장비가 없으면 본문을 통째로 끄고 안내만 남긴다.
            SetActive(m_bodySection, m_current.HasEquipment);
            SetActive(m_emptyHint, m_current.HasEquipment == false);

            RefreshHeader(m_current);
            RefreshStats(m_current);
            RefreshMaterials(m_current);
            RefreshGold(m_viewModel != null ? m_viewModel.CurrentGold.Value : 0);
        }

        private void RefreshHeader(ReinforceVD vd)
        {
            if (m_equipmentSlot != null)
            {
                // 장비는 개체가 있으므로 개체 경로로 만든다(티어 테두리가 채워진다).
                ItemSlotVD slotVD = vd.HasEquipment ? ItemSlotVD.FromEntity(vd.Handle, EntityManager) : null;
                m_equipmentSlot.Set(slotVD, ResolveIcon(slotVD != null ? slotVD.IconKey : null), this);
            }

            if (m_nameText != null) m_nameText.text = vd.HasEquipment ? vd.Name : string.Empty;
            if (m_upgradeText != null) m_upgradeText.text = vd.HasEquipment ? $"+{vd.UpgradeLevel}" : string.Empty;
            if (m_mountingAreaText != null)
            {
                m_mountingAreaText.text = vd.HasEquipment ? ItemLabels.MountingArea(vd.MountingArea) : string.Empty;
            }

            if (m_tierText != null) m_tierText.text = vd.HasEquipment ? $"T{vd.Tier}" : string.Empty;

            if (m_style == null || vd.HasEquipment == false)
            {
                return;
            }

            // 슬롯 테두리·팝업 뱃지와 같은 티어 색 — 창에서 팝업으로 시선이 옮겨가도 색이 이어진다.
            Color tierColor = m_style.TierColor(vd.Tier);
            if (m_tierText != null) m_tierText.color = tierColor;
            if (m_tierBadge != null) m_tierBadge.color = tierColor;

            // 강화 단계는 레어도 팔레트를 빌려 쓴다 — 등급이 오른다는 인상을 슬롯·툴팁과 같은 색 계열로 준다.
            Color upgradeColor = m_style.UpgradeColor(vd.UpgradeLevel);
            if (m_upgradeText != null) m_upgradeText.color = upgradeColor;
            if (m_upgradeBadge != null) m_upgradeBadge.color = upgradeColor;
        }

        // 행 풀링 — 모자라면 만들고, 남으면 끈다.
        private void RefreshStats(ReinforceVD vd)
        {
            if (m_statRowPrefab == null || m_statRoot == null)
            {
                return;
            }

            List<ReinforceStatVD> list = vd.Stats;
            while (m_statRows.Count < list.Count)
            {
                m_statRows.Add(Instantiate(m_statRowPrefab, m_statRoot));
            }

            for (int i = 0; i < list.Count; i++)
            {
                ReinforceStatVD stat = list[i];

                m_statRows[i].gameObject.SetActive(true);
                m_statRows[i].Set(stat.Stat, stat.Operation, stat.Current, stat.Delta);
            }
            for (int i = list.Count; i < m_statRows.Count; i++)
            {
                m_statRows[i].gameObject.SetActive(false);
            }
        }

        private void RefreshMaterials(ReinforceVD vd)
        {
            if (m_materialRowPrefab == null || m_materialRoot == null)
            {
                return;
            }

            List<ReinforceMaterialVD> list = vd.Materials;
            while (m_materialRows.Count < list.Count)
            {
                m_materialRows.Add(Instantiate(m_materialRowPrefab, m_materialRoot));
            }

            for (int i = 0; i < list.Count; i++)
            {
                ReinforceMaterialVD material = list[i];

                // 재료는 개체가 아니라 강화 비용 정의에서 온 표시다 — 정의 기반으로 슬롯을 만든다.
                ItemSlotVD slotVD = ItemSlotVD.FromData(material.Data);

                m_materialRows[i].gameObject.SetActive(true);
                m_materialRows[i].Set(slotVD, ResolveIcon(slotVD != null ? slotVD.IconKey : null), this,
                                      material.Owned, material.Required);
            }
            for (int i = list.Count; i < m_materialRows.Count; i++)
            {
                m_materialRows[i].gameObject.SetActive(false);
            }
        }

        /// <summary>골드 소모량과 강화 버튼의 활성 조건을 갱신한다. 보유 골드만 바뀌어도 이 경로로 들어온다.</summary>
        private void RefreshGold(int currentGold)
        {
            bool hasCost = m_current.HasEquipment && m_current.IsMaxLevel == false;
            bool enoughGold = currentGold >= m_current.GoldCost;

            if (m_goldText != null)
            {
                m_goldText.text = hasCost ? m_current.GoldCost.ToString("N0") : string.Empty;
                m_goldText.color = enoughGold ? m_goldNormalColor : m_goldLackColor;
            }

            if (m_reinforceBtn != null)
            {
                // 시스템(PlayerSystem.TryReinforceEquipment)의 검사 조건과 같은 범위를 본다.
                m_reinforceBtn.interactable = hasCost && m_current.MaterialsEnough && enoughGold;
            }
        }

        // ── 입력 ──

        private void Reinforce() => m_viewModel?.ReinforceCommand.Execute();

        // 강화 슬롯을 마우스로 클릭했을 때 호출되는 함수
        private void HandleSlotClicked()
        {
            if (m_itemPickupController != null && m_itemPickupController.HasItem)
            {
                // 현재 마우스 포인터에 아이템을 들고 있다면 드롭한 것과 동일하게 처리합니다.
                HandleItemDropped();
            }
            else if (m_current.HasEquipment)
            {
                // 들고 있는 아이템이 없는데 슬롯을 클릭했다면, 슬롯에 올라가 있는 장비를 해제합니다.
                m_viewModel?.RegisterEquipment(default);
            }
        }

        private void HandleItemDropped()
        {
            if (m_itemPickupController == null || m_itemPickupController.HasItem == false)
            {
                return;
            }

            EntityHandle droppedItem = m_itemPickupController.PickedHandle;
            Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(droppedItem);

            if (equipment != null)
            {
                m_viewModel.RegisterEquipment(droppedItem);
                m_itemPickupController.ClearPickup();
            }
            else
            {
                Debug.LogWarning("장비 아이템만 강화 슬롯에 올릴 수 있습니다.");
            }
        }

        // ── 툴팁 ──

        /// <summary>hover된 슬롯의 팝업 상세를 조립한다. 장비는 개체 기반, 재료는 정의 기반이다.</summary>
        public ItemTooltipData BuildTooltip(ItemSlotVD vd)
        {
            if (vd == null)
            {
                return null;
            }

            if (vd.Kind == TooltipItemKind.Equipment)
            {
                return ItemTooltipBuilder.FromEntity(vd.Handle, EntityManager);
            }

            // 재료 칸에는 개체가 없다 — 현재 표시 중인 비용 목록에서 같은 dataId의 정의를 찾는다.
            foreach (ReinforceMaterialVD material in m_current.Materials)
            {
                if (material.Data != null && material.Data.ID == vd.DataId)
                {
                    return ItemTooltipBuilder.FromData(material.Data);
                }
            }
            return null;
        }

        // ── 보조 ──

        private Sprite ResolveIcon(string iconKey)
        {
            if (AssetProvider == null || string.IsNullOrEmpty(iconKey))
            {
                return null;
            }
            AssetProvider.TryGet(iconKey, out Sprite sprite);
            return sprite;
        }

        private static void SetActive(GameObject target, bool on)
        {
            if (target != null) target.SetActive(on);
        }
    }
}
