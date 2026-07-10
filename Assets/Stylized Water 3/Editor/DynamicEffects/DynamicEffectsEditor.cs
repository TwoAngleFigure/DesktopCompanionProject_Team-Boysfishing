using UnityEditor;
using UnityEngine;

namespace StylizedWater3.DynamicEffects
{
    public static class DynamicEffectsEditor
    {
        private const string MENU_BASE_PATH = "GameObject/Effects/Water/";
        private const int MENU_PRIORITY = 1;

        private static void InstantiatePrefab(string guid, bool zeroPosition = false)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            if (assetPath == string.Empty)
            {
                Debug.LogError("Failed to find the prefab with the GUID \"{guid}\". Ensure all the asset files have been imported.");
            }
            
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(instance, $"Created {prefab.name}");
#endif

            if (Selection.activeGameObject)
            {
                instance.transform.parent = Selection.activeGameObject.transform;
                
                if(zeroPosition) instance.transform.localPosition = Vector3.zero;
            }
            Selection.activeObject = instance;

            //Position neatly in scene
            if (zeroPosition == false)
            {
                EditorApplication.ExecuteMenuItem("GameObject/Move To View");

                WaterObject waterObject = WaterObject.Find(instance.transform.position, true);
                if (waterObject)
                {
                    Vector3 waterPosition = waterObject.transform.position;
                    waterPosition.x = instance.transform.position.x;
                    waterPosition.z = instance.transform.position.z;
                    instance.transform.position = waterPosition;
                }
            }
            
        }
        
        [MenuItem(MENU_BASE_PATH + "Shoreline Wave", false, MENU_PRIORITY)]
        public static void CreateShorelineWave()
        {
            InstantiatePrefab("bfd030946afdd474e9d39927216de2a5");
        }
        
        [MenuItem(MENU_BASE_PATH + "Shoreline Ripple", false, MENU_PRIORITY)]
        public static void CreateShorelineRipple()
        {
            InstantiatePrefab("e4db5ed3f7a926348a0a33e5ef96e421");
        }
        
        [MenuItem(MENU_BASE_PATH + "Boat Wake (Particle based)", false, MENU_PRIORITY)]
        public static void CreateBoatWakeParticle()
        {
            InstantiatePrefab("f769116666165df4489d195d20b7c753", true);
        }
        
        [MenuItem(MENU_BASE_PATH + "Boat Wake (Trail based)", false, MENU_PRIORITY)]
        public static void CreateBoatWakeTrail()
        {
            InstantiatePrefab("1d697ae8508b6bc43a5970fb39f51784", true);
        }
        
        [MenuItem(MENU_BASE_PATH + "Directional Ripples", false, MENU_PRIORITY)]
        public static void CreateDirectionalRipples()
        {
            InstantiatePrefab("6ed86fc4f38faff45a210c91e00e755a");
        }
        
        [MenuItem(MENU_BASE_PATH + "Foam Trail", false, MENU_PRIORITY)]
        public static void CreateFoamTrail()
        {
            InstantiatePrefab("dc87553bf6a190a439001159d264c8e9", true);
        }
        
        [MenuItem(MENU_BASE_PATH + "Height Modifier", false, MENU_PRIORITY)]
        public static void CreateHeightModifier()
        {
            InstantiatePrefab("bc4f59ac6fa587a42a8a3dea28409448");
        }
        
        [MenuItem(MENU_BASE_PATH + "Impact Ripple", false, MENU_PRIORITY)]
        public static void CreateImpactRipple()
        {
            InstantiatePrefab("93ce78294d3aecf4d947a20999898482");
        }
        
        [MenuItem(MENU_BASE_PATH + "Raindrops", false, MENU_PRIORITY)]
        public static void CreateRaindrops()
        {
            InstantiatePrefab("447662d17d51f0743b2e9fd19ed0b8a1");
        }
        
        [MenuItem(MENU_BASE_PATH + "Ramp", false, MENU_PRIORITY)]
        public static void CreateRamp()
        {
            InstantiatePrefab("b02ca40fff2db2545ac57c967263ec24");
        }        
        
        [MenuItem(MENU_BASE_PATH + "Ripples (Stationary)", false, MENU_PRIORITY)]
        public static void CreateRipplesStationary()
        {
            InstantiatePrefab("d7ae56b03781f8b4abcefe3efea8681e");
        }
        
        [MenuItem(MENU_BASE_PATH + "Ripples (Trail)", false, MENU_PRIORITY)]
        public static void CreateRipplesTrail()
        {
            InstantiatePrefab("e7cd3a589e3653142a4533703817f041", true);
        }
        
        [MenuItem(MENU_BASE_PATH + "Wind Gust", false, MENU_PRIORITY)]
        public static void CreateWindGust()
        {
            InstantiatePrefab("0722dc0d11b05f9498b323dc69b3e54e");
        }
        
        [MenuItem(MENU_BASE_PATH + "Waterfall Impact Ripples", false, MENU_PRIORITY)]
        public static void CreateWaterfallImpact()
        {
            InstantiatePrefab("1923443893a971945ad7a0c1a9d54cf3");
        }
    }
}