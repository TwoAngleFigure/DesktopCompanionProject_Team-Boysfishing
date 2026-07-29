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
    }
}