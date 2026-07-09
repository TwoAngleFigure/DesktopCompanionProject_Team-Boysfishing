using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class StageUIView : UIViewBase
    {
        [Header("UI 연결")]
        [SerializeField] private TextMeshProUGUI m_currentStageText;
        [SerializeField] private TextMeshProUGUI m_timerText;
        
        [Header("이동 버튼들")]
        [SerializeField] private Button m_btnToBusan;
        [SerializeField] private Button m_btnToPohang;
        [SerializeField] private Button m_btnToTempMap;
        
        [Header("기타 버튼")]
        [SerializeField] private Button m_cancelButton; 

        [Header("임시 맵 ID 설정")]
        [SerializeField] private int m_tempMapId = 600003;

        private readonly StageViewModel m_vm = new();

        public override void Bind()
        {
            m_vm.Inject(SystemManager);
            m_vm.Bind();

            m_vm.CurrentStageName.Bind(OnCurrentStageNameChanged);
            m_vm.IsCancelButtonInteractable.Bind(interactable => { if (m_cancelButton != null) m_cancelButton.interactable = interactable; });

            if (m_btnToBusan != null) m_btnToBusan.onClick.AddListener(() => m_vm.MoveCommand?.Execute(600001));
            if (m_btnToPohang != null) m_btnToPohang.onClick.AddListener(() => m_vm.MoveCommand?.Execute(600002));
            if (m_btnToTempMap != null) m_btnToTempMap.onClick.AddListener(() => m_vm.MoveCommand?.Execute(m_tempMapId));

            if (m_cancelButton != null) m_cancelButton.onClick.AddListener(() => m_vm.CancelCommand?.Execute());

            if (m_timerText != null && !m_vm.IsTraveling) m_timerText.text = "Waiting";
        }

        public override void Unbind()
        {
            if (m_btnToBusan != null) m_btnToBusan.onClick.RemoveAllListeners();
            if (m_btnToPohang != null) m_btnToPohang.onClick.RemoveAllListeners();
            if (m_btnToTempMap != null) m_btnToTempMap.onClick.RemoveAllListeners();
            if (m_cancelButton != null) m_cancelButton.onClick.RemoveAllListeners();

            m_vm.CurrentStageName.Unbind(OnCurrentStageNameChanged);
            m_vm.Unbind();
        }


        private void OnCurrentStageNameChanged(string newName)
        {
            if (m_currentStageText != null) m_currentStageText.text = newName;

            if (m_timerText != null && !m_vm.IsTraveling)
            {
                m_timerText.text = newName.Contains("above the sea") ? "Stop" : "Arrived!";
            }
        }


        private void Update()
        {
            if (m_vm != null && m_vm.IsTraveling && m_timerText != null)
            {
                m_timerText.text = $"On the travel... Remaining time: {m_vm.RemainingTravelTime:F1}s";
            }
        }
    }
}