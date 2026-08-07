using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// UITabWindow의 탭 전환 버튼에 붙이는 책갈피 연출.
    /// hover하면 Width가 목표값까지 늘어나고, 선택되면 오버레이 이미지를 켜서 버튼을 가린다.
    ///
    /// 선택 상태는 UITabWindow가 소유하고 이 컴포넌트는 표시만 한다.
    /// 그래서 Toggle이 아니라 Button을 그대로 쓴다(계획서 36 참조).
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class TabBookmarkButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("소속 탭 창. 비우면 부모에서 찾는다")]
        [SerializeField] private UITabWindow m_tabWindow;

        [Tooltip("선택됐을 때 켜지는 오버레이 이미지 오브젝트")]
        [SerializeField] private GameObject m_selectedOverlay;

        [Header("Hover Width")]
        [Tooltip("hover 시 목표 Width(px). 선택된 탭도 이 폭을 유지한다. " +
                 "기본 Width는 씬에 설정한 값을 Awake에서 읽는다")]
        [SerializeField] private float m_hoverWidth = 120f;

        [SerializeField] private float m_hoverDuration = 0.12f;
        [SerializeField] private AnimationCurve m_hoverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField] private float m_restoreDuration = 0.10f;
        [SerializeField] private AnimationCurve m_restoreCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private RectTransform m_rect;
        private Button m_button;
        private int m_index = -1;
        private float m_baseWidth;
        private bool m_isSelected;

        // 진행 중인 Width 보간
        private float m_fromWidth;
        private float m_toWidth;
        private float m_duration;
        private float m_elapsed = -1f;   // 음수면 보간 중이 아님
        private AnimationCurve m_curve;

        private void Awake()
        {
            m_rect = transform as RectTransform;
            m_button = GetComponent<Button>();
            m_baseWidth = m_rect.rect.width;   // 씬에 설정한 값이 곧 기본 Width다
        }

        private void OnEnable()
        {
            if (m_tabWindow == null)
            {
                m_tabWindow = GetComponentInParent<UITabWindow>(true);
            }

            if (m_tabWindow == null)
            {
                Debug.LogWarning("[TabBookmarkButton] 소속 UITabWindow를 찾지 못했습니다.", this);
                return;
            }

            m_index = m_tabWindow.IndexOf(m_button);
            if (m_index < 0)
            {
                Debug.LogWarning("[TabBookmarkButton] 이 버튼이 UITabWindow의 탭 목록에 없습니다.", this);
                return;
            }

            m_tabWindow.SelectedChanged += HandleSelectedChanged;

            // Bind()의 초기 ApplyTab을 놓쳤을 수 있으므로 구독 순서에 기대지 않고 지금 값으로 한 번 맞춘다.
            // 창을 열 때 폭이 자라나 보이지 않도록 보간 없이 적용한다.
            ApplySelected(m_tabWindow.SelectedIndex == m_index, true);
        }

        private void OnDisable()
        {
            if (m_tabWindow != null)
            {
                m_tabWindow.SelectedChanged -= HandleSelectedChanged;
            }

            m_elapsed = -1f;
            SetWidth(m_baseWidth);   // OnPointerExit이 오지 않으므로, 늘어난 채로 남지 않게 되돌린다
        }

        private void HandleSelectedChanged(int selectedIndex) => ApplySelected(selectedIndex == m_index, false);

        /// <param name="instant">보간 없이 즉시 적용할지. 활성화 직후 초기 동기화에 쓴다.</param>
        private void ApplySelected(bool selected, bool instant)
        {
            m_isSelected = selected;

            if (m_selectedOverlay != null)
            {
                m_selectedOverlay.SetActive(selected);
            }

            // 선택된 탭은 늘어난 폭을 그대로 유지하고, 다른 탭이 선택되면 원래 폭으로 되돌아간다.
            // 폭을 여기서 정하지 않으면 hover로 늘어난 탭이 선택 해제 후에도 늘어난 채 남는다.
            float target = selected ? m_hoverWidth : m_baseWidth;

            if (instant)
            {
                m_elapsed = -1f;
                SetWidth(target);
                return;
            }

            BeginWidth(target,
                       selected ? m_hoverDuration : m_restoreDuration,
                       selected ? m_hoverCurve : m_restoreCurve);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (m_isSelected)
            {
                return;   // 선택된 탭은 오버레이에 가려져 있다. hover에 반응하지 않는다
            }
            BeginWidth(m_hoverWidth, m_hoverDuration, m_hoverCurve);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (m_isSelected)
            {
                return;
            }
            BeginWidth(m_baseWidth, m_restoreDuration, m_restoreCurve);
        }

        private void BeginWidth(float target, float duration, AnimationCurve curve)
        {
            m_fromWidth = m_rect.rect.width;   // 진행 중이던 보간의 현재 위치에서 이어 간다
            m_toWidth = target;
            m_duration = duration;
            m_curve = curve;
            m_elapsed = 0f;
        }

        private void Update()
        {
            if (m_elapsed < 0f)
            {
                return;
            }

            m_elapsed += Time.unscaledDeltaTime;
            float t = m_duration <= 0f ? 1f : Mathf.Clamp01(m_elapsed / m_duration);
            SetWidth(Mathf.LerpUnclamped(m_fromWidth, m_toWidth, m_curve.Evaluate(t)));

            if (t >= 1f)
            {
                m_elapsed = -1f;
            }
        }

        private void SetWidth(float width)
        {
            m_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
    }
}
