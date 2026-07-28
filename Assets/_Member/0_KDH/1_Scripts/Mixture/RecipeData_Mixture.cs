using System;

namespace DesktopCompanion.Data
{
    [Serializable]
    public class RecipeData_Mixture
    {
        public int m_id;
        public string m_resultType;
        public int m_resultId;
        public int m_resultCount;
        public string m_ingredients;
        public bool m_isUnlocked;
        public int m_goldCost;
    }
}