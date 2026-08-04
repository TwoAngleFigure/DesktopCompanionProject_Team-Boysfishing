using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;

namespace DesktopCompanion.Views
{
    public class FishCollectionVM : UIViewModelBase
    {
        private FishCollectionSystem m_fishCollectionSystem;

        public readonly BindableProperty<IReadOnlyList<FishCollectionDisplayEntry>>
            DisplayEntries = new(Array.Empty<FishCollectionDisplayEntry>());

        public readonly BindableProperty<FishCollectionDisplayEntry?>
            SelectedEntry = new(null);

        public readonly BindableProperty<FishCollectionSortKey>
            CurrentSortKey = new(FishCollectionSortKey.None);

        public readonly BindableProperty<SortDirection>
            CurrentSortDirection = new(SortDirection.Ascending);

        public RelayCommand<int> SelectFish { get; private set; }
        public RelayCommand<FishCollectionSortKey> ChangeSort { get; private set; }
        public RelayCommand ToggleSortDirection { get; private set; }

        public override void Bind()
        {
            m_fishCollectionSystem = SystemManager.GetSystem<FishCollectionSystem>();

            SelectFish = new RelayCommand<int>(ExecuteSelectFish);

            SelectFish = new RelayCommand<int>(ExecuteSelectFish);

            ChangeSort = new RelayCommand<FishCollectionSortKey>(ExecuteChangeSort);

            ToggleSortDirection = new RelayCommand(ExecuteToggleSortDirection);

            if (m_fishCollectionSystem != null)
            {
                m_fishCollectionSystem.OnCollectionUpdated += HandleCollectionUpdated;
            }

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
        }

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

        private IReadOnlyList<FishCollectionDisplayEntry> CreateSortedEntries()
        {
            List<FishCollectionDisplayEntry> allEntries =
                new(m_fishCollectionSystem.GetAllDisplayEntries());

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
    }
}