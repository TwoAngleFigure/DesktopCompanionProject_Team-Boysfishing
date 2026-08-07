using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// TMP_Dropdown에 부착하는 연출 컴포넌트. Arrow 반동 · 목록 펼침/접힘 · 옵션 hover 텍스트 색을 담당한다.
    /// TMP_Dropdown을 교체하지 않고 상태만 관측하므로 기존 참조 코드(SortBarView 등)에 영향이 없다.
    ///
    /// 동작 원리:
    ///  - TMP_Dropdown.Hide()가 virtual이 아니라 닫힘 시점을 직접 후킹할 수 없다. 대신 alphaFadeSpeed를
    ///    신호선으로 쓴다. 닫힌 동안 0으로 두면 Show()의 페이드 인이 즉시 끝나 알파가 항상 1이고,
    ///    열린 동안 접힘 시간으로 올려 두면 Hide()가 목록을 그만큼 살려 두면서 알파를 1 미만으로 떨어뜨린다.
    ///    즉 "열려 있는데 알파 &lt; 1" = 닫힘 시작이 된다.
    ///  - 감지는 LateUpdate에서 한다. EventSystem이 Update에서 Show()/Hide()를 호출하므로 같은 프레임에 잡힌다.
    ///  - 펼침은 목록 루트의 높이만 바꾼다. 목록의 Viewport(ScrollRect 뷰포트)가 함께 줄며 옵션을 잘라내고,
    ///    Content는 크기가 고정이라 옵션이 세로로 눌리지 않는다.
    /// </summary>
    [RequireComponent(typeof(TMP_Dropdown))]
    [DisallowMultipleComponent]
    public class DropdownAnimator : MonoBehaviour
    {
        private enum Phase { Closed, Expanding, Open, Collapsing }

        private const string ListName = "Dropdown List";   // TMP_Dropdown.Show()가 지정하는 고정 이름
        private const float MinFadeSpeed = 0.01f;          // 0이면 Hide()가 목록을 즉시 파괴한다

        [Header("목록 펼침/접힘")]
        [Tooltip("펼치는 데 걸리는 시간(초). timeScale의 영향을 받지 않는다")]
        [SerializeField] private float m_expandDuration = 0.18f;

        [Tooltip("접히는 데 걸리는 시간. 이 값이 TMP_Dropdown.alphaFadeSpeed를 덮어써 목록 파괴 시점을 결정한다")]
        [SerializeField] private float m_collapseDuration = 0.12f;

        [SerializeField] private AnimationCurve m_expandCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve m_collapseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Template에 설정한 높이를 유지한다. TMP_Dropdown.Show()는 옵션 내용이 Template보다 짧으면 " +
                 "남는 높이만큼 목록을 줄이는데(예: 150 → 92), 이 옵션을 켜면 원래 높이로 되돌린다. " +
                 "끄면 TMP 기본 동작대로 내용에 맞춰 목록이 줄어든다")]
        [SerializeField] private bool m_keepTemplateHeight = true;

        [Header("옵션 수에 맞춘 높이")]
        [Tooltip("체크 시 목록 높이를 '옵션 수 × 항목 높이'로 정한다. Template 높이와 Keep Template Height는 무시된다. " +
                 "ContentSizeFitter로는 안 되는 이유: TMP_Dropdown.Show()가 아이템 배치와 Content 크기를 직접 쓰므로 " +
                 "레이아웃 시스템이 개입할 여지가 없다")]
        [SerializeField] private bool m_fitHeightToOptions = false;

        [Tooltip("높이 상한(px). 옵션이 많아 이 값을 넘으면 목록이 스크롤된다. " +
                 "0 이하면 상한 없이 옵션 수만큼 계속 길어진다")]
        [SerializeField] private float m_maxHeight = 0f;

        [Tooltip("항목 높이 합계에 더할 위·아래 여백(px)")]
        [SerializeField] private float m_verticalPadding = 0f;

        [Header("Arrow 반동 — 위로 튀었다가 탄성으로 복귀(회전 없음)")]
        [Tooltip("비우면 자식에서 이름 'Arrow'를 찾는다")]
        [SerializeField] private RectTransform m_arrow;

        [Tooltip("튀어오르는 최대 높이(px)")]
        [SerializeField] private float m_arrowPunchHeight = 6f;

        [SerializeField] private float m_arrowPunchDuration = 0.28f;

        [Tooltip("0에서 시작해 1까지 튀었다가 음수로 반동한 뒤 0으로 수렴하는 곡선")]
        [SerializeField] private AnimationCurve m_arrowPunchCurve = DefaultPunchCurve();

        [Header("옵션 hover 텍스트 색")]
        [SerializeField] private Color m_optionNormalColor = new Color(0.196f, 0.196f, 0.196f, 1f);
        [SerializeField] private Color m_optionHoverColor = new Color(1f, 0.82f, 0.29f, 1f);

        private TMP_Dropdown m_dropdown;
        private RectTransform m_listRect;
        private CanvasGroup m_listGroup;
        private ScrollRect m_listScroll;
        private float m_listHeight;    // Show()가 확정한 자연 높이
        private float m_listTopEdge;   // 부모 기준 상단 모서리. 높이가 변해도 이 값을 고정한다
        private Phase m_phase = Phase.Closed;
        private float m_elapsed;

        private Vector2 m_arrowHome;
        private float m_arrowElapsed = -1f;   // 음수면 재생 중이 아님

        private void Awake()
        {
            m_dropdown = GetComponent<TMP_Dropdown>();

            // 닫힌 동안 0 — Show()의 페이드 인을 즉시 끝내 "알파 < 1"을 닫힘 전용 신호로 만든다.
            m_dropdown.alphaFadeSpeed = 0f;

            if (m_arrow == null)
            {
                m_arrow = transform.Find("Arrow") as RectTransform;
            }
            if (m_arrow != null)
            {
                m_arrowHome = m_arrow.anchoredPosition;
            }

            InstallOptionTint();
        }

        private void OnDisable()
        {
            // 창이 닫히는 등으로 비활성화되면 목록은 TMP가 정리한다. 우리 상태만 되돌린다.
            m_listRect = null;
            m_listGroup = null;
            m_listScroll = null;
            m_phase = Phase.Closed;
            m_arrowElapsed = -1f;

            if (m_arrow != null)
            {
                m_arrow.anchoredPosition = m_arrowHome;
            }
            if (m_dropdown != null)
            {
                m_dropdown.alphaFadeSpeed = 0f;
            }
        }

        /// <summary>
        /// 원본 Template의 아이템에 hover 틴트를 심는다.
        /// TMP_Dropdown이 이 아이템을 복제해 옵션을 만들므로 모든 옵션이 컴포넌트와 색을 물려받는다.
        /// </summary>
        private void InstallOptionTint()
        {
            RectTransform template = m_dropdown.template;
            if (template == null)
            {
                return;
            }

            // TMP_Dropdown.SetupTemplate과 같은 방식으로 아이템을 찾는다.
            Toggle item = template.GetComponentInChildren<Toggle>(true);
            if (item == null)
            {
                Debug.LogWarning("[DropdownAnimator] Template에서 옵션 Toggle을 찾지 못했습니다.", this);
                return;
            }

            DropdownOptionTint tint = item.GetComponent<DropdownOptionTint>();
            if (tint == null)
            {
                tint = item.gameObject.AddComponent<DropdownOptionTint>();
            }
            tint.SetColors(m_optionNormalColor, m_optionHoverColor);
        }

        // EventSystem이 Update에서 Show()/Hide()를 호출하므로 LateUpdate여야 같은 프레임에 잡힌다.
        // Update로 하면 목록이 완성 크기로 한 프레임 번쩍인다.
        private void LateUpdate()
        {
            bool expanded = m_dropdown.IsExpanded;

            if (expanded && m_listRect == null)
            {
                if (TryCaptureList())
                {
                    BeginExpand();
                    PunchArrow();
                }
            }
            else if (expanded == false && m_listRect != null)
            {
                ReleaseList();
            }

            if (m_listRect != null)
            {
                // Hide()가 시작한 알파 트윈이 닫힘을 알 수 있는 유일한 신호다.
                if (m_phase == Phase.Open && m_listGroup != null && m_listGroup.alpha < 0.999f)
                {
                    BeginCollapse();
                    PunchArrow();
                }

                TickList();

                if (m_listGroup != null)
                {
                    m_listGroup.alpha = 1f;   // 페이드는 쓰지 않는다. 내장 트윈을 매 프레임 무효화한다
                }
            }

            TickArrow();
        }

        private bool TryCaptureList()
        {
            Transform parent = m_dropdown.template != null ? m_dropdown.template.parent : null;
            if (parent == null)
            {
                return false;
            }

            Transform list = parent.Find(ListName);
            if (list == null)
            {
                return false;
            }

            m_listRect = list as RectTransform;
            m_listGroup = list.GetComponent<CanvasGroup>();

            // Show()가 남긴 현재 높이로 상단 모서리를 먼저 기록한다. 축소는 하단만 끌어올리므로 상단은 그대로다.
            float shownHeight = m_listRect.rect.height;
            m_listTopEdge = m_listRect.anchoredPosition.y + (1f - m_listRect.pivot.y) * shownHeight;

            m_listHeight = ResolveTargetHeight(shownHeight);
            SetListHeight(m_listHeight);   // 목표 높이로 먼저 세워 둔다 → 아래 레이아웃·스크롤 판정이 최종 상태 기준이 된다

            LayoutRebuilder.ForceRebuildLayoutImmediate(m_listRect);
            FreezeScroll();

            // 열린 동안만 파괴 지연 시간을 준다 → Hide()가 접힘 시간만큼 목록을 살려 둔다.
            m_dropdown.alphaFadeSpeed = Mathf.Max(m_collapseDuration, MinFadeSpeed);
            return true;
        }

        /// <summary>
        /// 펼쳤을 때의 목표 높이를 정한다.
        /// TMP_Dropdown.Show()는 옵션 내용이 Template보다 짧으면 그 차이만큼 목록을 줄인다
        /// (extraSpace &gt; 0일 때 sizeDelta.y -= extraSpace). Show()가 virtual이 아니라 막을 수 없으므로
        /// 여기서 Template 원본 높이로 되돌린다. 내용이 더 길면 Show()가 줄이지 않으므로 값이 같다.
        /// </summary>
        private float ResolveTargetHeight(float shownHeight)
        {
            if (m_fitHeightToOptions)
            {
                return FitHeightToOptions(shownHeight);
            }
            if (m_keepTemplateHeight == false || m_dropdown.template == null)
            {
                return shownHeight;
            }
            return Mathf.Max(shownHeight, m_dropdown.template.rect.height);
        }

        /// <summary>
        /// 옵션 수 × 항목 높이로 목표 높이를 구한다. 상한을 넘으면 상한에서 멈추고 목록이 스크롤된다.
        ///
        /// Show()가 계산한 Content 높이(itemSize.y * count + offsetMin.y - offsetMax.y)를 쓰지 않는다.
        /// 그 offset은 Template의 Content rect와 Item rect의 차이라 프리팹 설정에 좌우되며,
        /// Content 높이가 Item 높이보다 작으면 음수가 되어 목록이 그만큼 짧아진다.
        /// 항목 높이에서 직접 구하면 그 설정 실수와 무관하게 같은 결과가 나온다.
        /// ※ 단, 아이템 배치 자체는 Show()가 정하므로 Content 높이는 Item 높이와 맞춰 둬야 정렬이 맞다.
        /// </summary>
        private float FitHeightToOptions(float shownHeight)
        {
            float itemHeight = ResolveItemHeight();
            if (itemHeight <= 0f)
            {
                Debug.LogWarning("[DropdownAnimator] Template에서 항목 높이를 얻지 못해 옵션 수 맞춤을 건너뜁니다.", this);
                return shownHeight;
            }

            int count = m_dropdown.options != null ? m_dropdown.options.Count : 0;
            float desired = itemHeight * Mathf.Max(1, count) + m_verticalPadding;

            // 상한을 지정하지 않았으면 자르지 않는다. Template 높이를 상한으로 되돌려 쓰면
            // 옵션 수 맞춤이라는 전제 자체가 무너져 옵션이 많을 때 목록이 잘린다.
            return m_maxHeight > 0f ? Mathf.Min(desired, m_maxHeight) : desired;
        }

        /// <summary>
        /// 항목 하나의 높이. TMP_Dropdown이 itemSize로 쓰는 값과 같은 출처(Template의 Item rect)다.
        /// </summary>
        private float ResolveItemHeight()
        {
            RectTransform template = m_dropdown.template;
            if (template == null)
            {
                return 0f;
            }

            // TMP_Dropdown.SetupTemplate과 같은 방식으로 아이템을 찾는다.
            Toggle item = template.GetComponentInChildren<Toggle>(true);
            return item != null && item.transform is RectTransform rect ? rect.rect.height : 0f;
        }

        private void ReleaseList()
        {
            m_listRect = null;
            m_listGroup = null;
            m_listScroll = null;
            m_phase = Phase.Closed;
            m_dropdown.alphaFadeSpeed = 0f;
        }

        /// <summary>
        /// 애니메이션 동안 ScrollRect의 재레이아웃을 막는다.
        /// Scrollbar Visibility가 Auto Hide And Expand Viewport면 목록 높이가 변할 때마다
        /// 스크롤 필요 여부가 뒤집혀 뷰포트 폭이 튄다. 최종 높이 기준으로 한 번 확정한 뒤 얼려 둔다.
        /// </summary>
        private void FreezeScroll()
        {
            m_listScroll = m_listRect.GetComponent<ScrollRect>();
            if (m_listScroll == null)
            {
                return;
            }

            RectTransform viewport = m_listScroll.viewport;
            RectTransform content = m_listScroll.content;
            Scrollbar bar = m_listScroll.verticalScrollbar;

            if (bar != null && viewport != null && content != null)
            {
                bool needed = m_listScroll.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.Permanent
                              || content.rect.height > viewport.rect.height + 0.01f;

                if (bar.gameObject.activeSelf != needed)
                {
                    bar.gameObject.SetActive(needed);
                }
            }

            SetScrollEnabled(false);
        }

        private void SetScrollEnabled(bool on)
        {
            if (m_listScroll != null)
            {
                m_listScroll.enabled = on;
            }
        }

        private void BeginExpand()
        {
            m_phase = Phase.Expanding;
            m_elapsed = 0f;
            SetListHeight(0f);
        }

        private void BeginCollapse()
        {
            m_phase = Phase.Collapsing;
            m_elapsed = 0f;
            SetScrollEnabled(false);
        }

        private void TickList()
        {
            if (m_phase == Phase.Expanding)
            {
                m_elapsed += Time.unscaledDeltaTime;
                float t = m_expandDuration <= 0f ? 1f : Mathf.Clamp01(m_elapsed / m_expandDuration);
                SetListHeight(m_listHeight * m_expandCurve.Evaluate(t));

                if (t >= 1f)
                {
                    m_phase = Phase.Open;
                    SetScrollEnabled(true);   // 다 펼쳐진 뒤에 스크롤을 되살린다
                }
            }
            else if (m_phase == Phase.Collapsing)
            {
                m_elapsed += Time.unscaledDeltaTime;
                float t = m_collapseDuration <= 0f ? 1f : Mathf.Clamp01(m_elapsed / m_collapseDuration);
                SetListHeight(m_listHeight * (1f - m_collapseCurve.Evaluate(t)));
                // 높이가 0에 닿는 시점에 TMP가 목록을 파괴한다(alphaFadeSpeed = m_collapseDuration).
            }
        }

        /// <summary>상단 모서리를 고정한 채 목록의 높이만 바꾼다.</summary>
        private void SetListHeight(float height)
        {
            m_listRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            Vector2 pos = m_listRect.anchoredPosition;
            pos.y = m_listTopEdge - (1f - m_listRect.pivot.y) * height;
            m_listRect.anchoredPosition = pos;
        }

        // ── Arrow ──

        private void PunchArrow()
        {
            if (m_arrow == null)
            {
                return;
            }
            m_arrowElapsed = 0f;
        }

        private void TickArrow()
        {
            if (m_arrow == null || m_arrowElapsed < 0f)
            {
                return;
            }

            m_arrowElapsed += Time.unscaledDeltaTime;
            float t = m_arrowPunchDuration <= 0f ? 1f : Mathf.Clamp01(m_arrowElapsed / m_arrowPunchDuration);

            Vector2 pos = m_arrowHome;
            pos.y += m_arrowPunchCurve.Evaluate(t) * m_arrowPunchHeight;
            m_arrow.anchoredPosition = pos;

            if (t >= 1f)
            {
                m_arrow.anchoredPosition = m_arrowHome;
                m_arrowElapsed = -1f;
            }
        }

        /// <summary>위로 튀었다가 아래로 반동한 뒤 수렴하는 기본 탄성 곡선.</summary>
        private static AnimationCurve DefaultPunchCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.28f, 1f),
                new Keyframe(0.58f, -0.30f),
                new Keyframe(0.82f, 0.10f),
                new Keyframe(1f, 0f));
        }
    }
}
