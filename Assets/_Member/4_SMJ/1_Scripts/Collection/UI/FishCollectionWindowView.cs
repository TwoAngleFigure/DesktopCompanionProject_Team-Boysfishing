using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class FishCollectionWindowView : UIWindowBase
    {
        [Header("Window")]
        [SerializeField] private Button m_closeButton;

        [Header("Collection List")]
        [SerializeField] private Transform m_entryContent;
        [SerializeField] private FishCollectionEntryView m_entryPrefab;

        [Header("Detail")]
        [SerializeField] private Image m_fishIconImage;
        [SerializeField] private Image[] m_bestQualityStars;
        [SerializeField] private TMP_Text m_detailNameText;
        [SerializeField] private TMP_Text m_bestQualityText;
        [SerializeField] private TMP_Text m_bestSizeText;
        [SerializeField] private TMP_Text m_metadataText;

        [Header("Filter")]
        [SerializeField]
        private FishCollectionFilterPanelView m_filterPanel;

        [Header("Control Bar")]
        [SerializeField]
        private FishCollectionControlBarView m_controlBar;

        private readonly FishCollectionVM m_vm = new();
        private readonly List<FishCollectionEntryView> m_entryViews = new();

        public override void Bind()
        {
            if (m_detailNameText != null)
            {
                m_detailNameText.alignment = TextAlignmentOptions.Center;
            }

            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_filterPanel?.Bind(m_vm);
            m_controlBar?.Bind(m_vm, m_filterPanel);

            m_vm.DisplayEntries.Bind(HandleDisplayEntriesChanged);
            m_vm.SelectedEntry.Bind(HandleSelectedEntryChanged);

            m_closeButton?.onClick.AddListener(Close);
        }

        public override void Unbind()
        {
            m_vm.DisplayEntries.Unbind(HandleDisplayEntriesChanged);
            m_vm.SelectedEntry.Unbind(HandleSelectedEntryChanged);

            m_closeButton?.onClick.RemoveListener(Close);

            m_controlBar?.Unbind();
            m_filterPanel?.Unbind();

            ClearEntryViews();

            m_vm.Unbind();
        }

        private void HandleDisplayEntriesChanged(
            IReadOnlyList<FishCollectionDisplayEntry> entries)
        {
            ClearEntryViews();

            if (m_entryPrefab == null || m_entryContent == null)
            {
                return;
            }

            foreach (FishCollectionDisplayEntry entry in entries)
            {
                FishCollectionEntryView entryView = Instantiate(m_entryPrefab, m_entryContent);

                entryView.SetEntry(entry, HandleEntryClicked);

                m_entryViews.Add(entryView);
            }
        }

        private void HandleEntryClicked(int fishDataId)
        {
            m_vm.SelectFish?.Execute(fishDataId);
        }

        private void HandleSelectedEntryChanged(FishCollectionDisplayEntry? selectedEntry)
        {
            bool hasSelection = selectedEntry.HasValue;

            SetDetailTextVisible(hasSelection);
            SetFishIcon(null);
            SetBestQualityStars(0);

            if (!hasSelection)
            {
                return;
            }

            FishCollectionDisplayEntry entry = selectedEntry.Value;

            m_metadataText.text = BuildMetadataText(entry);

            string iconKey = $"ItemData_Fish_{entry.FishDataId}_Icon";

            if (AssetProvider != null &&
                AssetProvider.TryGet(iconKey, out Sprite fishIcon))
            {
                SetFishIcon(fishIcon, !entry.IsRegistered);
            }

            if (!entry.IsRegistered)
            {
                m_detailNameText.text = $"{entry.CollectionNumberText} ???";
                m_bestQualityText.text = "최고 품질: -";
                m_bestSizeText.text = "최고 크기: -";
                return;
            }

            m_detailNameText.text = $"{entry.CollectionNumberText} {entry.FishName}";
            m_bestQualityText.text = "최고 품질:";
            SetBestQualityStars((int)entry.BestQuality);
            m_bestSizeText.text = $"최고 크기: {entry.BestSize:0.0} cm";
        }

        private void SetFishIcon(Sprite icon, bool isSilhouette = false)
        {
            if (m_fishIconImage == null)
            {
                return;
            }

            m_fishIconImage.sprite = icon;
            m_fishIconImage.color = isSilhouette
                ? Color.black
                : Color.white;
            m_fishIconImage.gameObject.SetActive(icon != null);
        }

        private void SetBestQualityStars(int count)
        {
            if (m_bestQualityStars == null)
            {
                return;
            }

            int visibleCount = Mathf.Clamp(count, 0, m_bestQualityStars.Length);

            for (int i = 0; i < m_bestQualityStars.Length; i++)
            {
                if (m_bestQualityStars[i] != null)
                {
                    m_bestQualityStars[i].gameObject.SetActive(i < visibleCount);
                }
            }
        }

        private void SetDetailTextVisible(bool isVisible)
        {
            if (m_detailNameText != null)
            {
                m_detailNameText.gameObject.SetActive(isVisible);
            }

            if (m_bestQualityText != null)
            {
                m_bestQualityText.gameObject.SetActive(isVisible);
            }

            if (m_bestSizeText != null)
            {
                m_bestSizeText.gameObject.SetActive(isVisible);
            }

            if (m_metadataText != null)
            {
                m_metadataText.gameObject.SetActive(isVisible);
            }
        }

        private static string BuildMetadataText(
            FishCollectionDisplayEntry entry)
        {
            string stageNames = entry.StageNames.Count > 0
                ? string.Join(", ", entry.StageNames)
                : "-";

            return
                $"희귀도: {GetRarityText(entry.Rarity)}\n\n" +
                $"티어: T{entry.Tier}\n\n" +
                $"생산 재료: {entry.ProductionMaterialName}\n\n" +
                $"출현 지역: {stageNames}";
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

        private void ClearEntryViews()
        {
            foreach (FishCollectionEntryView entryView in m_entryViews)
            {
                if (entryView != null)
                {
                    Destroy(entryView.gameObject);
                }
            }

            m_entryViews.Clear();
        }
    }
}
