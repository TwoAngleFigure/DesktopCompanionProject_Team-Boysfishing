using System.Text;
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
    ///
    /// 소속 창도 UITabWindow가 <see cref="Attach"/>로 알려 준다 — 버튼이 계층을 거슬러 올라가 찾으면
    /// 탭 창이 중첩된 곳에서 자기 창이 아니라 더 가까운 바깥 창을 잡는다.
    /// 탭 목록을 가진 쪽은 창이므로, 찾는 방향도 창 → 버튼이어야 한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class TabBookmarkButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("소속 탭 창. 비워 두는 것이 정상 — UITabWindow가 Bind에서 자기 탭 버튼에게 직접 알려 준다. " +
                 "그 창이 이 버튼을 Tabs 목록에 갖고 있지 않은 특수한 경우에만 손으로 지정한다")]
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
        private UITabWindow m_owner;   // 실제 소유 창. Attach로 주입되거나 인스펙터 지정으로 정해진다
        private int m_index = -1;
        private bool m_subscribed;
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

        /// <summary>
        /// 소유 탭 창이 '너는 내 몇 번째 탭 버튼이다'라고 알려 준다. <see cref="UITabWindow.Bind"/>에서 부른다.
        /// 버튼이 아직 비활성이면 기억만 해 두고, 활성화될 때 <see cref="OnEnable"/>이 구독·동기화한다.
        /// </summary>
        public void Attach(UITabWindow window, int index)
        {
            if (window == null || index < 0)
            {
                return;
            }

            Unsubscribe();   // 다른 창에 붙어 있었다면 먼저 뗀다
            m_owner = window;
            m_index = index;

            // m_rect가 아직 null이면 Awake 전이다 — 소유 창의 Bind가 이 버튼의 Awake보다 먼저 도는 경우.
            // 그때는 붙였다는 사실만 남기고 넘어간다. 곧 이어질 OnEnable이 구독·동기화를 맡는다.
            if (m_rect != null && isActiveAndEnabled)
            {
                SubscribeAndSync();
            }
        }

        /// <summary>소유 탭 창이 Unbind될 때 연결을 끊는다. 주인이 아닌 창의 호출은 무시한다.</summary>
        public void Detach(UITabWindow window)
        {
            if (m_owner != window)
            {
                return;
            }

            Unsubscribe();
            m_owner = null;
            m_index = -1;
        }

        private void OnEnable()
        {
            // 소유 창이 아직 Bind되지 않아 Attach를 못 받았을 수 있다.
            // 인스펙터에 창이 지정돼 있으면 그것으로 스스로 붙는다(특수 배치용 탈출구).
            if (m_owner == null && m_tabWindow != null)
            {
                int index = m_tabWindow.IndexOf(m_button);
                if (index >= 0)
                {
                    m_owner = m_tabWindow;
                    m_index = index;
                }
            }

            if (m_owner != null)
            {
                SubscribeAndSync();
            }

            // 여기서 경고하지 않는다 — 소유 창의 Bind가 아직 안 돌았을 수 있다. 판정은 Start에서 한다.
        }

        /// <summary>
        /// 이 활성화 묶음의 Bind가 모두 끝난 뒤에도 주인이 없으면 그때는 진짜 설정 누락이다.
        /// OnEnable에서 곧바로 경고하면 '아직 Bind 전'인 정상 상태까지 잡아 버린다.
        /// </summary>
        private void Start()
        {
            if (m_owner != null)
            {
                return;
            }

            Debug.LogWarning(
                $"[TabBookmarkButton] 소속 탭 창을 찾지 못했습니다 — 버튼 '{HierarchyPath(this)}'. " +
                "이 버튼을 쓰는 UITabWindow의 Tabs 목록에 Button으로 등록됐는지 확인할 것" +
                (m_tabWindow != null
                    ? $" (인스펙터 지정 창 '{HierarchyPath(m_tabWindow)}'의 탭 {m_tabWindow.TabCount}개 중에는 없다)."
                    : "."), this);
        }

        private void OnDisable()
        {
            Unsubscribe();

            m_elapsed = -1f;
            SetWidth(m_baseWidth);   // OnPointerExit이 오지 않으므로, 늘어난 채로 남지 않게 되돌린다
        }

        private void SubscribeAndSync()
        {
            if (m_subscribed == false)
            {
                m_owner.SelectedChanged += HandleSelectedChanged;
                m_subscribed = true;
            }

            // 초기 ApplyTab을 놓쳤을 수 있으므로 구독 순서에 기대지 않고 지금 값으로 한 번 맞춘다.
            // 창을 열 때 폭이 자라나 보이지 않도록 보간 없이 적용한다.
            ApplySelected(m_owner.SelectedIndex == m_index, true);
        }

        private void Unsubscribe()
        {
            if (m_subscribed && m_owner != null)
            {
                m_owner.SelectedChanged -= HandleSelectedChanged;
            }
            m_subscribed = false;
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

        /// <summary>
        /// 씬 계층 경로. 탭 버튼은 창마다 같은 이름을 쓰는 일이 흔해 이름만으로는 어느 것인지 가려지지 않고,
        /// 이 경고는 창이 열리는 도중에 나와 콘솔의 ping 대상이 곧 사라질 수도 있다.
        /// </summary>
        private static string HierarchyPath(Component component)
        {
            if (component == null)
            {
                return "(없음)";
            }

            var builder = new StringBuilder(component.name);
            for (Transform parent = component.transform.parent; parent != null; parent = parent.parent)
            {
                builder.Insert(0, '/').Insert(0, parent.name);
            }
            return builder.ToString();
        }
    }
}
