using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DesktopCompanion.Rendering;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// UI 캔버스의 CanvasScaler를 픽셀 격자 설계값으로 강제하고, 사용자 UI 배율을 반영한다.
    ///  - Constant Pixel Size로 고정한다. 아트 1픽셀이 항상 UnitsPerDot × 배율 화면픽셀로 떨어져
    ///    해상도와 무관하게 정수 크기가 보장된다. ScaleWithScreenSize는 해상도에 따라 소수 배율을 만든다.
    ///  - 사용자 배율은 scaleFactor에 그대로 넣는다. 배율은 UiScaleRange가 1/UnitsPerDot 단위로 스냅한다.
    ///  - 해상도가 바뀌어도 UI 크기는 유지된다. 화면 대비 UI 비중 조절은 이 배율 옵션이 담당한다.
    /// 배율은 정적 값 하나를 모든 인스턴스가 공유하며, 나중에 활성화된 캔버스도 등록 시점에 현재 배율을 따라간다.
    /// ※ 월드 RT 표시 캔버스(Canvas_RT)에는 붙이지 않는다. 그쪽은 Constant Pixel Size · scaleFactor 1이 전제다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public class PixelUiCanvasScaler : MonoBehaviour
    {
        private static readonly List<PixelUiCanvasScaler> s_instances = new();
        private static float s_scale = UiScaleRange.Default;

        /// <summary>현재 적용 중인 UI 배율.</summary>
        public static float Scale => s_scale;

        private CanvasScaler m_scaler;

        /// <summary>등록된 모든 UI 캔버스에 배율을 적용한다. 값은 UiScaleRange 규칙으로 스냅된다.</summary>
        public static void SetScale(float scale)
        {
            s_scale = UiScaleRange.Snap(scale);

            for (int i = s_instances.Count - 1; i >= 0; i--)
            {
                if (s_instances[i] == null)
                {
                    s_instances.RemoveAt(i);   // 파괴된 인스턴스 정리
                    continue;
                }
                s_instances[i].Apply();
            }
        }

        private void OnEnable()
        {
            m_scaler = GetComponent<CanvasScaler>();
            if (s_instances.Contains(this) == false)
            {
                s_instances.Add(this);
            }
            Apply();
        }

        private void OnDisable()
        {
            s_instances.Remove(this);
        }

        private void OnValidate()
        {
            m_scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        /// <summary>스케일러 설정을 설계값으로 덮어쓴다. 반복 호출해도 결과가 같다.</summary>
        private void Apply()
        {
            if (m_scaler == null)
            {
                return;
            }

            // 편집 중에는 저장된 사용자 배율이 아니라 설계 기준(1배)으로 보여준다.
            // 배율이 씬에 굳어 버리면 다음 작업자가 축소된 상태를 원본으로 착각하게 된다.
            float scale = Application.isPlaying ? s_scale : UiScaleRange.Default;

            m_scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            m_scaler.referencePixelsPerUnit = PixelGridDesign.UiReferencePixelsPerUnit;
            m_scaler.scaleFactor = scale;
        }
    }
}
