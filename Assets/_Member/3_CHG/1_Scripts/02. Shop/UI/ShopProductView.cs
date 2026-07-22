using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopProductView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Button")]
    [SerializeField] private Button m_button;

    [Header("Visual")]
    [SerializeField] private Sprite m_itemIcon;
    [SerializeField] private TMP_Text m_itemName;
    [SerializeField] private TMP_Text m_desciptionText;
    [SerializeField] private TMP_Text m_priceText;

    private int m_productIndex;

    Action<int> m_onClick;
    Action<int> m_onPointerEnter;
    Action<int> m_onPointerExit;

    public void Initialize(int productIndex, Action<int> onClick, Action<int> onPointerEnter, Action<int> onPointerExit)
    {
        m_productIndex = productIndex;

        m_onClick = onClick;
        m_onPointerEnter = onPointerEnter;
        m_onPointerExit = onPointerExit;

        if(m_button == null)
        {
            Debug.LogWarning("[ShopProductView] 버튼이 할당되지 않았습니다.");
            return;
        }

        m_button.onClick.AddListener
    }

    public void Set(ShopProductViewData data, Sprite icon)
    {

    }

}
