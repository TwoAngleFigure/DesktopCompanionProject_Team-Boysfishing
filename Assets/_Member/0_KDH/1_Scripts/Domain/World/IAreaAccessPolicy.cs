namespace DesktopCompanion.Domain.World
{
    /// <summary>
    /// 지역(AreaId) 진입 권한 및 해금 정책을 검증하는 확장형 인터페이스
    /// </summary>
    public interface IAreaAccessPolicy
    {
        /// <summary>
        /// 특정 지역에 진입 가능한지 여부를 검사합니다.
        /// </summary>
        /// <param name="areaId">검사할 지역 고유 식별자</param>
        /// <param name="lockedReason">진입 불가 시 안내 사유 (예: "퀘스트 미완료")</param>
        /// <returns>진입 가능 여부</returns>
        bool CanEnterArea(int areaId, out string lockedReason);
    }

    /// <summary>
    /// 현재 개발 단계에서 사용되는 기본 해금 정책 (모든 구역 100% 무조건 오픈)
    /// </summary>
    public class AlwaysOpenAccessPolicy : IAreaAccessPolicy
    {
        public bool CanEnterArea(int areaId, out string lockedReason)
        {
            lockedReason = string.Empty;
            return true; // 현재는 제약 없이 전 구역 자유 항해 허용
        }
    }
}
