using System;

namespace DesktopCompanion.Domain.World
{
    /// <summary>
    /// 배의 항해 상태머신 정의
    /// </summary>
    public enum VoyageState
    {
        Anchored,   // 정박 완료
        Departing,  // 출항 가속 중
        Traveling,  // 정상 항해 중
        Arriving,   // 입항 감속 중
        Stopping    // 유저 중단 감속 중
    }

    /// <summary>
    /// 정박 형태 구분
    /// </summary>
    public enum AnchorType
    {
        DestinationPort, // 공식 항구 노드 도착 정박 (특수 앵커 씬 로드)
        FreePoint,       // 바다/강 임의 지점 정박 (일반 수면 사이클 유지)
        Interrupted      // 유저 중단에 의한 바다 위 정박 (일반 수면 사이클 유지)
    }

    /// <summary>
    /// 도메인 계층에서 외부(뷰, 낚시 시스템, UI 등)로 상태 변화를 브로드캐스팅하는 이벤트 계약 인터페이스
    /// </summary>
    public interface IWorldDomainEvents
    {
        /// <summary>
        /// 배가 새로운 그리드 셀에 진입할 때마다 발생 (월드맵 UI, 바이옴 감지용)
        /// </summary>
        event Action<WorldCellData> OnCellEntered;

        /// <summary>
        /// 배의 소속 지역(AreaId) 또는 환경 분류가 변경되었을 때 1회 발생 (배경 큐 전환, 낚시터 티어풀 갱신용)
        /// </summary>
        event Action<int /*newAreaId*/, EnvironmentType> OnAreaChanged;

        /// <summary>
        /// 항해 상태머신 전이 시 발생 (배 가감속 뷰, HUD 타이머용)
        /// </summary>
        event Action<VoyageState> OnVoyageStateChanged;

        /// <summary>
        /// 항해가 최종 완료되어 정박했을 때 발생 (정박 씬 처리용)
        /// </summary>
        event Action<AnchorType, WorldCellData> OnAnchored;
    }
}
