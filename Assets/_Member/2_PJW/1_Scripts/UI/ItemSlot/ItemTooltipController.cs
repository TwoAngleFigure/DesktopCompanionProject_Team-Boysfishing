using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>팝업을 슬롯의 어느 높이에 맞출지 지정한다.</summary>
    public enum TooltipVerticalAlign
    {
        SlotTop,      // 슬롯 위쪽 변
        SlotMiddle,   // 슬롯 세로 중앙
        SlotBottom,   // 슬롯 아래쪽 변
    }

    /// <summary>
    /// 아이템 상세 팝업의 단일 표시자. 씬에 1개만 둔다.
    /// 종류별 패널을 자식으로 갖고 <see cref="ItemTooltipData.Kind"/>에 맞는 하나만 켠다.
    /// 팝업을 hover된 슬롯 옆에 붙이되, 캔버스 우측을 넘치면 좌우를 뒤집고 마지막에 화면 안으로 민다.
    /// UIViewBase를 상속해 AssetProvider를 주입받으며, 활성 윈도우 스택에는 참여하지 않는다.
    /// </summary>
    public class ItemTooltipController : UIViewBase
    {
        private static ItemTooltipController s_instance;

        [Header("Canvas")]
        [SerializeField] private Canvas m_canvas;
        [Tooltip("팝업 본체 RectTransform. 비우면 이 오브젝트")]
        [SerializeField] private RectTransform m_root;
        [Tooltip("비우면 자동 추가. blocksRaycasts는 강제로 false가 된다")]
        [SerializeField] private CanvasGroup m_canvasGroup;

        [Header("Panels")]
        [Tooltip("종류별 패널. Kind가 겹치면 앞선 것이 쓰인다")]
        [SerializeField] private ItemTooltipPanelBase[] m_panels;

        [Header("Position")]
        [Tooltip("슬롯의 어느 높이를 기준선으로 삼을지")]
        [SerializeField] private TooltipVerticalAlign m_verticalAlign = TooltipVerticalAlign.SlotTop;

        [Tooltip("팝업의 세로 피벗. 1 = 팝업 위쪽을 기준선에 맞춤(아래로 펼쳐짐), " +
                 "0.5 = 팝업 가운데를 맞춤, 0 = 팝업 아래쪽을 맞춤(위로 펼쳐짐)")]
        [SerializeField, Range(0f, 1f)] private float m_pivotY = 1f;

        [Tooltip("x = 슬롯과의 가로 간격(px), y = 세로 미세 조정(+ 위 / - 아래)")]
        [SerializeField] private Vector2 m_offset = new Vector2(8f, 0f);

        [Tooltip("루트 크기를 활성 패널에 맞춘다. 루트에 Content Size Fitter가 없다면 켜 둘 것 — " +
                 "꺼 두면 루트 사각형과 실제 팝업 크기가 달라 위치·화면 밖 보정이 어긋난다")]
        [SerializeField] private bool m_fitRootToPanel = true;

        private RectTransform m_canvasRect;
        private ItemSlotView m_owner;
        private RectTransform m_anchor;
        private ItemTooltipPanelBase m_active;
        private bool m_isVisible;

        // GetWorldCorners는 배열을 채우는 방식이라 매 호출 할당을 피해 재사용한다.
        private readonly Vector3[] m_anchorCorners = new Vector3[4];
        private readonly Vector3[] m_tooltipCorners = new Vector3[4];

        // ── 정적 통로(슬롯이 직접 호출) ──

        private static bool s_warnedMissingInstance;

        public static void Request(ItemSlotView owner, ItemTooltipData data, Sprite icon, RectTransform anchor)
        {
            if (s_instance == null)
            {
                if (s_warnedMissingInstance == false)
                {
                    s_warnedMissingInstance = true;
                    Debug.LogWarning("[ItemTooltipController] 씬에 활성 상태의 컨트롤러가 없습니다 — " +
                                     "Canvas 직속 자식으로 배치하고 GameObject를 활성화하세요(비활성이면 OnEnable이 돌지 않습니다).");
                }
                return;
            }
            s_instance.ShowInternal(owner, data, icon, anchor);
        }

        /// <summary>그 슬롯이 띄운 팝업일 때만 닫는다. 이미 다른 슬롯으로 넘어갔으면 무시한다.</summary>
        public static void Dismiss(ItemSlotView owner)
        {
            if (s_instance != null && s_instance.m_owner == owner)
            {
                s_instance.HideInternal();
            }
        }

        // ── 생명주기 ──

        protected override void OnEnable()
        {
            base.OnEnable();   // UIViewBase 자가 등록(부팅 시 AssetProvider 주입)

            s_instance = this;
            if (m_root == null) m_root = transform as RectTransform;
            if (m_canvas == null) m_canvas = GetComponentInParent<Canvas>();
            if (m_canvas != null) m_canvasRect = m_canvas.transform as RectTransform;

            EnsureCanvasGroup();
            ResolvePanels();   // 프리팹 에셋이 할당돼 있으면 인스턴스로 교체 + 전부 비활성
            HideInternal();
        }

        protected override void OnDisable()
        {
            if (s_instance == this) s_instance = null;
            base.OnDisable();
        }

        // 표시 전용이라 구독할 System이 없다.
        public override void Bind() { }
        public override void Unbind() { }

        private void LateUpdate()
        {
            // 슬롯이 스크롤되거나 창이 재배치되면 팝업도 따라가야 한다.
            if (m_isVisible && m_anchor != null)
            {
                PositionBesideAnchor();
            }
        }

        // ── 표시/숨김 ──

        private void ShowInternal(ItemSlotView owner, ItemTooltipData data, Sprite icon, RectTransform anchor)
        {
            if (data == null || anchor == null || m_root == null)
            {
                HideInternal();
                return;
            }

            ItemTooltipPanelBase panel = FindPanel(data.Kind);
            if (panel == null)
            {
                // 장비·소모품처럼 아직 패널을 안 만든 종류는 정상 동작이지만, 배선 누락과 구분되지 않으므로 알린다.
                Debug.LogWarning($"[ItemTooltipController] '{data.Kind}' 종류의 패널이 없습니다 — " +
                                 "Panels 배열에 해당 Kind의 패널을 할당하세요.", this);
                HideInternal();
                return;
            }

            if (m_active != null && m_active != panel)
            {
                m_active.gameObject.SetActive(false);
            }
            m_active = panel;
            panel.gameObject.SetActive(true);
            panel.Apply(data, icon, AssetProvider);

            m_owner = owner;
            m_anchor = anchor;
            m_isVisible = true;

            if (m_canvasGroup != null) m_canvasGroup.alpha = 1f;
            m_root.SetAsLastSibling();   // 다른 UI 위에 표시

            // 패널마다 높이가 다르므로 위치를 계산하기 전에 레이아웃을 확정한다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_root);
            Canvas.ForceUpdateCanvases();

            FitRootToActivePanel();
            PositionBesideAnchor();
        }

        private void HideInternal()
        {
            m_isVisible = false;
            m_owner = null;
            m_anchor = null;

            if (m_canvasGroup != null) m_canvasGroup.alpha = 0f;
            if (m_active != null)
            {
                m_active.gameObject.SetActive(false);
                m_active = null;
            }
        }

        /// <summary>
        /// 패널 배열을 표시 가능한 상태로 정리한다.
        /// 프리팹 에셋이 할당된 항목은 자식으로 인스턴스화해 배열을 교체하고, 씬 인스턴스는 그대로 쓴다.
        /// 정리 후 모든 패널을 비활성화한다.
        /// </summary>
        private void ResolvePanels()
        {
            if (m_panels == null)
            {
                return;
            }

            Transform parent = m_root != null ? m_root : transform;
            for (int i = 0; i < m_panels.Length; i++)
            {
                ItemTooltipPanelBase panel = m_panels[i];
                if (panel == null) continue;

                if (panel.gameObject.scene.IsValid() == false)   // 씬에 없다 = 프리팹 에셋
                {
                    panel = Instantiate(panel, parent);
                    m_panels[i] = panel;
                }

                panel.gameObject.SetActive(false);
            }
        }

        private ItemTooltipPanelBase FindPanel(TooltipItemKind kind)
        {
            if (m_panels == null)
            {
                return null;
            }
            for (int i = 0; i < m_panels.Length; i++)
            {
                if (m_panels[i] != null && m_panels[i].Kind == kind) return m_panels[i];
            }
            return null;
        }

        private void EnsureCanvasGroup()
        {
            if (m_canvasGroup == null)
            {
                m_canvasGroup = GetComponent<CanvasGroup>();
                if (m_canvasGroup == null) m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 팝업이 마우스를 가로채면 슬롯의 hover가 즉시 풀려 깜빡인다.
            m_canvasGroup.interactable = false;
            m_canvasGroup.blocksRaycasts = false;
        }

        // ── 배치 ──

        private void PositionBesideAnchor()
        {
            if (m_canvasRect == null || m_anchor == null || m_root == null)
            {
                return;
            }

            // 코너 순서: 0=좌하, 1=좌상, 2=우상, 3=우하
            m_anchor.GetWorldCorners(m_anchorCorners);
            Vector2 bottomLeft = m_canvasRect.InverseTransformPoint(m_anchorCorners[0]);
            Vector2 topLeft = m_canvasRect.InverseTransformPoint(m_anchorCorners[1]);
            Vector2 topRight = m_canvasRect.InverseTransformPoint(m_anchorCorners[2]);

            float baseline = m_verticalAlign switch
            {
                TooltipVerticalAlign.SlotMiddle => (topLeft.y + bottomLeft.y) * 0.5f,
                TooltipVerticalAlign.SlotBottom => bottomLeft.y,
                _ => topLeft.y,
            } + m_offset.y;

            // 기본: 슬롯 오른쪽에 붙인다.
            m_root.pivot = new Vector2(0f, m_pivotY);
            SetLocalPosition(new Vector2(topRight.x + m_offset.x, baseline));

            if (IsOverflowingRight())
            {
                // 반대편: 슬롯 왼쪽에 붙인다.
                m_root.pivot = new Vector2(1f, m_pivotY);
                SetLocalPosition(new Vector2(topLeft.x - m_offset.x, baseline));
            }

            ClampInsideCanvas();
        }

        /// <summary>
        /// 루트 사각형을 활성 패널에 맞춘다. 패널을 루트 좌상단에 정렬하고 패널 크기를 루트에 복사한다.
        /// </summary>
        private void FitRootToActivePanel()
        {
            if (m_fitRootToPanel == false || m_active == null || m_root == null)
            {
                return;
            }
            if (m_active.transform is not RectTransform panelRect)
            {
                return;
            }

            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = Vector2.zero;
            m_root.sizeDelta = panelRect.rect.size;
        }

        private void SetLocalPosition(Vector2 canvasLocalPosition)
            => m_root.position = m_canvasRect.TransformPoint(
                new Vector3(canvasLocalPosition.x, canvasLocalPosition.y, 0f));

        private bool IsOverflowingRight()
        {
            m_root.GetWorldCorners(m_tooltipCorners);
            Vector2 topRight = m_canvasRect.InverseTransformPoint(m_tooltipCorners[2]);
            return topRight.x > m_canvasRect.rect.xMax;
        }

        /// <summary>팝업이 캔버스 밖으로 벗어난 만큼 안쪽으로 밀어 넣는다.</summary>
        private void ClampInsideCanvas()
        {
            m_root.GetWorldCorners(m_tooltipCorners);
            Vector2 bottomLeft = m_canvasRect.InverseTransformPoint(m_tooltipCorners[0]);
            Vector2 topRight = m_canvasRect.InverseTransformPoint(m_tooltipCorners[2]);

            Rect bounds = m_canvasRect.rect;
            Vector2 correction = Vector2.zero;

            if (bottomLeft.x < bounds.xMin) correction.x = bounds.xMin - bottomLeft.x;
            else if (topRight.x > bounds.xMax) correction.x = bounds.xMax - topRight.x;

            if (bottomLeft.y < bounds.yMin) correction.y = bounds.yMin - bottomLeft.y;
            else if (topRight.y > bounds.yMax) correction.y = bounds.yMax - topRight.y;

            if (correction == Vector2.zero)
            {
                return;
            }
            m_root.position += m_canvasRect.TransformVector(new Vector3(correction.x, correction.y, 0f));
        }
    }
}
