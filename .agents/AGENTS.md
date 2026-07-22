# Desktop Companion 프레임워크 룰 (팀원 가이드)

프로젝트를 진행하며 코드 작성/수정 시 다음의 3가지 프레임워크 아키텍처 원칙을 무조건 준수합니다.

## 1. WorldManager API (3D 월드 뷰 관리)
- **WorldViewBase 상속**: 월드 오브젝트는 WorldViewBase를 상속. WorldManager가 관리.
- **Bind() / Unbind()**: 자신의 System Action 이벤트를 구독(+=)/해제(-=)만 작성. SystemManager.GetSystem<T>() 활용.
- **단방향 데이터 흐름**: System(상태 변화) → Action 발행 → 유닛이 구독하여 월드 오브젝트 갱신(AssetProvider 활용).
- **페이로드 규칙**: System은 Action 이벤트에 Entity 객체 참조를 넘기면 안 됨! 오직 "식별자(EntityHandle) + 원시/불변 값(int, Vector3 등)"만 전달할 것.

## 2. UIManager API (UI 중앙 관리 및 MVVM 통일)
- **UI Base 클래스 상속**: 
  - 일반 UI는 UIViewBase 상속.
  - 창 UI(인벤토리 등)는 UIWindowBase 상속 (스택 및 우클릭 닫기 자동 참여).
- **ViewModel 구현 (UIViewModelBase)**:
  - Bind() / Unbind()에서 System의 Action을 구독/해제.
  - 뷰 갱신을 위해 BindableProperty<T> 사용 (값 변경 통지).
  - 뷰 입력을 처리하기 위해 RelayCommand 정의.
- **양방향 통신 (MVVM)**: 
  - [View] → RelayCommand → [ViewModel] → System 메서드 호출
  - [System] → Action 이벤트 → [ViewModel] → BindableProperty → [View]

## 3. 프레임워크 코어 및 아키텍처 규칙
- **의존성 방향성**: System은 EntityManager와 다른 System(통해 SystemManager)을 참조 가능하지만, EntityManager는 도메인을 알면 안 되며, System은 UI나 View를 직접 참조하면 안 됨.
- **Entity**: 로직 불가. 상태(정적 Data는 DataManager에서 읽고, 런타임 가변 상태는 Entity에 저장)와 EntityHandle만 유지. 
- **System**:
  - SystemBase 상속. 도메인 기능 및 가변 상태 소유.
  - Entity의 생성, 조회, 소멸은 무조건 EntityManager를 통해서 수행 (Create, Get, Destroy).
  - Entity 자체를 변수에 담지 말고 EntityHandle로만 보관할 것.
  - Update가 필요하다면 ITickable을 구현하여 Tick(float deltaTime)에서 처리 (MonoBehaviour 코루틴 불가).
- **Data (SO)**: 수정 불가능한 정의/청사진. 추가 시 GameManager.RegisterSystems/RegisterEntityFactories에 등록 및 세팅.
- **세이브 (ISaveable)**: 영구 상태 저장은 DTO (POCO)에 Data ID와 가변 상태만 포함. Entity 참조는 문자열 변환 (ToString("N"))하여 저장.
- **비주얼 에셋**: Data에서 에셋 직접 참조 금지. Addressables 키 규약({클래스명}_{ID}_{용도})를 따르고, View 단에서만 AssetProvider로 조회하여 세팅 (AssetKeys.Of(data, usage)).
