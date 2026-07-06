using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DesktopCompanion
{
    /// <summary>
    /// 커서 아래 픽셀의 알파를 검사해 클릭 관통을 동적으로 토글한다.
    /// 캐릭터(불투명 픽셀) 위 = 클릭 가능, 투명 영역 = 관통.
    /// ReadPixels는 프레임 렌더 완료 이후 호출해야 하므로 WaitForEndOfFrame으로 처리한다.
    /// </summary>
    [RequireComponent(typeof(TransparentWindow))]
    public class ClickThroughManager : MonoBehaviour
    {
        // _alphaThreshold / _window / _currentClickThrough는 Windows Standalone 빌드
        // (#if UNITY_STANDALONE_WIN && !UNITY_EDITOR)의 클릭 관통 로직에서만 읽힌다. 에디터는 해당
        // 블록을 컴파일에서 제외하므로 "할당했지만 미사용(CS0414)" 경고가 뜨지만, 빌드에서는 사용되므로
        // 필드를 삭제하면 안 된다. 에디터 경고만 억제한다. (_pixelBuffer는 OnDestroy에서 쓰여 경고 없음)
#pragma warning disable CS0414
        [Tooltip("이 값 이상의 알파면 캐릭터(클릭 가능)로 간주")]
        [SerializeField] private float _alphaThreshold = 0.1f;

        private TransparentWindow _window;
        private Texture2D _pixelBuffer;
        private bool _currentClickThrough = true;
#pragma warning restore CS0414

        private void Awake()
        {
            _window = GetComponent<TransparentWindow>();
            _pixelBuffer = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        }

        private void OnDestroy()
        {
            if (_pixelBuffer != null)
            {
                Destroy(_pixelBuffer);
            }
        }

        private void OnEnable()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            StartCoroutine(UpdateClickThroughLoop());
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator UpdateClickThroughLoop()
        {
            // ReadPixels는 프레임 렌더 완료 후에 호출해야 하므로 WaitForEndOfFrame 뒤에 처리한다.
            // 단일 프레임 예외가 루프를 영구 정지시키지 않도록 처리 본문은 try/catch로 감싼다.
            var frameEnd = new WaitForEndOfFrame();
            while (enabled)
            {
                yield return frameEnd;
                ProcessFrame();
            }
        }

        private void ProcessFrame()
        {
            if (_window.IsReady == false)
            {
                return;
            }

            try
            {
                bool overCharacter = IsCursorOverOpaquePixel();
                bool wantClickThrough = overCharacter == false; // 캐릭터 위면 관통 해제
                if (wantClickThrough != _currentClickThrough)
                {
                    _window.SetClickThrough(wantClickThrough);
                    _currentClickThrough = wantClickThrough;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ClickThroughManager] 프레임 처리 예외(무시하고 계속): {e.Message}");
            }
        }

        // 마우스 위치의 렌더 결과 알파를 1픽셀만 읽어 판정한다.
        private bool IsCursorOverOpaquePixel()
        {
            // Input System 패키지 사용. Mouse가 없는 프레임(입력 장치 미가용)은 관통 유지.
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            Vector2 mousePos = mouse.position.ReadValue();
            if (mousePos.x < 0 || mousePos.y < 0 ||
                mousePos.x >= Screen.width || mousePos.y >= Screen.height)
            {
                return false;
            }

            var readRect = new Rect(mousePos.x, mousePos.y, 1, 1);
            _pixelBuffer.ReadPixels(readRect, 0, 0, false);
            _pixelBuffer.Apply(false);
            return _pixelBuffer.GetPixel(0, 0).a >= _alphaThreshold;
        }
#endif
    }
}
