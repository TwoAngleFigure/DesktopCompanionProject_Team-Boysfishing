using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    public class FishCollectionVM : UIViewModelBase
    {
        #region State

        private FishCollectionSystem m_fishCollectionSystem;

        public readonly BindableProperty<IReadOnlyList<FishCollectionDisplayEntry>>
            DisplayEntries = new(Array.Empty<FishCollectionDisplayEntry>());

        public readonly BindableProperty<FishCollectionDisplayEntry?>
            SelectedEntry = new(null);

        public readonly BindableProperty<FishCollectionSortKey>
            CurrentSortKey = new(FishCollectionSortKey.None);

        public readonly BindableProperty<SortDirection>
            CurrentSortDirection = new(SortDirection.Ascending);

        public readonly BindableProperty<bool>
            RegisteredOnly = new(false);

        public readonly BindableProperty<IReadOnlyList<FishCollectionStageInfo>>
            AvailableStages = new(Array.Empty<FishCollectionStageInfo>());

        public readonly BindableProperty<IReadOnlyList<ItemRarity>>
            AvailableRarities = new(Array.Empty<ItemRarity>());

        public readonly BindableProperty<IReadOnlyList<int>>
            AvailableTiers = new(Array.Empty<int>());

        public readonly BindableProperty<IReadOnlyList<int>>
            SelectedStageDataIds = new(Array.Empty<int>());

        public readonly BindableProperty<IReadOnlyList<ItemRarity>>
            SelectedRarities = new(Array.Empty<ItemRarity>());

        public readonly BindableProperty<IReadOnlyList<int>>
            SelectedTiers = new(Array.Empty<int>());

        #endregion

        #region Commands

        public RelayCommand<int> SelectFish { get; private set; }
        public RelayCommand<FishCollectionSortKey> ChangeSort { get; private set; }
        public RelayCommand ToggleSortDirection { get; private set; }

        public RelayCommand<bool> SetRegisteredOnly { get; private set; }

        public RelayCommand<(int stageDataId, bool isSelected)> SetStageFilter { get; private set; }

        public RelayCommand<(ItemRarity rarity, bool isSelected)> SetRarityFilter { get; private set; }

        public RelayCommand<(int tier, bool isSelected)> SetTierFilter { get; private set; }

        public RelayCommand ResetFilters { get; private set; }

        #endregion

        #region Lifecycle

        public override void Bind()
        {
            m_fishCollectionSystem = SystemManager.GetSystem<FishCollectionSystem>();

            SelectFish = new RelayCommand<int>(ExecuteSelectFish);

            ChangeSort = new RelayCommand<FishCollectionSortKey>(ExecuteChangeSort);

            ToggleSortDirection = new RelayCommand(ExecuteToggleSortDirection);

            SetRegisteredOnly = new RelayCommand<bool>(ExecuteSetRegisteredOnly);

            SetStageFilter = new RelayCommand<(int, bool)>(ExecuteSetStageFilter);

            SetRarityFilter = new RelayCommand<(ItemRarity, bool)>(ExecuteSetRarityFilter);

            SetTierFilter = new RelayCommand<(int, bool)>(ExecuteSetTierFilter);

            ResetFilters = new RelayCommand(ExecuteResetFilters);

            if (m_fishCollectionSystem != null)
            {
                m_fishCollectionSystem.OnCollectionUpdated += HandleCollectionUpdated;
            }

            RefreshFilterOptions();
            RefreshDisplayEntries();
        }

        public override void Unbind()
        {
            if (m_fishCollectionSystem != null)
            {
                m_fishCollectionSystem.OnCollectionUpdated -= HandleCollectionUpdated;
            }

            m_fishCollectionSystem = null;

            DisplayEntries.Value = Array.Empty<FishCollectionDisplayEntry>();
            SelectedEntry.Value = null;
            AvailableStages.Value = Array.Empty<FishCollectionStageInfo>();
            AvailableRarities.Value = Array.Empty<ItemRarity>();
            AvailableTiers.Value = Array.Empty<int>();
        }

        #endregion

        #region Selection

        private void ExecuteSelectFish(int fishDataId)
        {
            foreach (FishCollectionDisplayEntry entry in DisplayEntries.Value)
            {
                if (entry.FishDataId != fishDataId)
                {
                    continue;
                }

                SelectedEntry.Value = entry;
                return;
            }

            SelectedEntry.Value = null;
        }

        #endregion

        #region Sort Controls

        private void ExecuteChangeSort(FishCollectionSortKey sortKey)
        {
            if (CurrentSortKey.Value == sortKey)
            {
                return;
            }

            CurrentSortKey.Value = sortKey;
            CurrentSortDirection.Value = GetDefaultDirection(sortKey);

            RefreshDisplayEntries();
        }

        private void ExecuteToggleSortDirection()
        {
            CurrentSortDirection.Value =
                CurrentSortDirection.Value == SortDirection.Ascending
                    ? SortDirection.Descending
                    : SortDirection.Ascending;

            RefreshDisplayEntries();
        }

        private static SortDirection GetDefaultDirection(
            FishCollectionSortKey sortKey)
        {
            return sortKey switch
            {
                FishCollectionSortKey.RecentlyUpdated =>
                    SortDirection.Descending,

                FishCollectionSortKey.BestRecord =>
                    SortDirection.Descending,

                _ => SortDirection.Ascending
            };
        }

        #endregion

        #region Refresh

        private void HandleCollectionUpdated(FishCollectionUpdateResult result)
        {
            RefreshDisplayEntries();
        }

        private void RefreshDisplayEntries()
        {
            if (m_fishCollectionSystem == null)
            {
                DisplayEntries.Value = Array.Empty<FishCollectionDisplayEntry>();
                SelectedEntry.Value = null;
                return;
            }

            IReadOnlyList<FishCollectionDisplayEntry> entries = CreateSortedEntries();

            DisplayEntries.Value = entries;

            RefreshSelectedEntry(entries);
        }

        private void RefreshSelectedEntry(
            IReadOnlyList<FishCollectionDisplayEntry> entries)
        {
            if (!SelectedEntry.Value.HasValue)
            {
                return;
            }

            int selectedFishDataId = SelectedEntry.Value.Value.FishDataId;

            foreach (FishCollectionDisplayEntry entry in entries)
            {
                if (entry.FishDataId != selectedFishDataId)
                {
                    continue;
                }

                SelectedEntry.Value = entry;
                return;
            }

            SelectedEntry.Value = null;
        }

        #endregion

        #region Sorting

        private IReadOnlyList<FishCollectionDisplayEntry> CreateSortedEntries()
        {
            List<FishCollectionDisplayEntry> allEntries = CreateFilteredEntries();

            if (CurrentSortKey.Value == FishCollectionSortKey.None)
            {
                if (CurrentSortDirection.Value ==
                    SortDirection.Descending)
                {
                    allEntries.Reverse();
                }

                return allEntries;
            }

            List<FishCollectionDisplayEntry> registeredEntries = new();
            List<FishCollectionDisplayEntry> unregisteredEntries = new();

            foreach (FishCollectionDisplayEntry entry in allEntries)
            {
                if (entry.IsRegistered)
                {
                    registeredEntries.Add(entry);
                }
                else
                {
                    unregisteredEntries.Add(entry);
                }
            }

            registeredEntries.Sort(CompareRegisteredEntries);

            if (CurrentSortKey.Value == FishCollectionSortKey.CollectionNumber)
            {
                unregisteredEntries.Sort(CompareCollectionNumbers);
            }
            else
            {
                unregisteredEntries.Sort((left, right) => left.FishDataId.CompareTo(right.FishDataId));
            }

            registeredEntries.AddRange(unregisteredEntries);

            return registeredEntries;
        }

        private int CompareRegisteredEntries(
            FishCollectionDisplayEntry left,
            FishCollectionDisplayEntry right)
        {
            int comparison = CurrentSortKey.Value switch
            {
                FishCollectionSortKey.CollectionNumber => left.FishDataId.CompareTo(right.FishDataId),

                FishCollectionSortKey.Name =>
                    string.Compare(
                        left.FishName,
                        right.FishName,
                        StringComparison.Ordinal),

                FishCollectionSortKey.RecentlyUpdated =>
                    left.LastUpdatedOrder.CompareTo(
                        right.LastUpdatedOrder),

                FishCollectionSortKey.BestRecord => CompareBestRecord(left, right),

                _ => 0
            };

            if (CurrentSortDirection.Value == SortDirection.Descending)
            {
                comparison = -comparison;
            }

            if (comparison != 0)
            {
                return comparison;
            }

            return left.FishDataId.CompareTo(right.FishDataId);
        }

        private static int CompareBestRecord(
            FishCollectionDisplayEntry left,
            FishCollectionDisplayEntry right)
        {
            int qualityComparison = left.BestQuality.CompareTo(right.BestQuality);

            if (qualityComparison != 0)
            {
                return qualityComparison;
            }

            return left.BestSize.CompareTo(right.BestSize);
        }

        private int CompareCollectionNumbers(
            FishCollectionDisplayEntry left,
            FishCollectionDisplayEntry right)
        {
            int comparison = left.FishDataId.CompareTo(right.FishDataId);

            return CurrentSortDirection.Value ==
                   SortDirection.Ascending
                   ? comparison
                   : -comparison;
        }

        #endregion

        #region Filter Controls

        private void ExecuteSetRegisteredOnly(bool registeredOnly)
        {
            RegisteredOnly.Value = registeredOnly;
            RefreshDisplayEntries();
        }

        private void ExecuteSetStageFilter((int stageDataId, bool isSelected) args)
        {
            SelectedStageDataIds.Value = UpdateSelection(
                SelectedStageDataIds.Value,
                args.stageDataId,
                args.isSelected);

            RefreshDisplayEntries();
        }

        private void ExecuteSetRarityFilter((ItemRarity rarity, bool isSelected) args)
        {
            SelectedRarities.Value = UpdateSelection(
                SelectedRarities.Value,
                args.rarity,
                args.isSelected);

            RefreshDisplayEntries();
        }

        private void ExecuteSetTierFilter((int tier, bool isSelected) args)
        {
            SelectedTiers.Value = UpdateSelection(
                SelectedTiers.Value,
                args.tier,
                args.isSelected);

            RefreshDisplayEntries();
        }

        private void ExecuteResetFilters()
        {
            RegisteredOnly.Value = false;
            SelectedStageDataIds.Value = Array.Empty<int>();
            SelectedRarities.Value = Array.Empty<ItemRarity>();
            SelectedTiers.Value = Array.Empty<int>();

            RefreshDisplayEntries();
        }

        #endregion

        #region Filter Helpers

        private void RefreshFilterOptions()
        {
            AvailableStages.Value =
                m_fishCollectionSystem.GetAllStageInfos();

            IReadOnlyList<FishCollectionDisplayEntry> entries =
                m_fishCollectionSystem.GetAllDisplayEntries();

            List<ItemRarity> rarities = new();
            List<int> tiers = new();

            foreach (FishCollectionDisplayEntry entry in entries)
            {
                if (!rarities.Contains(entry.Rarity))
                {
                    rarities.Add(entry.Rarity);
                }

                if (!tiers.Contains(entry.Tier))
                {
                    tiers.Add(entry.Tier);
                }
            }

            rarities.Sort(
                (left, right) =>
                    ((int)left).CompareTo((int)right));

            tiers.Sort();

            AvailableRarities.Value = rarities;
            AvailableTiers.Value = tiers;
        }

        private List<FishCollectionDisplayEntry> CreateFilteredEntries()
        {
            IReadOnlyList<FishCollectionDisplayEntry> allEntries =
                m_fishCollectionSystem.GetAllDisplayEntries();

            List<FishCollectionDisplayEntry> filteredEntries = new();

            foreach (FishCollectionDisplayEntry entry in allEntries)
            {
                if (RegisteredOnly.Value && !entry.IsRegistered)
                {
                    continue;
                }

                if (SelectedRarities.Value.Count > 0 &&
                    !Contains(SelectedRarities.Value, entry.Rarity))
                {
                    continue;
                }

                if (SelectedTiers.Value.Count > 0 &&
                    !Contains(SelectedTiers.Value, entry.Tier))
                {
                    continue;
                }

                if (SelectedStageDataIds.Value.Count > 0 &&
                    !MatchesStageFilter(entry.StageDataIds))
                {
                    continue;
                }

                filteredEntries.Add(entry);
            }

            return filteredEntries;
        }

        private bool MatchesStageFilter(
            IReadOnlyList<int> entryStageDataIds)
        {
            foreach (int selectedStageDataId in SelectedStageDataIds.Value)
            {
                if (Contains(entryStageDataIds, selectedStageDataId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains<T>(
            IReadOnlyList<T> values,
            T target)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;

            foreach (T value in values)
            {
                if (comparer.Equals(value, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<T> UpdateSelection<T>(IReadOnlyList<T> current, T value, bool isSelected)
        {
            List<T> updated = new(current);

            if (isSelected)
            {
                if (!updated.Contains(value))
                {
                    updated.Add(value);
                }
            }
            else
            {
                updated.Remove(value);
            }

            return updated;
        }

        #endregion
    }
}
