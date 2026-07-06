namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 모든 게임 System의 최소 계약. 도메인 기능(인벤토리·전투 등)의 단위.
    /// 구현은 보통 <see cref="SystemBase"/>를 상속해서 작성한다.
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// 모든 System이 SystemManager에 등록된 뒤 일괄 호출된다.
        /// 이벤트 구독(예: EntityManager.OnEntityDestroyed)·다른 System 참조 등
        /// "다른 요소가 준비된 뒤에 해야 하는 초기화"를 여기서 한다.
        /// </summary>
        void Initialize();
    }
}
