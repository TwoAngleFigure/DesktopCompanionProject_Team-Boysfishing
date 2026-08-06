using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class StageUIView : UIViewBase
    {
        [Header("HUD 텍스트 UI")]
        [SerializeField] private TextMeshProUGUI m_currentStageText;
        [SerializeField] private TextMeshProUGUI m_timerText;

        [Header("명령 버튼 UI")]
        [SerializeField] private Button m_cancelButton;

        [Header("월드맵 제어 UI")]
        public Button m_openMapButton;
        public GameObject m_worldMapPanel;

        private readonly StageViewModel m_vm = new();
        private bool m_wasTraveling = false;

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.CurrentStageName.Bind(OnCurrentStageNameChanged);
            m_vm.IsCancelButtonInteractable.Bind(interactable => { if (m_cancelButton != null) m_cancelButton.interactable = interactable; });

            if (m_cancelButton != null)
            {
                // 궁극의 타겟팅 리셋: 유니티 인스펙터에 숨어있던 모든 악성 닫기 바인딩을 영구 무력화!
                m_cancelButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                m_cancelButton.onClick.AddListener(() =>
                {
                    bool wasMapActive = m_worldMapPanel != null && m_worldMapPanel.activeSelf;
                    m_vm.CancelCommand?.Execute();
                    if (wasMapActive && m_worldMapPanel != null)
                    {
                        OpenWorldMap();
                    }
                });
            }

            if (m_openMapButton != null)
            {
                m_openMapButton.onClick.AddListener(OpenWorldMap);
            }

            if (m_timerText != null && !m_vm.IsTraveling)
            {
                m_timerText.text = "대기 중";
                m_wasTraveling = false;
            }
        }

        public override void Unbind()
        {
            if (m_cancelButton != null) m_cancelButton.onClick.RemoveAllListeners();

            if (m_openMapButton != null) m_openMapButton.onClick.RemoveAllListeners();

            m_vm.CurrentStageName.Unbind(OnCurrentStageNameChanged);
            m_vm.Unbind();
        }

        public void OpenWorldMap()
        {
            if (m_worldMapPanel != null)
            {
                var mapWindow = m_worldMapPanel.GetComponent<WorldMapUIView>();
                if (mapWindow != null)
                {
                    mapWindow.OpenByPlayer();
                }
                else
                {
                    m_worldMapPanel.SetActive(true);
                    var window = m_worldMapPanel.GetComponent<UIWindowBase>();
                    if (window != null) window.Show();
                }
            }
        }

        private void OnCurrentStageNameChanged(string newName)
        {
            if (m_currentStageText != null) m_currentStageText.text = newName;
        }

        private void Update()
        {
            if (m_vm == null || m_timerText == null) return;

            bool isCurrentlyTraveling = m_vm.IsTraveling;

            if (isCurrentlyTraveling)
            {
                m_timerText.text = $"이동 중... 남은 시간: {m_vm.RemainingTravelTime:F1}초";
            }
            else if (m_wasTraveling)
            {
                // 중단 버튼 클릭에 의한 정박 시 "항해 중단", 목적지 정상 도착 시 "도착 완료!" 출력!
                m_timerText.text = m_vm.IsCanceled ? "항해 중단" : "도착 완료!";
            }

            m_wasTraveling = isCurrentlyTraveling;
        }
    }
}