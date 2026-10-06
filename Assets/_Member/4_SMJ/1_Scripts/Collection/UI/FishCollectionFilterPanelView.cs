using System.Collections.Generic;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class FishCollectionFilterPanelView : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField]
        private FishCollectionFilterToggleView m_togglePrefab;

        [Header("Contents")]
        [SerializeField] private Transform m_stageContent;
        [SerializeField] private Transform m_rarityContent;
        [SerializeField] private Transform m_tierContent;

        [Header("Buttons")]
        [SerializeField] private Button m_resetFilterButton;
        [SerializeField] private Button m_closeButton;

        private FishCollectionVM m_vm;

        private readonly Dictionary<int, FishCollectionFilterToggleView>
            m_stageToggles = new();

        private readonly Dictionary<ItemRarity, FishCollectionFilterToggleView>
            m_rarityToggles = new();

        private readonly Dictionary<int, FishCollectionFilterToggleView>
            m_tierToggles = new();

        public void Bind(FishCollectionVM vm)
        {
            m_vm = vm;

            CreateStageToggles();
            CreateRarityToggles();
            CreateTierToggles();

            m_vm.SelectedStageDataIds.Bind(
                HandleSelectedStagesChanged);

            m_vm.SelectedRarities.Bind(
                HandleSelectedRaritiesChanged);

            m_vm.SelectedTiers.Bind(
                HandleSelectedTiersChanged);

            m_resetFilterButton.onClick.AddListener(
                HandleResetFilterClicked);

            m_closeButton.onClick.AddListener(
                HandleCloseButtonClicked);
        }

        public void Unbind()
        {
            if (m_vm == null)
            {
                return;
            }

            m_vm.SelectedStageDataIds.Unbind(
                HandleSelectedStagesChanged);

            m_vm.SelectedRarities.Unbind(
                HandleSelectedRaritiesChanged);

            m_vm.SelectedTiers.Unbind(
                HandleSelectedTiersChanged);

            m_resetFilterButton.onClick.RemoveListener(
                HandleResetFilterClicked);

            m_closeButton.onClick.RemoveListener(
                HandleCloseButtonClicked);

            ClearToggles(m_stageToggles);
            ClearToggles(m_rarityToggles);
            ClearToggles(m_tierToggles);

            m_vm = null;
        }

        private void CreateStageToggles()
        {
            foreach (FishCollectionStageInfo stage in
                     m_vm.AvailableStages.Value)
            {
                FishCollectionFilterToggleView toggle =
                    Instantiate(m_togglePrefab, m_stageContent);

                int stageDataId = stage.StageDataId;

                toggle.Initialize(
                    stage.StageName,
                    Contains(
                        m_vm.SelectedStageDataIds.Value,
                        stageDataId),
                    isSelected =>
                        m_vm.SetStageFilter.Execute(
                            (stageDataId, isSelected)));

                m_stageToggles.Add(stageDataId, toggle);
            }
        }

        private void CreateRarityToggles()
        {
            foreach (ItemRarity rarity in
                     m_vm.AvailableRarities.Value)
            {
                FishCollectionFilterToggleView toggle =
                    Instantiate(m_togglePrefab, m_rarityContent);

                toggle.Initialize(
                    GetRarityText(rarity),
                    Contains(
                        m_vm.SelectedRarities.Value,
                        rarity),
                    isSelected =>
                        m_vm.SetRarityFilter.Execute(
                            (rarity, isSelected)));

                m_rarityToggles.Add(rarity, toggle);
            }
        }

        private void CreateTierToggles()
        {
            foreach (int tier in m_vm.AvailableTiers.Value)
            {
                FishCollectionFilterToggleView toggle =
                    Instantiate(m_togglePrefab, m_tierContent);

                toggle.Initialize(
                    $"T{tier}",
                    Contains(
                        m_vm.SelectedTiers.Value,
                        tier),
                    isSelected =>
                        m_vm.SetTierFilter.Execute(
                            (tier, isSelected)));

                m_tierToggles.Add(tier, toggle);
            }
        }

        private void HandleSelectedStagesChanged(
            IReadOnlyList<int> selectedStageDataIds)
        {
            foreach (KeyValuePair<int, FishCollectionFilterToggleView>
                     pair in m_stageToggles)
            {
                pair.Value.SetSelected(
                    Contains(selectedStageDataIds, pair.Key));
            }
        }

        private void HandleSelectedRaritiesChanged(
            IReadOnlyList<ItemRarity> selectedRarities)
        {
            foreach (KeyValuePair<ItemRarity, FishCollectionFilterToggleView>
                     pair in m_rarityToggles)
            {
                pair.Value.SetSelected(
                    Contains(selectedRarities, pair.Key));
            }
        }

        private void HandleSelectedTiersChanged(
            IReadOnlyList<int> selectedTiers)
        {
            foreach (KeyValuePair<int, FishCollectionFilterToggleView>
                     pair in m_tierToggles)
            {
                pair.Value.SetSelected(
                    Contains(selectedTiers, pair.Key));
            }
        }

        private void HandleResetFilterClicked()
        {
            m_vm.ResetFilters.Execute();
        }

        private void HandleCloseButtonClicked()
        {
            gameObject.SetActive(false);
        }

        private void ClearToggles<TKey>(
            Dictionary<TKey, FishCollectionFilterToggleView> toggles)
        {
            foreach (FishCollectionFilterToggleView toggle in
                     toggles.Values)
            {
                toggle.Release();
                Destroy(toggle.gameObject);
            }

            toggles.Clear();
        }

        private static bool Contains<T>(
            IReadOnlyList<T> values,
            T target)
        {
            EqualityComparer<T> comparer =
                EqualityComparer<T>.Default;

            foreach (T value in values)
            {
                if (comparer.Equals(value, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetRarityText(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Normal => "일반",
                ItemRarity.Uncommon => "고급",
                ItemRarity.Rare => "희귀",
                ItemRarity.Epic => "영웅",
                ItemRarity.Legendary => "전설",
                ItemRarity.Boss => "보스",
                _ => rarity.ToString()
            };
        }
    }
}
