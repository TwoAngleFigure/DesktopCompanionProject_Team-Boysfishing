using UnityEngine;
using DesktopCompanion.Systems;
using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    public class StageWorldView : UIViewBase
    {
        private readonly StageViewModel m_vm = new();

        private GameObject m_currentStageInstance;
        private string m_lastLoadedAssetKey = "";

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();
        }

        public override void Unbind()
        {
            m_vm.Unbind();
            ClearCurrentStage();
        }

        private void Update()
        {
            if (m_vm == null) return;

            if (m_vm.CurrentStageData == null) return;

            string currentStageKey = AssetKeys.Of(m_vm.CurrentStageData, AssetUsage.Model);

            if (m_lastLoadedAssetKey != currentStageKey)
            {
                LoadStage(currentStageKey);
            }
        }

        private void LoadStage(string stageAssetKey)
        {
            ClearCurrentStage();
            m_lastLoadedAssetKey = stageAssetKey;

            if (AssetProvider != null && AssetProvider.TryGet(stageAssetKey, out GameObject stagePrefab))
            {
                m_currentStageInstance = Instantiate(stagePrefab, Vector3.zero, Quaternion.identity, transform);

                StageBlueprint blueprint = m_currentStageInstance.GetComponent<StageBlueprint>();
                if (blueprint != null)
                {
                    blueprint.InitProvider(AssetProvider);
                    Debug.Log($"[StageWorldView] {stageAssetKey} 맵 로드 완료 및 권한 주입 성공!");
                }
                else
                {
                    Debug.LogWarning($"[StageWorldView] {stageAssetKey} 프리팹에 StageBlueprint가 없습니다!");
                }
            }
        }

        private void ClearCurrentStage()
        {
            if (m_currentStageInstance != null)
            {
                Destroy(m_currentStageInstance);
                m_currentStageInstance = null;
            }
            m_lastLoadedAssetKey = "";
        }
    }
}