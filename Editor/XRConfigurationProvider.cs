using System;
using System.IO;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor.XR.Management.Metadata;

namespace UnityEditor.XR.Management
{
    internal class XRConfigurationProvider : SettingsProvider
    {
        static readonly GUIContent s_WarningToCreateSettings = EditorGUIUtility.TrTextContent("You must create a serialized instance of the settings data in order to modify the settings in this UI. Until then only default settings set by the provider will be available.");

        Type m_BuildDataType = null;
        string m_BuildSettingsKey;
        Editor m_CachedEditor;
        SerializedObject m_SettingsWrapper;

        public XRConfigurationProvider(string path, string buildSettingsKey, Type buildDataType, SettingsScope scopes = SettingsScope.Project) : base(path, scopes)
        {
            m_BuildDataType = buildDataType;
            m_BuildSettingsKey = buildSettingsKey;
            if (currentSettings == null)
            {
                Create();
            }
        }

        ScriptableObject currentSettings
        {
            get
            {
                ScriptableObject settings = null;
                EditorBuildSettings.TryGetConfigObject(m_BuildSettingsKey, out settings);
                if (settings == null)
                {
                    string searchText = String.Format("t:{0}", m_BuildDataType.Name);
                    string[] assets = AssetDatabase.FindAssets(searchText);
                    foreach (var guid in assets)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);

                        // Check if this asset is from an immutable package
                        var packageInfo = PackageManager.PackageInfo.FindForAssetPath(path);
                        if (packageInfo != null)
                        {
                            switch (packageInfo.source)
                            {
                                case PackageSource.Local:
                                case PackageSource.Embedded:
                                    // Do nothing, local and embedded packages can be edited
                                    break;
                                default:
                                    continue;
                            }
                        }

                        settings = AssetDatabase.LoadAssetAtPath(path, m_BuildDataType) as ScriptableObject;
                        EditorBuildSettings.AddConfigObject(m_BuildSettingsKey, settings, true);

                        break;
                    }
                }
                return settings;
            }
        }

        void InitEditorData(ScriptableObject settings)
        {
            if (settings != null)
            {
                m_SettingsWrapper = new SerializedObject(settings);
                Editor.CreateCachedEditor(settings, null, ref m_CachedEditor);
            }
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            InitEditorData(currentSettings);
        }

        public override void OnDeactivate()
        {
            if(m_CachedEditor != null)
                UnityEngine.Object.DestroyImmediate(m_CachedEditor);
            m_CachedEditor = null;
            m_SettingsWrapper = null;
        }

        public override void OnGUI(string searchContext)
        {
            if (m_SettingsWrapper == null || m_SettingsWrapper.targetObject == null)
            {
                ScriptableObject settings = (currentSettings != null) ? currentSettings : Create();
                InitEditorData(settings);
            }

            if (m_SettingsWrapper != null  && m_SettingsWrapper.targetObject != null && m_CachedEditor != null)
            {
                m_SettingsWrapper.Update();
                m_CachedEditor.OnInspectorGUI();
                m_SettingsWrapper.ApplyModifiedProperties();
            }
        }

        ScriptableObject Create()
        {
            ScriptableObject settings = ScriptableObject.CreateInstance(m_BuildDataType) as ScriptableObject;
            if (settings != null)
            {
                string newAssetName = String.Format("{0}.asset", EditorUtilities.TypeNameToString(m_BuildDataType));
                string assetPath = EditorUtilities.GetAssetPathForComponents(EditorUtilities.s_DefaultSettingsPath);
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogError($"Invalid default settings path");
                    return null;
                }

                assetPath = Path.Combine(assetPath, newAssetName);
                AssetDatabase.CreateAsset(settings, assetPath);
                AssetDatabase.SaveAssets();
                EditorBuildSettings.AddConfigObject(m_BuildSettingsKey, settings, true);

                var package = XRPackageMetadataStore.GetPackageForSettingsTypeNamed(m_BuildDataType.FullName);
                package?.PopulateNewSettingsInstance(settings);
                return settings;
            }
            return null;
        }
    }
}
