using System;
using UnityEngine;

namespace DesktopCompanion.Data
{
    [CreateAssetMenu(
        fileName = "RecipeData_Mixture", 
        menuName = "Data/RecipeData_Mixture"
        )]
    public class RecipeData_Mixture : GameData
    {
        public string m_resultType;
        public int m_resultId;
        public int m_resultCount;
        public string m_ingredients;
        public bool m_isUnlocked;
        public int m_goldCost;

        // 이 레시피만의 설명 문장. 결과 아이템의 설명으로 부족한 경우(랜덤 장비 생산 등)에 쓴다.
        // 비어 있으면 표시 계층이 결과 아이템의 Description으로 대체한다.
        [TextArea]
        public string m_description;
    }
}