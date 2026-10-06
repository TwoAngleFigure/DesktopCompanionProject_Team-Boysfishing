using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 물고기 1개체를 표시하는 목록 행. 인벤토리·수족관 목록에 공용으로 쓴다.
    /// 슬롯·이름·현재 정렬 기준의 값·액션 버튼(과 불가 사유)을 표시하며, 나머지 상세는 슬롯 hover 팝업이 담당한다.
    /// 액션 대상은 인덱스가 아닌 <see cref="EntityHandle"/>이므로 정렬을 바꿔도 지목이 어긋나지 않는다.
    /// </summary>
    public class AquariumFishRow : MonoBehaviour
    {
        [SerializeField] private ItemSlotView m_slot;
        [SerializeField] private TMP_Text m_nameText;

        [Tooltip("현재 정렬 기준의 값(티어로 정렬 중이면 T2, 크기로 정렬 중이면 45.8 …)")]
        [SerializeField] private TMP_Text m_sortValueText;

        [Header("액션")]
        [SerializeField] private Button m_actionButton;
        [SerializeField] private TMP_Text m_actionLabel;
        [Tooltip("액션 불가 사유(자동판매 대상·창고 만석·수용량 부족 등)")]
        [SerializeField] private TMP_Text m_blockReasonText;

        private EntityHandle m_payload;
        private Action<EntityHandle> m_onAction;

        private void Awake()
        {
            if (m_actionButton != null)
            {
                m_actionButton.onClick.AddListener(() => m_onAction?.Invoke(m_payload));
            }
        }

        public void Set(AquariumFishVD vd, ItemSlotVD slotVD, Sprite icon, IItemTooltipSource tooltipSource,
                        AquariumFishSortKey sortKey, string actionText, Action<EntityHandle> onAction)
        {
            if (vd == null)
            {
                return;
            }

            if (m_slot != null) m_slot.Set(slotVD, icon, tooltipSource);
            if (m_nameText != null) m_nameText.text = vd.Name;
            if (m_sortValueText != null) m_sortValueText.text = AquariumSort.FormatValue(vd, sortKey);

            if (m_actionLabel != null) m_actionLabel.text = actionText;
            if (m_actionButton != null) m_actionButton.interactable = vd.CanAct;
            if (m_blockReasonText != null) m_blockReasonText.text = vd.CanAct ? string.Empty : vd.BlockReason;

            m_payload = vd.Handle;
            m_onAction = onAction;
        }
    }
}
