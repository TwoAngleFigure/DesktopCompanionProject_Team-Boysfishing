using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 조합 창. 레시피 목록을 결과물 아이콘 칸으로 늘어놓고, 고른 레시피의 종류·적용 능력치·필요 재료·골드 소모량을 보여준다.
    /// 아이콘·티어 테두리·hover 상세 팝업은 공용 <see cref="ItemSlotView"/>가 맡으므로
    /// 이 창은 자체 툴팁을 갖지 않고 <see cref="IItemTooltipSource"/>로 상세 데이터만 공급한다.
    /// </summary>
    public class MixtureUIView : UIWindowBase, IItemTooltipSource
    {
        [Header("Window Buttons")]
        [SerializeField] private Button m_closeButton;

        [Header("레시피 목록")]
        [SerializeField] private MixtureSlotView m_mixtureSlotPrefab;
        [SerializeField] private Transform m_gridContainer;

        [Header("결과물 정보")]
        [Tooltip("종류 — 미끼/떡밥")]
        [SerializeField] private TMP_Text m_categoryText;

        [Tooltip("보스 소환 미끼일 때만 켜지는 안내 문장. 일반 소모품은 아래 능력치 행이 대신한다")]
        [SerializeField] private TMP_Text m_effectText;
        [Tooltip("일반 소모품의 능력치 행이 생성될 부모")]
        [SerializeField] private Transform m_statRoot;
        [SerializeField] private StatEffectRow m_statRowPrefab;

        [Header("필요 재료")]
        [Tooltip("재료 칸이 생성될 부모")]
        [SerializeField] private Transform m_ingredientRoot;
        [SerializeField] private MaterialCostRow m_ingredientRowPrefab;

        [Header("비용")]
        [Tooltip("골드 소모량. 보유 골드가 모자라면 부족 색으로 칠한다")]
        [SerializeField] private TMP_Text m_goldText;
        [SerializeField] private Color m_goldNormalColor = Color.white;
        [SerializeField] private Color m_goldLackColor = new Color(0.85f, 0.25f, 0.25f);
        [SerializeField] private Button m_craftBtn;

        private MixtureViewModel m_viewModel;
        private MixtureSystem m_mixtureSystem;   // 팝업의 제작 비용을 레시피 기준으로 채우기 위해 보관
        private RecipeData_Mixture m_currentSelectedRecipe;

        private readonly List<MixtureSlotView> m_instantiatedSlots = new();
        private readonly List<StatEffectRow> m_statRows = new();
        private readonly List<MaterialCostRow> m_ingredientRows = new();

        // 현재 표시 중인 재료 목록. 보유 수량 갱신과 hover 팝업 조립에서 다시 읽는다.
        private List<ParsedIngredient> m_currentIngredients = new();

        public override void Bind()
        {
            m_mixtureSystem = SystemManager.GetSystem<MixtureSystem>();

            m_viewModel = new MixtureViewModel();
            m_viewModel.Inject(SystemManager, EntityManager);
            m_viewModel.Bind();

            m_viewModel.OnInventoryUpdated += RefreshUI;

            if (m_closeButton != null)
            {
                m_closeButton.onClick.AddListener(Close);
            }

            if (m_craftBtn != null)
            {
                m_craftBtn.onClick.AddListener(Craft);
            }

            GenerateRecipeGrid();
            SelectRecipe(null);
        }

        public override void Unbind()
        {
            if (m_closeButton != null) m_closeButton.onClick.RemoveListener(Close);
            if (m_craftBtn != null) m_craftBtn.onClick.RemoveListener(Craft);

            ClearRecipeGrid();

            if (m_viewModel != null)
            {
                m_viewModel.OnInventoryUpdated -= RefreshUI;
                m_viewModel.Unbind();
                m_viewModel = null;
            }

            m_mixtureSystem = null;
        }

        // ── 레시피 목록 ──

        private void GenerateRecipeGrid()
        {
            ClearRecipeGrid();

            if (m_mixtureSlotPrefab == null || m_gridContainer == null) return;
            if (m_mixtureSystem == null) return;

            var allRecipes = m_mixtureSystem.GetAllRecipes();

            foreach (var recipe in allRecipes)
            {
                if (recipe == null) continue;

                MixtureSlotView slotObj = Instantiate(m_mixtureSlotPrefab, m_gridContainer);
                m_instantiatedSlots.Add(slotObj);

                // 칸에 보이는 것은 레시피가 아니라 '결과물 아이템'이다 — 정의 기반으로 슬롯을 만든다.
                ItemSlotVD slotVD = ItemSlotVD.FromData(m_viewModel.GetResultData(recipe), recipe.m_resultCount);

                slotObj.Set(recipe, slotVD, ResolveIcon(slotVD != null ? slotVD.IconKey : null), this, SelectRecipe);
            }
        }

        private void ClearRecipeGrid()
        {
            foreach (var slot in m_instantiatedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            m_instantiatedSlots.Clear();
        }

        private void SelectRecipe(RecipeData_Mixture recipe)
        {
            m_currentSelectedRecipe = recipe;
            m_currentIngredients = m_viewModel.GetParsedIngredients(recipe != null ? recipe.m_ingredients : null);

            if (m_categoryText != null) m_categoryText.text = m_viewModel.GetResultCategoryText(recipe);

            RefreshSelectionHighlight();
            RefreshEffect(recipe);
            RefreshUI();
        }

        /// <summary>고른 칸만 선택 표시를 켠다. 앞서 골랐던 칸은 이 순회에서 함께 꺼진다.</summary>
        private void RefreshSelectionHighlight()
        {
            foreach (MixtureSlotView slot in m_instantiatedSlots)
            {
                if (slot != null) slot.SetSelected(slot.Recipe == m_currentSelectedRecipe && m_currentSelectedRecipe != null);
            }
        }

        /// <summary>
        /// '적용 능력치' 칸을 채운다. 데이터에 설명 문장이 있으면(보스 소환 미끼·랜덤 장비 생산 등)
        /// 그 문장을, 없으면 능력치 수치 행을 보여준다. 둘은 같은 자리를 쓰므로 한쪽만 켠다.
        /// </summary>
        private void RefreshEffect(RecipeData_Mixture recipe)
        {
            string text = m_viewModel.GetResultDescription(recipe);
            bool hasText = string.IsNullOrEmpty(text) == false;

            if (m_effectText != null)
            {
                m_effectText.gameObject.SetActive(hasText);
                if (hasText) m_effectText.text = text;
            }

            RefreshStats(hasText ? System.Array.Empty<StatModifier>() : m_viewModel.GetResultModifiers(recipe));
        }

        // 행 풀링 — 모자라면 만들고, 남으면 끈다.
        private void RefreshStats(StatModifier[] modifiers)
        {
            if (m_statRowPrefab == null || m_statRoot == null) return;

            while (m_statRows.Count < modifiers.Length)
            {
                m_statRows.Add(Instantiate(m_statRowPrefab, m_statRoot));
            }

            for (int i = 0; i < modifiers.Length; i++)
            {
                m_statRows[i].gameObject.SetActive(true);
                m_statRows[i].Set(modifiers[i]);
            }
            for (int i = modifiers.Length; i < m_statRows.Count; i++)
            {
                m_statRows[i].gameObject.SetActive(false);
            }
        }

        private void Craft()
        {
            if (m_currentSelectedRecipe != null)
            {
                m_viewModel.CraftCommand?.Execute(m_currentSelectedRecipe);
            }
        }

        // ── 갱신 ──

        private void RefreshUI()
        {
            RefreshIngredients();
            RefreshCost();
        }

        // 행 풀링 — 모자라면 만들고, 남으면 끈다.
        private void RefreshIngredients()
        {
            if (m_ingredientRowPrefab == null || m_ingredientRoot == null) return;

            while (m_ingredientRows.Count < m_currentIngredients.Count)
            {
                m_ingredientRows.Add(Instantiate(m_ingredientRowPrefab, m_ingredientRoot));
            }

            for (int i = 0; i < m_currentIngredients.Count; i++)
            {
                ParsedIngredient ingredient = m_currentIngredients[i];

                ItemSlotVD slotVD = ItemSlotVD.FromData(
                    m_viewModel.GetItemData(ingredient.itemType, ingredient.dataId));

                int owned = m_viewModel.GetOwnedQuantity(ingredient.itemType, ingredient.dataId);

                m_ingredientRows[i].gameObject.SetActive(true);
                m_ingredientRows[i].Set(slotVD, ResolveIcon(slotVD != null ? slotVD.IconKey : null), this,
                                        owned, ingredient.amount);
            }
            for (int i = m_currentIngredients.Count; i < m_ingredientRows.Count; i++)
            {
                m_ingredientRows[i].gameObject.SetActive(false);
            }
        }

        private void RefreshCost()
        {
            int goldCost = m_currentSelectedRecipe != null ? m_currentSelectedRecipe.m_goldCost : 0;
            bool enoughGold = m_viewModel.CurrentGold >= goldCost;

            if (m_goldText != null)
            {
                m_goldText.text = m_currentSelectedRecipe != null ? goldCost.ToString("N0") : string.Empty;
                m_goldText.color = enoughGold ? m_goldNormalColor : m_goldLackColor;
            }

            if (m_craftBtn != null)
            {
                m_craftBtn.interactable = m_currentSelectedRecipe != null
                                          && m_viewModel.CanCraft(m_currentSelectedRecipe)
                                          && enoughGold;
            }
        }

        // ── 툴팁 ──

        /// <summary>
        /// hover된 칸의 팝업 상세를 조립한다. 결과물·재료 모두 개체가 아닌 정의에서 온 표시다.
        /// </summary>
        public ItemTooltipData BuildTooltip(ItemSlotVD vd)
        {
            if (vd == null || m_viewModel == null)
            {
                return null;
            }

            ItemData data = m_viewModel.GetItemData(ToItemType(vd.Kind), vd.DataId);

            // MixtureSystem을 함께 넘겨야 팝업의 제작 비용이 아이템 정의가 아닌 레시피 기준으로 채워진다.
            return data != null ? ItemTooltipBuilder.FromData(data, m_mixtureSystem) : null;
        }

        private static ItemType ToItemType(TooltipItemKind kind) => kind switch
        {
            TooltipItemKind.Fish => ItemType.Fish,
            TooltipItemKind.Materials => ItemType.Materials,
            TooltipItemKind.Equipment => ItemType.Equipment,
            TooltipItemKind.Consumables => ItemType.Consumables,
            _ => default,
        };

        private Sprite ResolveIcon(string iconKey)
        {
            if (AssetProvider == null || string.IsNullOrEmpty(iconKey))
            {
                return null;
            }
            AssetProvider.TryGet(iconKey, out Sprite sprite);
            return sprite;
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
            {
                if (m_currentSelectedRecipe == null) return;

                var inventorySystem = SystemManager.GetSystem<InventorySystem>();
                if (inventorySystem == null) return;

                foreach (var ing in m_currentIngredients)
                {
                    for (int i = 0; i < ing.amount; i++)
                    {
                        EntityHandle newHandle = EntityManager.Create<ItemData_Materials>(ing.dataId);
                        inventorySystem.AddItem(newHandle);
                    }
                }

                Debug.Log($"[테스트] {m_viewModel.GetRecipeName(m_currentSelectedRecipe)} 전용 재료가 지급되었습니다!");
                RefreshUI();
            }
        }
    }
}
