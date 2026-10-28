using System;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class InventorySellView : MonoBehaviour
    {
        [Header("Sell Buttons")]
        [SerializeField] private Button m_sellButton;
        [SerializeField] private Button m_autoSellButton;

        [Header("Sell Quantity Popup")]
        [SerializeField] private GameObject m_quantityPopup;
        [SerializeField] private TMP_Text m_quantityItemNameText;
        [SerializeField] private TMP_Text m_quantityMaxText;
        [SerializeField] private TMP_InputField m_quantityInput;
        [SerializeField] private Slider m_quantitySlider;
        [SerializeField] private Button m_quantityMaxButton;
        [SerializeField] private Button m_quantityApplyButton;
        [SerializeField] private Button m_quantityCancelButton;

        [Header("Sell Confirm Popup")]
        [SerializeField] private GameObject m_confirmPopup;
        [SerializeField] private TMP_Text m_confirmText;
        [SerializeField] private Button m_confirmButton;
        [SerializeField] private Button m_confirmCancelButton;

        [Header("Auto Sell Filter Popup")]
        [SerializeField] private GameObject m_filterPopup;
        [SerializeField] private TMP_Text m_autoSellStatusText;
        [SerializeField] private TMP_Dropdown m_qualityDropdown;
        [SerializeField] private TMP_Dropdown m_rarityDropdown;
        [SerializeField] private Button m_filterApplyButton;
        [SerializeField] private Button m_filterDisableButton;
        [SerializeField] private Button m_filterCloseButton;

        private readonly InventorySellViewModel m_vm = new();
        private bool m_isBound;

        public bool IsSellMode => m_vm.IsSellMode.Value;

        public event Action OnSellVisualStateChanged;
        public event Action<bool> OnSellModeChanged;

        public void Bind(SystemManager systemManager, EntityManager entityManager)
        {
            if (m_isBound) return;

            m_vm.Inject(systemManager, entityManager);
            m_vm.Bind();

            m_vm.IsSellMode.Bind(RefreshSellMode);

            m_vm.IsQuantityPopupOpen.Bind(RefreshQuantityPopup);
            m_vm.QuantityItemName.Bind(RefreshQuantityItemName);
            m_vm.QuantityAmount.Bind(RefreshQuantityAmount);
            m_vm.QuantityMaxAmount.Bind(RefreshQuantityMaxAmount);

            m_vm.IsConfirmPopupOpen.Bind(RefreshConfirmPopup);
            m_vm.SelectedItemCount.Bind(RefreshConfirmItemCount);
            m_vm.SelectedTotalAmount.Bind(RefreshConfirmTotalAmount);
            m_vm.ExpectedGold.Bind(RefreshConfirmExpectedGold);

            m_vm.IsFilterPopupOpen.Bind(RefreshFilterPopup);
            m_vm.AutoSellEnabled.Bind(RefreshAutoSellStatus);
            m_vm.AutoSellMaxQuality.Bind(RefreshAutoSellQuality);
            m_vm.AutoSellMaxRarity.Bind(RefreshAutoSellRarity);

            m_vm.OnSellSelectionChanged += HandleSellSelectionChanged;

            m_sellButton?.onClick.AddListener(OnSellButtonClicked);
            m_autoSellButton?.onClick.AddListener(OnAutoSellFilterClicked);

            m_quantityInput?.onValueChanged.AddListener(OnQuantityInputChanged);
            m_quantityInput?.onEndEdit.AddListener(OnQuantityInputEndEdit);
            m_quantitySlider?.onValueChanged.AddListener(OnQuantitySliderChanged);
            m_quantityMaxButton?.onClick.AddListener(OnQuantityMaxClicked);
            m_quantityApplyButton?.onClick.AddListener(OnQuantityApplyClicked);
            m_quantityCancelButton?.onClick.AddListener(OnQuantityCancelClicked);

            m_confirmButton?.onClick.AddListener(OnConfirmClicked);
            m_confirmCancelButton?.onClick.AddListener(OnConfirmCancelClicked);

            m_qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
            m_rarityDropdown?.onValueChanged.AddListener(OnRarityChanged);
            m_filterApplyButton?.onClick.AddListener(OnFilterApplyClicked);
            m_filterDisableButton?.onClick.AddListener(OnFilterDisableClicked);
            m_filterCloseButton?.onClick.AddListener(OnFilterCloseClicked);

            m_isBound = true;

            // 첫 화면 상태 보정
            RefreshSellMode(m_vm.IsSellMode.Value);
            RefreshQuantityPopup(m_vm.IsQuantityPopupOpen.Value);
            RefreshConfirmPopup(m_vm.IsConfirmPopupOpen.Value);
            RefreshFilterPopup(m_vm.IsFilterPopupOpen.Value);
        }

        public void Unbind()
        {
            if (!m_isBound) return;

            m_sellButton?.onClick.RemoveListener(OnSellButtonClicked);
            m_autoSellButton?.onClick.RemoveListener(OnAutoSellFilterClicked);

            m_quantityInput?.onValueChanged.RemoveListener(OnQuantityInputChanged);
            m_quantityInput?.onEndEdit.RemoveListener(OnQuantityInputEndEdit);
            m_quantitySlider?.onValueChanged.RemoveListener(OnQuantitySliderChanged);
            m_quantityMaxButton?.onClick.RemoveListener(OnQuantityMaxClicked);
            m_quantityApplyButton?.onClick.RemoveListener(OnQuantityApplyClicked);
            m_quantityCancelButton?.onClick.RemoveListener(OnQuantityCancelClicked);

            m_confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            m_confirmCancelButton?.onClick.RemoveListener(OnConfirmCancelClicked);

            m_qualityDropdown?.onValueChanged.RemoveListener(OnQualityChanged);
            m_rarityDropdown?.onValueChanged.RemoveListener(OnRarityChanged);
            m_filterApplyButton?.onClick.RemoveListener(OnFilterApplyClicked);
            m_filterDisableButton?.onClick.RemoveListener(OnFilterDisableClicked);
            m_filterCloseButton?.onClick.RemoveListener(OnFilterCloseClicked);

            m_vm.OnSellSelectionChanged -= HandleSellSelectionChanged;

            m_vm.IsSellMode.Unbind(RefreshSellMode);

            m_vm.IsQuantityPopupOpen.Unbind(RefreshQuantityPopup);
            m_vm.QuantityItemName.Unbind(RefreshQuantityItemName);
            m_vm.QuantityAmount.Unbind(RefreshQuantityAmount);
            m_vm.QuantityMaxAmount.Unbind(RefreshQuantityMaxAmount);

            m_vm.IsConfirmPopupOpen.Unbind(RefreshConfirmPopup);
            m_vm.SelectedItemCount.Unbind(RefreshConfirmItemCount);
            m_vm.SelectedTotalAmount.Unbind(RefreshConfirmTotalAmount);
            m_vm.ExpectedGold.Unbind(RefreshConfirmExpectedGold);

            m_vm.IsFilterPopupOpen.Unbind(RefreshFilterPopup);
            m_vm.AutoSellEnabled.Unbind(RefreshAutoSellStatus);
            m_vm.AutoSellMaxQuality.Unbind(RefreshAutoSellQuality);
            m_vm.AutoSellMaxRarity.Unbind(RefreshAutoSellRarity);

            m_vm.Unbind();

            m_quantityPopup?.SetActive(false);
            m_confirmPopup?.SetActive(false);
            m_filterPopup?.SetActive(false);

            OnSellVisualStateChanged = null;
            OnSellModeChanged = null;
            m_isBound = false;
        }

        public void HandleSlotClick(InventorySlotViewData slotData)
        {
            m_vm.SelectSellItemCommand.Execute(slotData);
        }

        public int GetSelectedAmount(EntityHandle handle)
        {
            return m_vm.GetSelectedAmount(handle);
        }

        public void ResetSellState()
        {
            m_vm.ResetSellStateCommand.Execute();
            m_vm.CloseFilterCommand.Execute();
        }

        private void OnSellButtonClicked()
        {
            m_vm.SellButtonCommand.Execute();
        }

        private void OnAutoSellFilterClicked()
        {
            m_vm.OpenFilterCommand.Execute();
        }

        private void OnQuantityInputChanged(string value)
        {
            if (int.TryParse(value, out int amount))
            {
                m_vm.SetQuantityCommand.Execute(amount);
            }
        }

        private void OnQuantityInputEndEdit(string value)
        {
            if (int.TryParse(value, out int amount))
            {
                m_vm.SetQuantityCommand.Execute(amount);
            }

            // 빈 문자열이나 범위를 벗어난 값이 화면에 남지 않도록 보정된 값을 다시 표시
            RefreshQuantityAmount(m_vm.QuantityAmount.Value);
        }

        private void OnQuantitySliderChanged(float value)
        {
            m_vm.SetQuantityCommand.Execute(Mathf.RoundToInt(value));
        }

        private void OnQuantityMaxClicked()
        {
            m_vm.SetQuantityCommand.Execute(m_vm.QuantityMaxAmount.Value);
        }

        private void OnQuantityApplyClicked()
        {
            m_vm.ApplyQuantityCommand.Execute();
        }

        private void OnQuantityCancelClicked()
        {
            m_vm.CancelQuantityCommand.Execute();
        }

        private void OnConfirmClicked()
        {
            m_vm.ConfirmSellCommand.Execute();
        }

        private void OnConfirmCancelClicked()
        {
            m_vm.CancelSellCommand.Execute();
        }

        private void OnQualityChanged(int dropdownIndex)
        {
            if (dropdownIndex < 0 || dropdownIndex > 4) return;

            m_vm.SetAutoSellQualityCommand.Execute((ItemQuality)(dropdownIndex + 1));
        }

        private void OnRarityChanged(int dropdownIndex)
        {
            if (dropdownIndex < 0 || dropdownIndex > 4) return; // Boss 제외

            m_vm.SetAutoSellRarityCommand.Execute((ItemRarity)dropdownIndex);
        }

        private void OnFilterApplyClicked()
        {
            m_vm.ApplyAutoSellFilterCommand.Execute();
        }

        private void OnFilterDisableClicked()
        {
            m_vm.DisableAutoSellCommand.Execute();
        }

        private void OnFilterCloseClicked()
        {
            m_vm.CloseFilterCommand.Execute();
        }

        private void HandleSellSelectionChanged()
        {
            OnSellVisualStateChanged?.Invoke();
        }

        private void RefreshSellMode(bool isSellMode)
        {
            if (m_autoSellButton != null)
            {
                m_autoSellButton.interactable = !isSellMode;
            }

            OnSellModeChanged?.Invoke(isSellMode);
            OnSellVisualStateChanged?.Invoke();
        }

        private void RefreshQuantityPopup(bool isOpen)
        {
            m_quantityPopup?.SetActive(isOpen);
        }

        private void RefreshQuantityItemName(string itemName)
        {
            if (m_quantityItemNameText != null)
            {
                m_quantityItemNameText.text = itemName;
            }
        }

        private void RefreshQuantityAmount(int amount)
        {
            m_quantityInput?.SetTextWithoutNotify(amount.ToString());
            m_quantitySlider?.SetValueWithoutNotify(amount);
        }

        private void RefreshQuantityMaxAmount(int maxAmount)
        {
            int normalizedMaxAmount = Math.Max(1, maxAmount);

            if (m_quantityMaxText != null)
            {
                m_quantityMaxText.text = $"현재 보유 : {normalizedMaxAmount:N0}개";
            }

            if (m_quantitySlider != null)
            {
                m_quantitySlider.wholeNumbers = true;
                m_quantitySlider.minValue = 1f;
                m_quantitySlider.maxValue = normalizedMaxAmount;
                m_quantitySlider.SetValueWithoutNotify(Mathf.Clamp(m_vm.QuantityAmount.Value, 1, normalizedMaxAmount));
            }
        }

        private void RefreshConfirmPopup(bool isOpen)
        {
            m_confirmPopup?.SetActive(isOpen);
            RefreshConfirmText();
        }

        private void RefreshConfirmItemCount(int _)
        {
            RefreshConfirmText();
        }

        private void RefreshConfirmTotalAmount(int _)
        {
            RefreshConfirmText();
        }

        private void RefreshConfirmExpectedGold(int _)
        {
            RefreshConfirmText();
        }

        private void RefreshConfirmText()
        {
            if (m_confirmText == null)
            {
                return;
            }

            m_confirmText.text =
                $"선택 항목: {m_vm.SelectedItemCount.Value:N0}개\n" +
                $"총 판매 수량: {m_vm.SelectedTotalAmount.Value:N0}개\n" +
                $"예상 획득 골드: {m_vm.ExpectedGold.Value:N0} G\n\n" +
                "선택한 아이템을 판매하시겠습니까?";
        }

        private void RefreshFilterPopup(bool isOpen)
        {
            m_filterPopup?.SetActive(isOpen);
        }

        private void RefreshAutoSellStatus(bool enabled)
        {
            if (m_autoSellStatusText != null)
            {
                m_autoSellStatusText.text = enabled
                    ? "자동 판매: 사용 중"
                    : "자동 판매: 사용 안 함";
            }

            if (m_filterDisableButton != null)
            {
                m_filterDisableButton.interactable = enabled;
            }
        }

        private void RefreshAutoSellQuality(ItemQuality quality)
        {
            if (m_qualityDropdown == null || m_qualityDropdown.options.Count == 0) return;

            int index = Mathf.Clamp((int)quality - 1, 0, m_qualityDropdown.options.Count - 1);
            m_qualityDropdown.SetValueWithoutNotify(index);
        }

        private void RefreshAutoSellRarity(ItemRarity rarity)
        {
            if (m_rarityDropdown == null || m_rarityDropdown.options.Count == 0) return;

            int index = Mathf.Clamp((int)rarity, 0, m_rarityDropdown.options.Count - 1);
            m_rarityDropdown.SetValueWithoutNotify(index);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        //외부 참조용 판매 진입 코드
        public void EnterSellMode()
        {
            if (IsSellMode)
            {
                return;
            }

            m_vm.SellButtonCommand.Execute();
        }
    }
}