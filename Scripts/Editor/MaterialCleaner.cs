
// Original code taken from https://forum.unity.com/threads/clear-old-texture-references-from-materials.318769/
// Modifications by Dj Lukis.LT and are licensed under "unlicense"

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

//namespace CrankshaftEditor.Toolset
namespace DJL
{
    public class MaterialCleaner : EditorWindow
    {
        private Material m_selectedMaterial;
        private SerializedObject m_serializedObject;
        private Vector2 scrollPos;
        private const string PropPath_Tex   = "m_SavedProperties.m_TexEnvs";
        private const string PropPath_Float = "m_SavedProperties.m_Floats";
        private const string PropPath_Col   = "m_SavedProperties.m_Colors";

        [MenuItem("Window/Material Cleaner")]
        private static void Init()
        {
            GetWindow<MaterialCleaner>("Mat. Cleaner");
        }
        [MenuItem("Tools/DJL/Cleanup Material properties")]
        private static void CleanupMultiple()
        {
            foreach (var obj in Selection.objects)
            {
                Material mat = obj as Material;
                if (mat != null)
                {
                    SerializedObject serObj = new SerializedObject(mat);
                    RemoveAllUnusedProperties(mat, serObj);
                }
            }
        }

        [MenuItem("CONTEXT/ModelImporter/Cleanup material remapping")]
        public static void CLeanupMaterialRemapping(MenuCommand menuCommand)
        {
            ModelImporter mi = menuCommand.context as ModelImporter;

            // The list of materials that are actually used by the model is not exposed,
            // but can be accessed via SerializedObject interface
            var usedmats = new SerializedObject(mi).FindProperty("m_Materials");

            // Cache the actually-used name list
            List<string> usednames = new List<string>(usedmats.arraySize);
            for (int i = 0; i < usedmats.arraySize; i++)
            {
                // SourceAssetIdentifier is struct so need to use 'FindPropertyRelative' instead of 'objectReferenceValue'
                SerializedProperty usedmat = usedmats.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                usednames.Add(usedmat.stringValue);
            }

            var remaps = mi.GetExternalObjectMap();
            int count = 0;
            foreach(var kv in remaps)
            {
                if (kv.Key.type == typeof(UnityEngine.Material))
                {
                    if (!usednames.Contains(kv.Key.name))
                    {
                        mi.RemoveRemap(kv.Key);
                        count++;
                    }
                }
            }
            Debug.Log("Removed " + count + " unused material remaps");
        }

        protected virtual void OnEnable()
        {
            GetSelectedMaterial();
        }
        protected virtual void OnSelectionChange()
        {
            GetSelectedMaterial();
        }
        protected virtual void OnProjectChange()
        {
            GetSelectedMaterial();
        }

        private void GetSelectedMaterial()
        {
            m_selectedMaterial = Selection.activeObject as Material;
            if (m_selectedMaterial != null)
            {
                m_serializedObject = new SerializedObject(m_selectedMaterial);
            }

            Repaint();
        }


        protected virtual void OnGUI()
        {
            EditorGUIUtility.labelWidth = 200f;

            if (m_selectedMaterial == null)
            {
                EditorGUILayout.LabelField("No material selected");
            }
            else
            {
                m_serializedObject.Update();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Selected material:", m_selectedMaterial.name);
                EditorGUILayout.LabelField("Shader:", m_selectedMaterial.shader.name);
                EditorGUILayout.LabelField("Keywords: " + m_selectedMaterial.shaderKeywords.Length);

                if (GUILayout.Button("Clear keywords"))
                    ClearKeywords(m_selectedMaterial);

                EditorGUI.indentLevel++;
                for (int i = 0; i < m_selectedMaterial.shaderKeywords.Length; i++)
                {
                    string kwname = m_selectedMaterial.shaderKeywords[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(kwname);
                        if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                        {
                            Undo.RecordObject(m_selectedMaterial, "Material keyword remove");
                            m_selectedMaterial.DisableKeyword(kwname);
                            EditorUtility.SetDirty(m_selectedMaterial);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("Properties:");
                if (GUILayout.Button("Cleanup properties"))
                    RemoveAllUnusedProperties(m_selectedMaterial, m_serializedObject);

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                EditorGUI.indentLevel++;

                ProcessProperties(PropPath_Tex, "Textures", true);
                ProcessProperties(PropPath_Float, "Floats", false);
                ProcessProperties(PropPath_Col, "Colors", false);

                EditorGUI.indentLevel--;
                EditorGUILayout.EndScrollView();
            }

            EditorGUIUtility.labelWidth = 0;
        }

        private void ProcessProperties(string path, string name, bool checkTexture)
        {
            var properties = m_serializedObject.FindProperty(path);
            if (properties != null && properties.isArray)
            {
                int count = properties.arraySize;
                EditorGUILayout.LabelField($"{name}: {count}");
                EditorGUI.indentLevel++;

                for (int i = 0; i < count; i++)
                {
                    string propName = properties.GetArrayElementAtIndex(i).displayName;
                    if (!m_selectedMaterial.HasProperty(propName))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (checkTexture && (m_selectedMaterial.GetTexture(propName) != null))
                                EditorGUILayout.LabelField(propName, "Unused and set", "CN StatusError");
                            else
                                EditorGUILayout.LabelField(propName, "Unused", "CN StatusWarn");
                            if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                            {
                                properties.DeleteArrayElementAtIndex(i);
                                m_serializedObject.ApplyModifiedProperties();
                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        private static int RemoveUnusedProperties(Material mat, SerializedObject serObj, string path)
        {
            int removedprops = 0;
            var properties = serObj.FindProperty(path);
            if (properties != null && properties.isArray)
            {
                int amount = properties.arraySize;
                for (int i = amount-1; i >= 0; i--) // reverse loop because array gets modified
                {
                    string propName = properties.GetArrayElementAtIndex(i).displayName;
                    if (!mat.HasProperty(propName))
                    {
                        properties.DeleteArrayElementAtIndex(i);
                        removedprops++;
                    }
                }
                if (removedprops > 0)
                    serObj.ApplyModifiedProperties();
            }
            return removedprops;
        }
        private static void RemoveAllUnusedProperties(Material mat, SerializedObject serObj)
        {
            if (!mat.shader.isSupported)
            {
                Debug.LogWarning("Skipping \""+mat.name+"\" cleanup because shader is unsupported!");
                return;
            }
            Undo.RecordObject(mat, "Material property cleanup");
            int removedprops = 0;
            removedprops += RemoveUnusedProperties(mat, serObj, PropPath_Tex);
            removedprops += RemoveUnusedProperties(mat, serObj, PropPath_Float);
            removedprops += RemoveUnusedProperties(mat, serObj, PropPath_Col);

            Debug.Log("Removed "+removedprops+" unused properties from "+mat.name);
        }
        private static void ClearKeywords(Material mat)
        {
            Undo.RecordObject(mat, "Material keyword clear");
            string[] keywords = mat.shaderKeywords;
            mat.shaderKeywords = new string[0];
        }
    }
}
