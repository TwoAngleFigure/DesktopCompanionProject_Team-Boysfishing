using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Core;
using DesktopCompanion.Systems;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 플레이 뷰 카메라 화면에 배경 이미지 1개가 100% 딱 맞도록 배치하고,
    /// 배의 이동에 따라 원근감(Parallax) 무한 루프 스크롤을 수행합니다.
    /// AssetProvider와 Addressables 규약(Bg_{StageID} 또는 Bg_{StageID}_A/B)을 통해
    /// 맵이 변경될 때만 메인 카메라 시야 전체 100% 검은색/안개 페이드 전환 연출과 함께 배경 스프라이트를 자동 교체합니다.
    /// </summary>
    [ExecuteAlways]
    public class ParallaxLoopBackgroundView : WorldViewBase
    {
        [Header("=== 무한 루프 배경 패널 (Transform / SpriteRenderer) ===")]
        [SerializeField] private Transform m_bgPanelA;
        [SerializeField] private Transform m_bgPanelB;
        [SerializeField] private SpriteRenderer m_spriteRendererA;
        [SerializeField] private SpriteRenderer m_spriteRendererB;

        [Header("=== 디폴트 대체 배경 에셋 (패널 A, 패널 B용 2개) ===")]
        [Tooltip("해당 맵의 Addressables 에셋이 없을 때 사용할 디폴트 배경 스프라이트 A")]
        [SerializeField] private Sprite m_defaultBackgroundSpriteA;
        [Tooltip("해당 맵의 Addressables 에셋이 없을 때 사용할 디폴트 배경 스프라이트 B (비어있으면 A와 동일하게 사용)")]
        [SerializeField] private Sprite m_defaultBackgroundSpriteB;

        [Header("=== 카메라 자동 피팅 & 수동 오버라이드 조절 ===")]
        [Tooltip("카메라 시야에 패널 크기를 100% 화면 전체에 딱 맞출지 여부")]
        [SerializeField] private bool m_autoFitToCamera = true;
        [Tooltip("자동 피팅 후 추가로 적용할 스케일 배율 (1.0 = 화면 딱 맞춤, 1.2 = 여유 있게 확장)")]
        [SerializeField] private Vector2 m_scaleMultiplier = Vector2.one;
        [Tooltip("자동 피팅 사용 안 할 때 직접 지정할 수동 스케일값")]
        [SerializeField] private Vector2 m_customScale = new Vector2(1f, 1f);

        [Header("=== 위치 & 오프셋 세밀 조절 ===")]
        [Tooltip("카메라 중앙 대비 배경 위치 미세 조절 (X, Y, Z)")]
        [SerializeField] private Vector3 m_positionOffset = Vector3.zero;

        [Header("=== 루프 & 패럴랙스 설정 ===")]
        [Tooltip("배경 패널 1개의 가로 폭 (m_autoFitToCamera 사용 시 화면 너비로 자동 계산됨)")]
        [SerializeField] private float m_bgWidth = 20f;
        [Tooltip("패럴랙스 원근감 비율 (0: 카메라와 완전 동기화, 0.3: 카메라 속도의 30%로 이동)")]
        [Range(0f, 1f)]
        [SerializeField] private float m_parallaxFactor = 0.3f;

        [Header("=== 화면 전환 페이드 연출 (SpriteRenderer Overlay) ===")]
        [SerializeField] private SpriteRenderer m_fadeOverlaySprite;
        [SerializeField] private float m_fadeDuration = 0.8f;
        [SerializeField] private Color m_fadeColor = Color.black;

        private StageSystem m_stageSystem;
        private VoyageSystem m_voyageSystem;

        private Transform m_cameraTransform;
        private Camera m_mainCamera;
        private float m_lastCameraX;
        private bool m_isFading = false;
        private StageData m_currentAreaStage;

        public override void Bind()
        {
            m_stageSystem = SystemManager?.GetSystem<StageSystem>();
            m_voyageSystem = SystemManager?.GetSystem<VoyageSystem>();

            if (m_stageSystem != null)
            {
                m_stageSystem.OnAreaStageChanged += HandleAreaStageChanged;
            }

            FindMainCamera();
            EnsureFadeOverlaySetup();

            if (m_fadeOverlaySprite != null)
            {
                Color c = m_fadeColor;
                c.a = 0f;
                m_fadeOverlaySprite.color = c;
            }

            if (m_stageSystem != null && m_stageSystem.CurrentAreaStageData != null)
            {
                m_currentAreaStage = m_stageSystem.CurrentAreaStageData;
                ApplyStageBackgroundSprite(m_currentAreaStage);
            }
            else
            {
                ApplyDefaultSprites();
            }

            ApplyCameraFittingAndScaling();
            ResetPanelsPosition();
        }

        public override void Unbind()
        {
            if (m_stageSystem != null)
            {
                m_stageSystem.OnAreaStageChanged -= HandleAreaStageChanged;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AutoSetupReferences();
            EnsureFadeOverlaySetup();
            ApplyCameraFittingAndScaling();
            ResetPanelsPosition();
        }

        private void OnValidate()
        {
            AutoSetupReferences();
            EnsureFadeOverlaySetup();
            ApplyCameraFittingAndScaling();
            ResetPanelsPosition();
        }

        private void AutoSetupReferences()
        {
            if (m_bgPanelA == null && transform.childCount > 0) m_bgPanelA = transform.GetChild(0);
            if (m_bgPanelB == null && transform.childCount > 1) m_bgPanelB = transform.GetChild(1);

            if (m_bgPanelA != null && m_spriteRendererA == null) m_spriteRendererA = m_bgPanelA.GetComponent<SpriteRenderer>();
            if (m_bgPanelB != null && m_spriteRendererB == null) m_spriteRendererB = m_bgPanelB.GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// FadeOverlay 가림막을 메인 카메라의 직접 자식(Child)으로 밀착 부착하여 100% 화면 전체 커버 보장
        /// </summary>
        private void EnsureFadeOverlaySetup()
        {
            FindMainCamera();

            if (m_fadeOverlaySprite == null)
            {
                // 메인 카메라 아래에 FadeOverlay가 이미 있는지 탐색
                if (m_mainCamera != null)
                {
                    Transform camFadeChild = m_mainCamera.transform.Find("CameraFadeOverlay");
                    if (camFadeChild != null)
                    {
                        m_fadeOverlaySprite = camFadeChild.GetComponent<SpriteRenderer>();
                    }
                }

                if (m_fadeOverlaySprite == null && Application.isPlaying)
                {
                    GameObject fadeObj = new GameObject("CameraFadeOverlay");
                    if (m_mainCamera != null)
                    {
                        fadeObj.transform.SetParent(m_mainCamera.transform);
                    }
                    else
                    {
                        fadeObj.transform.SetParent(transform);
                    }
                    m_fadeOverlaySprite = fadeObj.AddComponent<SpriteRenderer>();
                }
            }

            // 🎯 런타임 재생 중일 때만 계층구조(SetParent) 변경 실행 (OnValidate 에러 방지)
            if (Application.isPlaying && m_mainCamera != null && m_fadeOverlaySprite != null && m_fadeOverlaySprite.transform.parent != m_mainCamera.transform)
            {
                m_fadeOverlaySprite.transform.SetParent(m_mainCamera.transform);
            }

            // PPU = 1f 의 피벗 (0.5, 0.5) 정중앙 1x1 픽셀 단색 흰색 스프라이트 생성
            if (m_fadeOverlaySprite != null)
            {
                if (m_fadeOverlaySprite.sprite == null)
                {
                    Texture2D tex = new Texture2D(1, 1);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    m_fadeOverlaySprite.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                }

                m_fadeOverlaySprite.sortingOrder = 32767; // 화면 최상단 레이어
            }

            UpdateFadeOverlayPosition();
        }

        private void FindMainCamera()
        {
            if (m_mainCamera == null) m_mainCamera = Camera.main;
            if (m_mainCamera == null) m_mainCamera = FindAnyObjectByType<Camera>();

            if (m_mainCamera != null)
            {
                m_cameraTransform = m_mainCamera.transform;
                m_lastCameraX = m_cameraTransform.position.x;
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;

            if (m_cameraTransform == null)
            {
                FindMainCamera();
                if (m_cameraTransform == null) return;
            }

            float currentCamX = m_cameraTransform.position.x;
            float deltaX = currentCamX - m_lastCameraX;
            m_lastCameraX = currentCamX;

            float bgMoveAmount = deltaX * m_parallaxFactor;
            if (m_bgPanelA != null) m_bgPanelA.position += Vector3.right * bgMoveAmount;
            if (m_bgPanelB != null) m_bgPanelB.position += Vector3.right * bgMoveAmount;

            CheckAndLoopPanels(currentCamX);
            UpdateFadeOverlayPosition();
        }

        /// <summary>
        /// 페이드 가림막을 메인 카메라의 정중앙(Local (0,0)) 전면에 밀착 및 넉넉한 스케일 적용
        /// </summary>
        private void UpdateFadeOverlayPosition()
        {
            if (m_fadeOverlaySprite == null || m_mainCamera == null) return;

            // 🎯 메인 카메라의 직속 자식으로서 카메라 정중앙 (0, 0) 및 Z축 바로 앞에 밀착!
            m_fadeOverlaySprite.transform.localPosition = new Vector3(0f, 0f, m_mainCamera.nearClipPlane + 0.2f);
            m_fadeOverlaySprite.transform.localRotation = Quaternion.identity;

            // 카메라 시야 크기 계산
            float camHeight = m_mainCamera.orthographic ? (2f * m_mainCamera.orthographicSize) : 20f;
            float camWidth = camHeight * m_mainCamera.aspect;

            // 🎯 카메라 화면 전체 상/하/좌/우를 넉넉하게 가리도록 5배 거대 스케일 적용
            float scaleX = Mathf.Max(camWidth * 5f, 200f);
            float scaleY = Mathf.Max(camHeight * 5f, 200f);
            m_fadeOverlaySprite.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        [ContextMenu("Apply Fitting & Scaling Now")]
        public void ApplyCameraFittingAndScaling()
        {
            FindMainCamera();

            float camWidth = 19.2f;
            float camHeight = 10.8f;

            if (m_mainCamera != null)
            {
                float distanceZ = Mathf.Abs(m_mainCamera.transform.position.z - (m_bgPanelA != null ? m_bgPanelA.position.z : 50f));
                Vector3 bottomLeft = m_mainCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distanceZ));
                Vector3 topRight = m_mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, distanceZ));

                camWidth = Mathf.Abs(topRight.x - bottomLeft.x);
                camHeight = Mathf.Abs(topRight.y - bottomLeft.y);
            }

            Vector3 finalScale = Vector3.one;

            if (m_autoFitToCamera && m_spriteRendererA != null && m_spriteRendererA.sprite != null)
            {
                Vector2 spriteSize = m_spriteRendererA.sprite.rect.size / m_spriteRendererA.sprite.pixelsPerUnit;
                if (spriteSize.x > 0 && spriteSize.y > 0)
                {
                    float baseScaleX = camWidth / spriteSize.x;
                    float baseScaleY = camHeight / spriteSize.y;

                    finalScale = new Vector3(baseScaleX * m_scaleMultiplier.x, baseScaleY * m_scaleMultiplier.y, 1f);
                    m_bgWidth = spriteSize.x * finalScale.x;
                }
            }
            else
            {
                finalScale = new Vector3(m_customScale.x, m_customScale.y, 1f);
                if (m_spriteRendererA != null && m_spriteRendererA.sprite != null)
                {
                    m_bgWidth = (m_spriteRendererA.sprite.rect.width / m_spriteRendererA.sprite.pixelsPerUnit) * finalScale.x;
                }
                else
                {
                    m_bgWidth = camWidth * m_customScale.x;
                }
            }

            if (m_bgPanelA != null) m_bgPanelA.localScale = finalScale;
            if (m_bgPanelB != null) m_bgPanelB.localScale = finalScale;

            UpdateFadeOverlayPosition();
        }

        private void CheckAndLoopPanels(float camX)
        {
            if (m_bgPanelA == null || m_bgPanelB == null) return;

            float thresholdLeft = camX - m_bgWidth;
            float thresholdRight = camX + m_bgWidth;

            if (m_bgPanelA.position.x < thresholdLeft)
            {
                m_bgPanelA.position = new Vector3(m_bgPanelB.position.x + m_bgWidth, m_bgPanelA.position.y, m_bgPanelA.position.z);
            }
            else if (m_bgPanelB.position.x < thresholdLeft)
            {
                m_bgPanelB.position = new Vector3(m_bgPanelA.position.x + m_bgWidth, m_bgPanelA.position.y, m_bgPanelA.position.z);
            }

            if (m_bgPanelA.position.x > thresholdRight)
            {
                m_bgPanelA.position = new Vector3(m_bgPanelB.position.x - m_bgWidth, m_bgPanelA.position.y, m_bgPanelA.position.z);
            }
            else if (m_bgPanelB.position.x > thresholdRight)
            {
                m_bgPanelB.position = new Vector3(m_bgPanelA.position.x - m_bgWidth, m_bgPanelB.position.y, m_bgPanelB.position.z);
            }
        }

        private void ResetPanelsPosition()
        {
            FindMainCamera();

            float camX = (m_mainCamera != null ? m_mainCamera.transform.position.x : 0f) + m_positionOffset.x;
            float camY = (m_mainCamera != null ? m_mainCamera.transform.position.y : 0f) + m_positionOffset.y;
            float posZ = (m_bgPanelA != null) ? m_bgPanelA.position.z : ((m_mainCamera != null ? m_mainCamera.transform.position.z : 0f) + 50f + m_positionOffset.z);

            if (m_bgPanelA != null)
            {
                m_bgPanelA.position = new Vector3(camX, camY, posZ);
            }

            if (m_bgPanelB != null)
            {
                m_bgPanelB.position = new Vector3(camX + m_bgWidth, camY, posZ);
            }

            UpdateFadeOverlayPosition();
        }

        private void HandleAreaStageChanged(StageData newStage)
        {
            if (newStage == null) return;
            if (m_currentAreaStage != null && newStage.ID == m_currentAreaStage.ID) return;
            if (m_isFading) return;

            Debug.Log($"[ParallaxLoopBackgroundView] 🎬 맵 영역 변경 감지! ({m_currentAreaStage?.ID} -> {newStage.ID}) -> 페이드 코루틴 시작!");
            m_currentAreaStage = newStage;

            StartCoroutine(Co_TransitionToNewStageBackground(newStage));
        }

        private IEnumerator Co_TransitionToNewStageBackground(StageData newStage)
        {
            m_isFading = true;
            EnsureFadeOverlaySetup();

            Debug.Log($"[ParallaxLoopBackgroundView] 🎬 [1/3] Fade Out 시작 (Duration: {m_fadeDuration}s)");

            // 1. [Fade Out] 화면 전체가 부드럽게 검은색으로 가려짐
            if (m_fadeOverlaySprite != null)
            {
                float timer = 0f;
                Color baseColor = m_fadeColor;
                while (timer < m_fadeDuration)
                {
                    timer += Time.deltaTime;
                    baseColor.a = Mathf.Clamp01(timer / m_fadeDuration);
                    m_fadeOverlaySprite.color = baseColor;
                    yield return null;
                }
                baseColor.a = 1f;
                m_fadeOverlaySprite.color = baseColor;
            }

            Debug.Log($"[ParallaxLoopBackgroundView] 🎬 [2/3] 화면 가려짐 -> 배경 에셋 교체!");

            // 2. 🌄 새 맵 배경 에셋 교체
            ApplyStageBackgroundSprite(newStage);

            // TODO: [확장 포인트] BluePrint 3D/2D 오브젝트 생성 및 제거 로직 연동 예정 지점

            yield return new WaitForSeconds(0.15f);

            Debug.Log($"[ParallaxLoopBackgroundView] 🎬 [3/3] Fade In 시작!");

            // 3. [Fade In] 화면 전체가 부드럽게 밝아짐
            if (m_fadeOverlaySprite != null)
            {
                float timer = 0f;
                Color baseColor = m_fadeColor;
                while (timer < m_fadeDuration)
                {
                    timer += Time.deltaTime;
                    baseColor.a = Mathf.Clamp01(1f - (timer / m_fadeDuration));
                    m_fadeOverlaySprite.color = baseColor;
                    yield return null;
                }
                baseColor.a = 0f;
                m_fadeOverlaySprite.color = baseColor;
            }

            m_isFading = false;
            Debug.Log($"[ParallaxLoopBackgroundView] 🎬 페이드 전환 연출 완벽 종료!");
        }

        private Sprite FetchSpriteFromAssetProvider(string key)
        {
            if (AssetProvider == null || string.IsNullOrEmpty(key)) return null;

            if (AssetProvider.TryGet<Sprite>(key, out Sprite spriteAsset))
            {
                return spriteAsset;
            }

            if (AssetProvider.TryGet<Texture2D>(key, out Texture2D texAsset) && texAsset != null)
            {
                return Sprite.Create(texAsset, new Rect(0, 0, texAsset.width, texAsset.height), new Vector2(0.5f, 0.5f), 100f);
            }

            return null;
        }

        private void ApplyStageBackgroundSprite(StageData stageData)
        {
            if (stageData == null) return;

            Sprite spriteA = null;
            Sprite spriteB = null;

            if (AssetProvider != null)
            {
                spriteA = FetchSpriteFromAssetProvider($"Bg_{stageData.ID}_A");
                spriteB = FetchSpriteFromAssetProvider($"Bg_{stageData.ID}_B");

                if (spriteA == null)
                {
                    string singleKey = !string.IsNullOrEmpty(stageData.AssetKey) ? stageData.AssetKey : $"Bg_{stageData.ID}";
                    spriteA = FetchSpriteFromAssetProvider(singleKey);
                    spriteB = spriteA;
                }
            }

            if (spriteA == null) spriteA = m_defaultBackgroundSpriteA;
            if (spriteB == null) spriteB = (m_defaultBackgroundSpriteB != null) ? m_defaultBackgroundSpriteB : m_defaultBackgroundSpriteA;

            if (m_spriteRendererA != null) m_spriteRendererA.sprite = spriteA;
            if (m_spriteRendererB != null) m_spriteRendererB.sprite = spriteB;

            ApplyCameraFittingAndScaling();
            Debug.Log($"[ParallaxLoopBackgroundView] 🌄 2D World 배경 패널 A/B 교체 완료! 현재 맵: {stageData.Name} (ID: {stageData.ID})");
        }

        private void ApplyDefaultSprites()
        {
            Sprite spriteA = m_defaultBackgroundSpriteA;
            Sprite spriteB = (m_defaultBackgroundSpriteB != null) ? m_defaultBackgroundSpriteB : m_defaultBackgroundSpriteA;

            if (m_spriteRendererA != null) m_spriteRendererA.sprite = spriteA;
            if (m_spriteRendererB != null) m_spriteRendererB.sprite = spriteB;
        }
    }
}
