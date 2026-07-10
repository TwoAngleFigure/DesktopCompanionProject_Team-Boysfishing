// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace StylizedWater3
{
    [CustomPropertyDrawer(typeof(HeightQuerySystem.Interface))]
    public class HeightInterfaceDrawer : PropertyDrawer
    {
        private bool waveProfileMismatch;
        private bool renderFeatureSetup;

        private bool enabled;

        private void OnEnable()
        {
            enabled = true;

            CheckRenderFeature();
        }

        private void CheckRenderFeature()
        {
            renderFeatureSetup = StylizedWaterEditor.IsRenderFeatureSetup();
        }
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if(!enabled) OnEnable();
            
            GUILayout.Space(-GetPropertyHeight(property, label));
            
            EditorGUILayout.LabelField("Water Height Interface", EditorStyles.boldLabel);
            
            var methodProperty = property.FindPropertyRelative("method");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(methodProperty);
            if (EditorGUI.EndChangeCheck())
            {
                CheckRenderFeature();
            }
            
            if (methodProperty.intValue == (int)HeightQuerySystem.Interface.Method.CPU)
            {
                EditorGUILayout.Separator();
                
                EditorGUI.BeginChangeCheck();
                
                EditorGUI.indentLevel++;
                
                var waterObject = property.FindPropertyRelative("waterObject");
                var autoFind = property.FindPropertyRelative("autoFind");
                using (new EditorGUI.DisabledScope(autoFind.boolValue))
                {
                    EditorGUILayout.PropertyField(waterObject);
                }
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(autoFind);
                EditorGUI.indentLevel--;

                var waveProfile = property.FindPropertyRelative("waveProfile");
                EditorGUILayout.PropertyField(waveProfile);
                if (waveProfile.objectReferenceValue == null)
                {
                    if (waterObject.objectReferenceValue)
                    {
                        UI.DrawNotification(true, "A wave profile must assigned", "Try get", () =>
                        {
                            waveProfile.objectReferenceValue = WaveProfileEditor.LoadFromWaterObject(waterObject.objectReferenceValue as WaterObject);
                        }, MessageType.Error);
                    }
                    else
                    {
                        UI.DrawNotification("A wave profile must assigned", MessageType.Error);
                    }
                }
                else
                {
                    if (waveProfileMismatch)
                    {
                        UI.DrawNotification(true, "The wave profile does not match the one used on the water material." +
                                                  "\n\nWave animations will likely appear out of sync", "Attempt fix",() =>
                        {
                            WaterObject obj = (WaterObject)waterObject.objectReferenceValue;
                            waveProfile.objectReferenceValue = WaveProfileEditor.LoadFromMaterial(obj.material);
                        }, MessageType.Warning);
                    }
                }
                
                EditorGUILayout.Separator();
                
                var waterLevelSource = property.FindPropertyRelative("waterLevelSource");
                EditorGUILayout.PropertyField(waterLevelSource);
                
                if (waterLevelSource.intValue == (int)HeightQuerySystem.Interface.WaterLevelSource.FixedValue)
                {
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("waterLevel"));
                }
                if (waterLevelSource.intValue == (int)HeightQuerySystem.Interface.WaterLevelSource.Transform)
                {
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("waterLevelTransform"), new GUIContent("Transform", "The transform to use to calculate the water level"));
                }

                if (waterLevelSource.intValue == (int)HeightQuerySystem.Interface.WaterLevelSource.Ocean &&
                    !OceanFollowBehaviour.Instance)
                {
                    EditorGUILayout.HelpBox("No ocean is currently present", MessageType.Warning);
                }

                if (waterLevelSource.intValue != (int)HeightQuerySystem.Interface.WaterLevelSource.FixedValue)
                {
                    HeightQuerySystem.Interface interfaceObj = property.boxedValue as HeightQuerySystem.Interface;
                    EditorGUILayout.HelpBox($"Water level: {interfaceObj.GetWaterLevel()}", MessageType.None, false);
                }

                EditorGUI.indentLevel--;
                
                if (EditorGUI.EndChangeCheck())
                {
                    waveProfileMismatch = false;

                    if (waveProfile.objectReferenceValue && waterObject.objectReferenceValue)
                    {
                        WaveProfile profile = (WaveProfile)waveProfile.objectReferenceValue;
                        WaterObject obj = (WaterObject)waterObject.objectReferenceValue;

                        WaveProfile materialProfile = WaveProfileEditor.LoadFromMaterial(obj.material);
                        waveProfileMismatch = materialProfile != profile;
                    }
                }
            }
            else
            {
                UI.DrawNotification(HeightQuerySystem.IsSupported() == false,"This technique is not supported on the current platform." +
                                    "\n\n" +
                                    "Unity reports asynchronous compute shaders are not supported, which this functionality relies on.", MessageType.Error);
                
                UI.DrawRenderFeatureSetupError(ref renderFeatureSetup);

                if (Application.isPlaying == false && HeightQuerySystem.DISABLE_IN_EDIT_MODE)
                {
                    UI.DrawNotification("GPU height queries have been disabled while in edit mode. You'll find this option on the render feature", MessageType.Warning);
                }

                if (Application.isPlaying && Camera.main == null)
                {
                    UI.DrawNotification("No main camera found, water height will not be correct. Ensure a camera has the \"MainCamera\" tag.", MessageType.Error);
                }
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("");

                    if (GUILayout.Button(new GUIContent(" Inspect Queries", EditorGUIUtility.FindTexture("_Help"))))
                    {
                        HeightQuerySystemEditor.HeightQueryInspector.Open();
                    }
                }
            }
        }
    }
}