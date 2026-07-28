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

        public RelayCommand<int> SelectFish { get; private set; }

        public override void Bind()
        {
            m_fishCollectionSystem = SystemManager.GetSystem<FishCollectionSystem>();

            SelectFish = new RelayCommand<int>(ExecuteSelectFish);

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

            IReadOnlyList<FishCollectionDisplayEntry> entries =
                m_fishCollectionSystem.GetAllDisplayEntries();

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
    }
}