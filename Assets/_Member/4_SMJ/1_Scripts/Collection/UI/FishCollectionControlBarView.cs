using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class FishCollectionControlBarView : MonoBehaviour
    {
        private static readonly FishCollectionSortKey[] SortKeys =
        {
            FishCollectionSortKey.None,
            FishCollectionSortKey.CollectionNumber,
            FishCollectionSortKey.Name,
            FishCollectionSortKey.RecentlyUpdated,
            FishCollectionSortKey.BestRecord
        };

        [Header("Filter")]
        [SerializeField] private Button m_filterButton;
        [SerializeField] private Toggle m_registeredOnlyToggle;

        [Header("Sort")]
        [SerializeField] private TMP_Dropdown m_sortDropdown;
        [SerializeField] private Button m_sortDirectionButton;
        [SerializeField] private TMP_Text m_sortDirectionText;

        private FishCollectionVM m_vm;
        private FishCollectionFilterPanelView m_filterPanel;

        public void Bind(
            FishCollectionVM vm,
            FishCollectionFilterPanelView filterPanel)
        {
            m_vm = vm;
            m_filterPanel = filterPanel;

            InitializeSortOptions();

            m_filterButton.onClick.AddListener(
                HandleFilterButtonClicked);

            m_registeredOnlyToggle.onValueChanged.AddListener(
                HandleRegisteredOnlyChanged);

            m_sortDropdown.onValueChanged.AddListener(
                HandleSortDropdownChanged);

            m_sortDirectionButton.onClick.AddListener(
                HandleSortDirectionClicked);

            m_vm.RegisteredOnly.Bind(
                HandleRegisteredOnlyStateChanged);

            m_vm.CurrentSortKey.Bind(
                HandleSortKeyChanged);

            m_vm.CurrentSortDirection.Bind(
                HandleSortDirectionChanged);
        }

        public void Unbind()
        {
            if (m_vm == null)
            {
                return;
            }

            m_vm.RegisteredOnly.Unbind(
                HandleRegisteredOnlyStateChanged);

            m_vm.CurrentSortKey.Unbind(
                HandleSortKeyChanged);

            m_vm.CurrentSortDirection.Unbind(
                HandleSortDirectionChanged);

            m_filterButton.onClick.RemoveListener(
                HandleFilterButtonClicked);

            m_registeredOnlyToggle.onValueChanged.RemoveListener(
                HandleRegisteredOnlyChanged);

            m_sortDropdown.onValueChanged.RemoveListener(
                HandleSortDropdownChanged);

            m_sortDirectionButton.onClick.RemoveListener(
                HandleSortDirectionClicked);

            m_vm = null;
            m_filterPanel = null;
        }

        private void InitializeSortOptions()
        {
            List<string> options = new()
            {
                "없음",
                "도감 번호 순",
                "이름순",
                "최근 갱신순",
                "최고 기록 순"
            };

            m_sortDropdown.ClearOptions();
            m_sortDropdown.AddOptions(options);
        }

        private void HandleFilterButtonClicked()
        {
            if (m_filterPanel == null)
            {
                return;
            }

            GameObject panelObject = m_filterPanel.gameObject;
            panelObject.SetActive(!panelObject.activeSelf);
        }

        private void HandleRegisteredOnlyChanged(bool registeredOnly)
        {
            m_vm.SetRegisteredOnly.Execute(registeredOnly);
        }

        private void HandleSortDropdownChanged(int optionIndex)
        {
            m_vm.ChangeSort.Execute(SortKeys[optionIndex]);
        }

        private void HandleSortDirectionClicked()
        {
            m_vm.ToggleSortDirection.Execute();
        }

        private void HandleRegisteredOnlyStateChanged(bool registeredOnly)
        {
            m_registeredOnlyToggle.SetIsOnWithoutNotify(registeredOnly);
        }

        private void HandleSortKeyChanged(FishCollectionSortKey sortKey)
        {
            int optionIndex = Array.IndexOf(SortKeys, sortKey);
            m_sortDropdown.SetValueWithoutNotify(optionIndex);
        }

        private void HandleSortDirectionChanged(SortDirection direction)
        {
            m_sortDirectionText.text =
                direction == SortDirection.Ascending
                    ? "▲"
                    : "▼";
        }
    }
}
