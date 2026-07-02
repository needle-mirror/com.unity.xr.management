using System;
using System.Collections.Generic;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace UnityEditor.XR.Management
{
    class XRSettingsManager : SettingsProvider
    {
        internal static class Styles
        {
            public static readonly GUIStyle k_UrlLabelPersonal = new(EditorStyles.label)
            {
                name = "url-label",
                richText = true,
                normal = new GUIStyleState { textColor = new Color(8 / 255f, 8 / 255f, 252 / 255f) },
            };

            public static readonly GUIStyle k_UrlLabelProfessional = new(EditorStyles.label)
            {
                name = "url-label",
                richText = true,
                normal = new GUIStyleState { textColor = new Color(79 / 255f, 128 / 255f, 248 / 255f) },
            };

            public static readonly GUIStyle k_LabelWordWrap = new(EditorStyles.label) { wordWrap = true };

            public static readonly GUIStyle k_HelpBox = new(EditorStyles.helpBox)
            {
                fixedWidth = 376,
                padding = new RectOffset(8, 8, 6, 6)
            };

            public static readonly GUIStyle k_HelpBoxContent = new()
            {
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(0, 0, 0, 8)
            };

            public static readonly GUIStyle k_HelpBoxActions = new()
            {
                alignment = TextAnchor.MiddleRight
            };

            public static readonly GUIStyle k_Icon = new ()
            {
                fixedWidth = 16,
                fixedHeight = 16,
                margin = new RectOffset(0, 4, 0, 0)
            };
        }

        struct Content
        {
            public static readonly GUIContent k_InitializeOnStart = new("Initialize XR on Startup");
            public static readonly GUIContent k_XRConfigurationText = new("Information about configuration and tracking can be found below.");
            public static readonly GUIContent k_XRConfigurationDocUriText = new("View Documentation");
            public static readonly Uri k_XRConfigurationUri = new(
                $"https://docs.unity3d.com/{Application.unityVersion[..Application.unityVersion.LastIndexOf(".")]}/Documentation/Manual/configuring-project-for-xr.html");
            public static readonly GUIContent k_EditorTargetPlatform = new("Editor Play mode uses Desktop Platform Settings regardless of Active Build Target.");
            public static readonly GUIContent k_InfoIcon = EditorGUIUtility.IconContent("d_console.infoicon");
        }

        internal static GUIStyle GetStyle(string styleName)
        {
            var s = GUI.skin.FindStyle(styleName) ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(styleName);
            if (s == null)
            {
                Debug.LogError("Missing built-in guistyle " + styleName);
                s = GUI.skin.box;
            }
            return s;
        }

        static string s_SettingsRootTitle = $"Project/{XRConstants.k_XRPluginManagement}";
        static XRSettingsManager s_SettingsManager = null;

        internal static XRSettingsManager Instance => s_SettingsManager;

        bool m_ResetUi;
        internal bool ResetUi
        {
            get => m_ResetUi;
            set
            {
                m_ResetUi = value;
                if (m_ResetUi)
                    Repaint();
            }
        }

        SerializedObject m_SettingsWrapper;

        Dictionary<BuildTargetGroup, XRManagerSettingsEditor> m_CachedSettingsEditor = new();

        BuildTargetGroup m_LastBuildTargetGroup = BuildTargetGroup.Unknown;

        static XRGeneralSettingsPerBuildTarget currentSettings => XRGeneralSettingsPerBuildTarget.GetOrCreate();

        [UnityEngine.Internal.ExcludeFromDocs]
        XRSettingsManager(string path, SettingsScope scopes = SettingsScope.Project) : base(path, scopes)
        { }

        [SettingsProvider]
        [UnityEngine.Internal.ExcludeFromDocs]
        static SettingsProvider Create()
        {
            s_SettingsManager ??= new XRSettingsManager(s_SettingsRootTitle);
            return s_SettingsManager;
        }

        [SettingsProviderGroup]
        [UnityEngine.Internal.ExcludeFromDocs]
        static SettingsProvider[] CreateAllChildSettingsProviders()
        {
            var ret = new List<SettingsProvider>();
            if (s_SettingsManager != null)
            {
                var types = TypeLoaderExtensions.GetAllTypesWithAttribute<XRConfigurationDataAttribute>();
                foreach (var type in types)
                {
                    if (type.FullName != null && type.FullName.Contains("Unity.XR.Management.TestPackage"))
                        continue;

                    if (type.GetCustomAttributes(typeof(XRConfigurationDataAttribute), true)[0] is XRConfigurationDataAttribute attr)
                    {
                        string settingsPath = String.Format("{1}/{0}", attr.displayName, s_SettingsRootTitle);
                        var resProv = new XRConfigurationProvider(settingsPath, attr.buildSettingsKey, type);
                        ret.Add(resProv);
                    }
                }
            }

            return ret.ToArray();
        }

        void InitEditorData(ScriptableObject settings)
        {
            if (settings != null)
            {
                m_SettingsWrapper = new SerializedObject(settings);
            }
        }

        /// <summary>
        /// See <see href="https://docs.unity3d.com/ScriptReference/SettingsProvider.html">SettingsProvider documentation</see>.
        /// </summary>
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            InitEditorData(currentSettings);
        }

        /// <summary>
        /// See <see href="https://docs.unity3d.com/ScriptReference/SettingsProvider.html">SettingsProvider documentation</see>.
        /// </summary>
        public override void OnDeactivate()
        {
            m_SettingsWrapper = null;
            m_CachedSettingsEditor.Clear();
        }

        void DisplayLoaderSelectionUI()
        {
            var buildTargetGroup = EditorGUILayout.BeginBuildTargetSelectionGrouping();

            try
            {
                bool buildTargetChanged = m_LastBuildTargetGroup != buildTargetGroup;
                if (buildTargetChanged)
                    m_LastBuildTargetGroup = buildTargetGroup;

                if (!currentSettings.HasManagerSettingsForBuildTarget(buildTargetGroup))
                {
                    currentSettings.CreateDefaultManagerSettingsForBuildTarget(buildTargetGroup);
                }
                XRGeneralSettings settings = currentSettings.SettingsForBuildTarget(buildTargetGroup);

                var serializedSettingsObject = new SerializedObject(settings);
                serializedSettingsObject.Update();

                var initOnStart = serializedSettingsObject.FindProperty(nameof(settings.m_InitManagerOnStart));
                EditorGUILayout.PropertyField(initOnStart, Content.k_InitializeOnStart);
                EditorGUILayout.Space();

                if (BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget) != BuildTargetGroup.Standalone)
                {
                    EditorGUILayout.HelpBox(Content.k_EditorTargetPlatform.text, MessageType.Info);
                    EditorGUILayout.Space();
                }

                var loaderProp = serializedSettingsObject.FindProperty(nameof(settings.m_Manager));
                var obj = loaderProp.objectReferenceValue;

                if (obj != null)
                {
                    loaderProp.objectReferenceValue = obj;

                    m_CachedSettingsEditor.TryAdd(buildTargetGroup, null);

                    if (m_CachedSettingsEditor[buildTargetGroup] == null)
                    {
                        m_CachedSettingsEditor[buildTargetGroup] = Editor.CreateEditor(obj) as XRManagerSettingsEditor;

                        if (m_CachedSettingsEditor[buildTargetGroup] == null)
                        {
                            Debug.LogError("Failed to create a view for XR Manager Settings Instance");
                        }
                    }

                    if (m_CachedSettingsEditor[buildTargetGroup] != null)
                    {
                        if (ResetUi)
                        {
                            ResetUi = false;
                            m_CachedSettingsEditor[buildTargetGroup].Reload();
                        }

                        m_CachedSettingsEditor[buildTargetGroup].BuildTarget = buildTargetGroup;
                        m_CachedSettingsEditor[buildTargetGroup].OnInspectorGUI();
                    }
                }
                else if (obj == null)
                {
                    settings.Manager = null;
                    loaderProp.objectReferenceValue = null;
                }

                serializedSettingsObject.ApplyModifiedProperties();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error trying to display plug-in assignment UI : {ex.Message}");
            }

            EditorGUILayout.EndBuildTargetSelectionGrouping();
        }

        static void DisplayLink(GUIContent text, Uri link, int leftMargin)
        {
            var labelStyle = EditorGUIUtility.isProSkin ? Styles.k_UrlLabelProfessional : Styles.k_UrlLabelPersonal;
            var size = labelStyle.CalcSize(text);
            var uriRect = GUILayoutUtility.GetRect(text, labelStyle);
            uriRect.x += leftMargin;
            uriRect.width = size.x;
            if (GUI.Button(uriRect, text, labelStyle))
            {
                System.Diagnostics.Process.Start(link.AbsoluteUri);
            }
            EditorGUIUtility.AddCursorRect(uriRect, MouseCursor.Link);
        }

        static void DisplayXRTrackingDocumentationLink()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.BeginHorizontal(Styles.k_HelpBoxContent);
                {
                    GUILayout.Label(Content.k_InfoIcon, Styles.k_Icon);
                    EditorGUILayout.LabelField(Content.k_XRConfigurationText, Styles.k_LabelWordWrap);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(Styles.k_HelpBoxActions);
                {
                    GUILayout.FlexibleSpace();
                    DisplayLink(Content.k_XRConfigurationDocUriText, Content.k_XRConfigurationUri, 2);
                }
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        void DisplayLoadOrderUi()
        {
            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(
                XRPackageMetadataStore.isDoingQueueProcessing
                || EditorApplication.isPlaying
                || EditorApplication.isPaused);

            if (m_SettingsWrapper != null && m_SettingsWrapper.targetObject != null)
            {
                m_SettingsWrapper.Update();

                EditorGUILayout.Space();

                DisplayLoaderSelectionUI();

                m_SettingsWrapper.ApplyModifiedProperties();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();
        }

        /// <summary>
        /// See <see href="https://docs.unity3d.com/ScriptReference/SettingsProvider.html">SettingsProvider documentation</see>.
        /// </summary>
        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space();

            DisplayLoadOrderUi();
            DisplayXRTrackingDocumentationLink();

            base.OnGUI(searchContext);
        }
    }
}
