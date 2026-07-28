using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class MixtureUIView : UIWindowBase
    {
        [Header("Window Buttons")]
        [SerializeField] private UnityEngine.UI.Button m_closeButton;

        [Header("Grid & Slot Setup")]
        [SerializeField] private List<RecipeData_Mixture> m_mixtureList = new();
        [SerializeField] private MixtureSlotView m_mixtureSlotPrefab;
        [SerializeField] private Transform m_gridContainer;

        [Header("Bottom UI")]
        [SerializeField] private Button m_craftBtn;

        [Header("Main Tooltip UI")]
        [SerializeField] private GameObject m_mainTooltipPanel;
        [SerializeField] private TextMeshProUGUI m_tooltipNameText;
        [SerializeField] private TextMeshProUGUI m_tooltipIngredientsText;
        [SerializeField] private Button m_tooltipCloseBtn;

        [Header("Hover Tooltip UI")]
        [SerializeField] private RectTransform m_hoverTooltipRect;
        [SerializeField] private TextMeshProUGUI m_hoverTooltipText;

        private MixtureViewModel m_viewModel;
        private RecipeData_Mixture m_currentSelectedRecipe;
        private readonly List<MixtureSlotView> m_instantiatedSlots = new();

        public override void Bind()
        {
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
                m_craftBtn.onClick.AddListener(() =>
                {
                    if (m_currentSelectedRecipe != null)
                    {
                        m_viewModel.CraftCommand?.Execute(m_currentSelectedRecipe);
                    }
                });
            }

            if (m_tooltipCloseBtn != null)
            {
                m_tooltipCloseBtn.onClick.AddListener(() => m_mainTooltipPanel.SetActive(false));
            }

            GenerateRecipeGrid();
            RefreshUI();
        }

        public override void Unbind()
        {
            if (m_closeButton != null) m_closeButton.onClick.RemoveListener(Close);
            if (m_craftBtn != null) m_craftBtn.onClick.RemoveAllListeners();
            if (m_tooltipCloseBtn != null) m_tooltipCloseBtn.onClick.RemoveAllListeners();

            ClearRecipeGrid();

            if (m_viewModel != null)
            {
                m_viewModel.OnInventoryUpdated -= RefreshUI;
                m_viewModel.Unbind();
                m_viewModel = null;
            }
        }

        private void GenerateRecipeGrid()
        {
            ClearRecipeGrid();

            if (m_mixtureSlotPrefab == null || m_gridContainer == null) return;

            foreach (var recipe in m_mixtureList)
            {
                if (recipe == null) continue;

                MixtureSlotView slotObj = Instantiate(m_mixtureSlotPrefab, m_gridContainer);
                m_instantiatedSlots.Add(slotObj);

                slotObj.Initialize(
                    recipe: recipe,
                    onClick: OnSlotClicked,
                    onHoverEnter: OnSlotHovered,
                    onHoverExit: OnSlotHoverExited
                );
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

        private void OnSlotClicked(RecipeData_Mixture recipe)
        {
            m_currentSelectedRecipe = recipe;

            if (m_mainTooltipPanel != null) m_mainTooltipPanel.SetActive(true);
            if (m_tooltipNameText != null) m_tooltipNameText.text = m_viewModel.GetRecipeName(recipe);

            RefreshUI();
        }

        private void OnSlotHovered(RecipeData_Mixture recipe)
        {
            if (m_hoverTooltipRect != null && m_hoverTooltipText != null)
            {
                m_hoverTooltipText.text = m_viewModel.GetRecipeName(recipe);

                m_hoverTooltipRect.gameObject.SetActive(true);
            }
        }

        private void OnSlotHoverExited()
        {
            if (m_hoverTooltipRect != null)
            {
                m_hoverTooltipRect.gameObject.SetActive(false);
            }
        }

        private void RefreshUI()
        {
            if (m_craftBtn != null)
            {
                m_craftBtn.interactable = m_currentSelectedRecipe != null && m_viewModel.CanCraft(m_currentSelectedRecipe);
            }

            if (m_currentSelectedRecipe != null && m_mainTooltipPanel != null && m_mainTooltipPanel.activeSelf)
            {
                if (m_tooltipIngredientsText != null)
                {
                    string info = "";

                    var parsedIngredients = m_viewModel.GetParsedIngredients(m_currentSelectedRecipe.m_ingredients);

                    foreach (var ing in parsedIngredients)
                    {
                        int owned = m_viewModel.GetOwnedQuantity(ing.itemType, ing.dataId);
                        string colorHex = owned >= ing.amount ? "#00FF00" : "#FF0000";
                        string itemName = m_viewModel.GetItemName(ing.itemType, ing.dataId);

                        info += $"{itemName} : <color={colorHex}>{owned}</color> / {ing.amount}\n";
                    }
                    m_tooltipIngredientsText.text = info;
                }
            }
        }

        private void Update()
        {
            if (m_hoverTooltipRect != null && m_hoverTooltipRect.gameObject.activeSelf)
            {
                Vector2 screenMousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_hoverTooltipRect.parent as RectTransform,
                    screenMousePos, null, out Vector2 localPos);

                m_hoverTooltipRect.localPosition = localPos + new Vector2(20f, -20f);
            }

            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
            {
                if (m_currentSelectedRecipe == null) return;

                var inventorySystem = SystemManager.GetSystem<DesktopCompanion.Systems.InventorySystem>();
                if (inventorySystem == null) return;

                var parsedIngredients = m_viewModel.GetParsedIngredients(m_currentSelectedRecipe.m_ingredients);

                foreach (var ing in parsedIngredients)
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