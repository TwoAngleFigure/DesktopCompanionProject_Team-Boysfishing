# AGENTS.md

## 기본 작업 규칙

- 코드를 직접 수정하지 말고, 변경이 필요하면 먼저 수정안을 텍스트로 제안한다.
- 사용자가 명시적으로 파일 생성을 요청한 문서 작업은 수행해도 된다.
- 기존 파일을 덮어쓰기 전에 반드시 내용을 확인한다.
- Unity C# 코드는 기존 네임스페이스, 폴더 구조, 매니저 패턴을 따른다.

## 먼저 읽을 문서

작업 종류에 따라 아래 문서를 먼저 읽고 설계를 맞춘다.

- UI, uGUI, ViewModel, 창 UI 작업: `Docs/API/UIManager.md`
- 3D 월드 View, World 오브젝트 표시 작업: `Docs/API/WorldManager.md`
- 프로젝트 프레임워크 전체 맥락: `Docs/Framework_Guide.md`

## 요약

- UI는 MVVM 패턴을 따른다.
- 일반 UI는 `UIViewBase`, 창 UI는 `UIWindowBase`를 상속한다.
- 3D World View는 `WorldViewBase`를 상속한다.
- System은 상태와 로직을 담당하고, View/ViewModel은 표시와 입력 연결을 담당한다.
- `Bind()`와 `Unbind()`는 반드시 1:1로 대응시킨다.
