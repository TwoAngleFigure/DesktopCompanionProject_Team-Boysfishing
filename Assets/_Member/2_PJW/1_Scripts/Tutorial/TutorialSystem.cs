using System;
using System.Collections.Generic;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 튜토리얼 도움말의 발동 판정을 담당한다. 문구는 갖지 않고 스텝만 방송한다(표시는 View 책임).
    /// 첫 스테이지에서만 활성이며, 이탈해 다른 스테이지에 도착하면 종료되어 다시 발동하지 않는다.
    /// 행동을 강제하지 않고, 게임 흐름에 개입하는 지점은 재료 보정 지급 하나뿐이다.
    /// </summary>
    public class TutorialSystem : SystemBase, ISaveable
    {
        private const int TutorialStageId = 600001;   // 튜토리얼이 진행되는 첫 스테이지
        private const int GrantMaterialCount = 1;     // 재료 보정 지급 수량

        private readonly HashSet<TutorialStep> m_shownSteps = new();
        private bool m_completed;

        private StageSystem m_stageSystem;
        private FishingSystem m_fishingSystem;
        private InventorySystem m_inventorySystem;

        /// <summary>도움말을 띄워야 하는 스텝. TutorialPopupView가 구독한다.</summary>
        public event Action<TutorialStep> OnStepTriggered;

        /// <summary>
        /// 트리거를 받을 수 있는 상태인지. 종료 전이고 튜토리얼 스테이지에 정박 중일 때만 참이다.
        /// 항해 중에는 StageSystem이 현재 스테이지를 비우므로 자연히 거짓이 된다.
        /// </summary>
        public bool IsActive =>
            m_completed == false &&
            m_stageSystem != null &&
            m_stageSystem.CurrentStageData != null &&
            m_stageSystem.CurrentStageData.ID == TutorialStageId;

        // ── 생명주기 ──

        public override void Initialize()
        {
            m_shownSteps.Clear();
            m_completed = false;   // 원시 기본값. 저장이 있으면 RestoreState가 덮어쓴다.
        }

        public override void PostInitialize()
        {
            m_stageSystem = SystemManager.GetSystem<StageSystem>();
            m_fishingSystem = SystemManager.GetSystem<FishingSystem>();
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();

            if (m_stageSystem != null)
            {
                m_stageSystem.OnStageChanged += HandleStageChanged;
            }
            else
            {
                Debug.LogWarning("[TutorialSystem] StageSystem 미발견 — 튜토리얼이 활성화되지 않는다.");
            }

            if (m_fishingSystem != null)
            {
                m_fishingSystem.OnStateChanged += HandleFishingStateChanged;
                m_fishingSystem.OnFishingResult += HandleFishingResult;
                m_fishingSystem.OnItemDropped += HandleItemDropped;
            }
            else
            {
                Debug.LogWarning("[TutorialSystem] FishingSystem 미발견 — 낚시 도움말이 뜨지 않는다.");
            }
        }

        // ── 트리거 ──

        /// <summary>
        /// 스텝을 1회만 방송한다. 비활성이거나 이미 표시한 스텝이면 아무 일도 하지 않는다.
        /// View 계층의 트리거(창 열림 등)도 이 진입점을 쓴다.
        /// </summary>
        public bool TryTrigger(TutorialStep step)
        {
            if (step == TutorialStep.None || IsActive == false)
            {
                return false;
            }
            if (m_shownSteps.Add(step) == false)
            {
                return false;   // 이미 표시한 스텝
            }

            Debug.Log($"[TutorialSystem] 도움말 발동: {step}");
            OnStepTriggered?.Invoke(step);
            return true;
        }

        /// <summary>해당 스텝을 이미 표시했는지.</summary>
        public bool IsShown(TutorialStep step) => m_shownSteps.Contains(step);

        // ── 이벤트 핸들러 ──

        private void HandleFishingStateChanged(FishingState state)
        {
            switch (state)
            {
                case FishingState.Waiting:
                    TryTrigger(TutorialStep.FishingStart);
                    break;

                case FishingState.Battling:
                    TryTrigger(TutorialStep.Battle);
                    break;
            }
        }

        private void HandleItemDropped(FishingGrantedDropInfo drop)
        {
            if (drop.IsValid == false || drop.ItemType != ItemType.Materials)
            {
                return;
            }

            TryTrigger(TutorialStep.MaterialAcquired);
        }

        private void HandleFishingResult(FishingResultType result)
        {
            if (result != FishingResultType.Success)
            {
                return;
            }

            TryTrigger(TutorialStep.CatchSuccess);
            TryGrantTutorialMaterial();
        }

        private void HandleStageChanged(int stageDataId)
        {
            if (m_completed || stageDataId == TutorialStageId)
            {
                return;
            }

            m_completed = true;
            Debug.Log($"[TutorialSystem] 튜토리얼 스테이지 이탈 — 종료한다. stageId={stageDataId}");
        }

        // ── 재료 보정 지급 ──

        /// <summary>
        /// 드랍은 확률이라 첫 포획에서 재료가 안 나올 수 있다. 조합 안내까지 도달시키기 위해
        /// 아직 재료를 못 받았을 때만 한 번 지급한다(드랍이 이미 떴으면 지급하지 않는다).
        /// </summary>
        private void TryGrantTutorialMaterial()
        {
            if (IsActive == false || IsShown(TutorialStep.MaterialAcquired))
            {
                return;
            }
            if (m_inventorySystem == null)
            {
                return;
            }

            int materialId = ResolveTutorialMaterialId();
            if (materialId <= 0)
            {
                Debug.LogWarning("[TutorialSystem] 튜토리얼 스테이지의 드랍 목록에서 재료를 찾지 못했다.");
                return;
            }
            if (m_inventorySystem.GetTotalQuantityByDataId(ItemType.Materials, materialId) > 0)
            {
                return;   // 이미 보유 중이면 지급하지 않는다
            }

            if (GrantMaterial(materialId, GrantMaterialCount))
            {
                TryTrigger(TutorialStep.MaterialAcquired);
            }
        }

        /// <summary>
        /// 튜토리얼 스테이지에 등장하는 물고기들의 드랍 목록에서 첫 재료를 찾는다.
        /// id를 상수로 두지 않아 밸런스 데이터가 바뀌어도 따라간다.
        /// </summary>
        private int ResolveTutorialMaterialId()
        {
            StageData stageData = DataManager.GetData<StageData>(TutorialStageId);
            if (stageData == null || stageData.TierPools == null)
            {
                return 0;
            }

            foreach (TierPool pool in stageData.TierPools)
            {
                if (pool == null || pool.Entries == null)
                {
                    continue;
                }

                foreach (FishPoolEntry entry in pool.Entries)
                {
                    ItemDrop[] drops = entry?.Fish?.Drops;
                    if (drops == null)
                    {
                        continue;
                    }

                    foreach (ItemDrop drop in drops)
                    {
                        if (drop != null && drop.Item is ItemData_Materials)
                        {
                            return drop.Item.ID;
                        }
                    }
                }
            }

            return 0;
        }

        private bool GrantMaterial(int materialId, int count)
        {
            EntityHandle handle = EntityManager.Create<ItemData_Materials>(materialId);
            Entity_Materials entity = EntityManager.Get<Entity_Materials>(handle);

            if (entity != null)
            {
                entity.SetQuantity(count);

                if (m_inventorySystem.AddItem(handle))
                {
                    Debug.Log($"[TutorialSystem] 재료 보정 지급: id={materialId}, count={count}");
                    return true;
                }
            }

            EntityManager.Destroy(handle);   // 지급 실패 시 개체를 남기지 않는다
            Debug.LogWarning($"[TutorialSystem] 재료 보정 지급 실패: id={materialId}");
            return false;
        }

        // ── 저장 ──

        public string SaveId => "tutorial";
        public Type StateType => typeof(TutorialSave);

        public object CaptureState()
        {
            TutorialSave save = new TutorialSave { completed = m_completed };
            save.shownSteps.AddRange(m_shownSteps);
            return save;
        }

        public void RestoreState(object state)
        {
            if (state is not TutorialSave save)
            {
                return;
            }

            m_completed = save.completed;
            m_shownSteps.Clear();

            if (save.shownSteps == null)
            {
                return;
            }

            foreach (TutorialStep step in save.shownSteps)
            {
                m_shownSteps.Add(step);
            }
        }
    }
}
