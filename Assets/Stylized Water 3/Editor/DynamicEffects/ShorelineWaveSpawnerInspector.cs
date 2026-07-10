using UnityEditor;

namespace StylizedWater3.DynamicEffects
{
    [CustomEditor(typeof(ShorelineWaveSpawner))]
    public class ShorelineWaveSpawnerInspector : Editor
    {
        private ShorelineWaveSpawner script;
        
        private void OnEnable()
        {
            script = (ShorelineWaveSpawner)target;
        }
        
        public override void OnInspectorGUI()
        {
            UI.DrawHeader();
            
            #if !SPLINES
            UI.DrawNotification("This component requires the \"Splines\" package to be installed", MessageType.Error);
            #else
            if(script.audioSource)
            {
                if (script.audioSource.transform == script.transform)
                {
                    EditorGUILayout.HelpBox("Audio source transform must be on a separate Transform. Using the same transform as this component causes the spawned waves to also move", MessageType.Error);
                }
            }
            base.OnInspectorGUI();
            #endif
            
            UI.DrawFooter();
        }
    }
}