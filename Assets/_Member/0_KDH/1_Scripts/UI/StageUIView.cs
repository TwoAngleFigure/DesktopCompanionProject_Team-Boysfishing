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
        public Button m_moveButton;

        [Header("현재 설정된 목적지 (실전 동적 연동용)")]
        public int m_targetMapId;

        private readonly StageViewModel m_vm = new();

        private bool m_wasTraveling = false;

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.CurrentStageName.Bind(OnCurrentStageNameChanged);
            m_vm.IsCancelButtonInteractable.Bind(interactable => { if (m_cancelButton != null) m_cancelButton.interactable = interactable; });

            if (m_moveButton != null) m_moveButton.onClick.AddListener(() => m_vm.MoveCommand?.Execute(m_targetMapId));
            if (m_cancelButton != null) m_cancelButton.onClick.AddListener(() => m_vm.CancelCommand?.Execute());

            if (m_timerText != null && !m_vm.IsTraveling)
            {
                m_timerText.text = "대기 중";
                m_wasTraveling = false;
            }
        }

        public override void Unbind()
        {
            if (m_moveButton != null) m_moveButton.onClick.RemoveAllListeners();
            if (m_cancelButton != null) m_cancelButton.onClick.RemoveAllListeners();

            m_vm.CurrentStageName.Unbind(OnCurrentStageNameChanged);
            m_vm.Unbind();
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
                m_timerText.text = m_vm.CurrentStageName.Value.Contains("바다 위") ? "정지됨" : "도착 완료!";
            }

            m_wasTraveling = isCurrentlyTraveling;
        }
    }
}