using System;
using System.Collections.Generic;
using DesktopCompanion.Views;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// 연출 목록을 타입 선택이 되는 리스트로 그리고, 각 상태의 최종 모습을 미리 보게 한다.
    ///
    /// <see cref="SerializeReference"/> 배열의 기본 인스펙터는 이 Unity 버전에서 `+`를 눌러도
    /// 빈 항목만 추가되고 어떤 효과인지 고를 수단이 없다 — 그래서 리스트를 직접 그린다.
    /// 타입 목록은 <see cref="TypeCache"/>로 훑으므로, 새 효과 클래스를 만들면
    /// 등록 없이 드롭다운에 나타난다.
    ///
    /// 미리보기는 <b>실제 값을 바꾼다</b>(폭·위치가 진짜로 변한다). 값을 감췄다 되돌리는 방식은
    /// 효과마다 '내가 건드리는 대상 목록'을 노출해야 해서 새 효과를 만드는 비용을 올린다 —
    /// 그래서 안내와 되돌리기 버튼으로 대신한다.
    /// </summary>
    [CustomEditor(typeof(ButtonStateOwner))]
    [CanEditMultipleObjects]
    public class ButtonStateOwnerEditor : Editor
    {
        private const string EffectsField = "m_effects";

        private static List<Type> s_effectTypes;

        private SerializedProperty m_effects;
        private ReorderableList m_list;

        private void OnEnable()
        {
            m_effects = serializedObject.FindProperty(EffectsField);
            if (m_effects == null)
            {
                return;
            }

            m_list = new ReorderableList(serializedObject, m_effects, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "연출 목록"),
                elementHeightCallback = ElementHeight,
                drawElementCallback = DrawElement,
                onAddDropdownCallback = ShowAddMenu,
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 연출 목록만 빼고 나머지 필드는 기본 방식으로 그린다.
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }
                    continue;
                }
                if (iterator.name == EffectsField)
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            if (m_list != null)
            {
                EditorGUILayout.Space();
                m_list.DoLayoutList();
            }

            serializedObject.ApplyModifiedProperties();

            DrawPreview();
        }

        // ── 목록 ──

        private float ElementHeight(int index)
        {
            SerializedProperty element = m_effects.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = m_effects.GetArrayElementAtIndex(index);

            rect.y += EditorGUIUtility.standardVerticalSpacing * 0.5f;
            rect.height = EditorGUI.GetPropertyHeight(element, true);

            EditorGUI.PropertyField(rect, element, new GUIContent(TypeLabel(element)), true);
        }

        private void ShowAddMenu(Rect rect, ReorderableList list)
        {
            var menu = new GenericMenu();

            foreach (Type type in EffectTypes())
            {
                Type captured = type;   // 클로저가 루프 변수를 잡지 않도록 복사
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false,
                             () => AddEffect(captured));
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("추가할 수 있는 효과가 없습니다"));
            }
            menu.DropDown(rect);
        }

        /// <summary>
        /// 메뉴 콜백은 이 OnInspectorGUI가 끝난 뒤에 실행되므로,
        /// 들고 있던 SerializedObject가 낡았을 수 있다 — 다시 읽고 쓴다.
        /// </summary>
        private void AddEffect(Type type)
        {
            serializedObject.Update();

            SerializedProperty effects = serializedObject.FindProperty(EffectsField);
            int index = effects.arraySize;

            effects.InsertArrayElementAtIndex(index);
            effects.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(type);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>목록 항목에 보일 이름. managedReferenceFullTypename은 "어셈블리 네임스페이스.타입" 꼴이다.</summary>
        private static string TypeLabel(SerializedProperty element)
        {
            string full = element.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full))
            {
                return "(비어 있음 — 항목을 지우고 다시 추가할 것)";
            }

            int space = full.IndexOf(' ');
            string typeName = space >= 0 ? full.Substring(space + 1) : full;

            int dot = typeName.LastIndexOf('.');
            if (dot >= 0)
            {
                typeName = typeName.Substring(dot + 1);
            }
            return ObjectNames.NicifyVariableName(typeName);
        }

        /// <summary>붙일 수 있는 효과 타입. 클래스를 만들기만 하면 여기 잡힌다 — 등록 절차가 없다.</summary>
        private static List<Type> EffectTypes()
        {
            if (s_effectTypes != null)
            {
                return s_effectTypes;
            }

            s_effectTypes = new List<Type>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<ButtonStateEffect>())
            {
                if (type.IsAbstract || type.IsGenericType)
                {
                    continue;
                }
                s_effectTypes.Add(type);
            }
            s_effectTypes.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return s_effectTypes;
        }

        // ── 미리보기 ──

        private void DrawPreview()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "누르면 그 상태의 최종 모습이 씬에 그대로 반영된다.\n" +
                "실제 값이 바뀌므로 저장하기 전에 '대기로 되돌리기'를 누를 것.",
                MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("대기")) Preview(ButtonVisualState.Normal);
                if (GUILayout.Button("마우스 오버")) Preview(ButtonVisualState.Hover);
                if (GUILayout.Button("켜짐")) Preview(ButtonVisualState.Active);
            }

            if (GUILayout.Button("대기로 되돌리기"))
            {
                Preview(ButtonVisualState.Normal);
            }
        }

        private void Preview(ButtonVisualState state)
        {
            foreach (UnityEngine.Object each in targets)
            {
                if (each is ButtonStateOwner owner)
                {
                    owner.PreviewState(state);
                }
            }
        }
    }
}
