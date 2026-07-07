namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 매 프레임 갱신이 필요한 System이 구현하는 계약.
    /// GameManager(유일한 MonoBehaviour)의 Update가 SystemManager.TickAll을 통해
    /// 등록 순서대로 중계한다 — System은 plain C#을 유지한 채 프레임 루프를 얻는다.
    ///
    /// 사용 전 확인:
    /// - 입력/클릭 반응은 이벤트로(View→System 호출) — 폴링 불필요.
    /// - "꺼져 있어도 흐르는 시간"(방치 정산·보스 리젠)은 Tick 축적이 아니라
    ///   타임스탬프(DateTime 저장+경과 계산) 기반으로 할 것. Tick은 실시간 갱신 전용.
    /// </summary>
    public interface ITickable
    {
        void Tick(float deltaTime);
    }
}
