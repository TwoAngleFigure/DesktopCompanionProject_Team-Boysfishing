using System;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public enum ItemPickupSource
    {
        None,
        Inventory,
        Equipment
    }

    public class ItemPickupController : MonoBehaviour
    {
        [Header("Pickup Icon")]
        [SerializeField] private Canvas m_canvas;
        [SerializeField] private RectTransform m_iconRoot;
        [SerializeField] private Image m_iconImage;

        public bool HasItem { get; private set; }

        public ItemPickupSource Source { get; private set; }

        //Source가 인벤토리일 때 출처
        public ItemType SourceSlotType { get; private set; }
        public int SourceSlotIndex { get; private set; } = -1;

        //Source가 장비창일 때 출처
        public EquipmentMountingArea SourceEquipmentArea { get; private set; }

        public EntityHandle PickedHandle { get; private set; }

        public event Action OnPickupChanged;

        private void Awake()
        {
            if (m_canvas == null)
            {
                m_canvas = GetComponentInParent<Canvas>();
            }

            if (m_iconImage != null)
            {
                m_iconImage.raycastTarget = false;
            }

            HideIcon();
        }

        private void Update()
        {
            if (!HasItem || m_iconRoot == null || Mouse.current == null)
            {
                return;
            }

            RectTransform parentRect = m_iconRoot.parent as RectTransform;

            if (parentRect == null)
            {
                return;
            }

            Camera eventCamera = null;

            if (m_canvas != null && m_canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = m_canvas.worldCamera;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, mousePosition, eventCamera, out Vector2 localPosition))
            {
                m_iconRoot.anchoredPosition = localPosition;
            }
        }

        /// <summary>
        /// 인벤토리 슬롯의 아이템 선택
        /// </summary>
        public void BeginPickup(ItemType sourceSlotType, int sourceSlotIndex, EntityHandle pickedHandle, Sprite icon)
        {
            Source = ItemPickupSource.Inventory;

            SourceSlotType = sourceSlotType;
            SourceSlotIndex = sourceSlotIndex;

            //쓰지 않는 정보 초기화
            SourceEquipmentArea = default;

            PickedHandle = pickedHandle;
            HasItem = true;

            if (m_iconImage != null)
            {
                m_iconImage.sprite = icon;
                m_iconImage.enabled = icon != null;
            }

            if (m_iconRoot != null)
            {
                m_iconRoot.gameObject.SetActive(true);
                m_iconRoot.SetAsLastSibling();
            }

            OnPickupChanged?.Invoke();
        }

        /// <summary>
        /// 장비창에 장착된 아이템 선택
        /// </summary>
        public void BeginEquipmentPickup(EquipmentMountingArea sourceEquipmentArea, EntityHandle pickedHandle, Sprite icon)
        {
            Source = ItemPickupSource.Equipment;

            //쓰지 않는 정보 초기화
            SourceSlotType = default;
            SourceSlotIndex = -1;

            SourceEquipmentArea = sourceEquipmentArea;
            PickedHandle = pickedHandle;
            HasItem = true;

            if (m_iconImage != null)
            {
                m_iconImage.sprite = icon;
                m_iconImage.enabled = icon != null;
            }

            if (m_iconRoot != null)
            {
                m_iconRoot.gameObject.SetActive(true);
                m_iconRoot.SetAsLastSibling();
            }

            OnPickupChanged?.Invoke();
        }

        public void ClearPickup()
        {
            Source = ItemPickupSource.None;

            SourceSlotType = default;
            SourceSlotIndex = -1;
            SourceEquipmentArea = default;

            PickedHandle = default;
            HasItem = false;

            HideIcon();
            OnPickupChanged?.Invoke();
        }

        private void HideIcon()
        {
            if (m_iconImage != null)
            {
                m_iconImage.sprite = null;
                m_iconImage.enabled = false;
            }

            if (m_iconRoot != null)
            {
                m_iconRoot.gameObject.SetActive(false);
            }
        }
    }
}