using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.XR.Management;

namespace UnityEditor.XR.Management
{
    class XRLoaderInfoManager : IXRLoaderOrderManager
    {
        // Simple class to give us updates when the asset database changes.
        internal class AssetCallbacks : AssetPostprocessor
        {
            static bool s_EditorUpdatable;
            internal static Action Callback { get; set; }

            static AssetCallbacks()
            {
                if (!s_EditorUpdatable)
                {
                    EditorApplication.update += EditorUpdatable;
                }
                EditorApplication.projectChanged += EditorApplicationOnProjectChanged;
            }

            static void EditorApplicationOnProjectChanged()
            {
                Callback?.Invoke();
            }

            static void EditorUpdatable()
            {
                s_EditorUpdatable = true;
                EditorApplication.update -= EditorUpdatable;
                Callback?.Invoke();
            }
        }

        SerializedObject m_SerializedObject;
        SerializedProperty m_RequiresSettingsUpdate;
        SerializedProperty m_LoaderList;

        public SerializedObject SerializedObjectData
        {
            get => m_SerializedObject;
            set
            {
                if (m_SerializedObject != value)
                {
                    m_SerializedObject = value;
                    PopulateProperty("m_RequiresSettingsUpdate", ref m_RequiresSettingsUpdate);
                    PopulateProperty("m_Loaders", ref m_LoaderList);
                    ShouldReload = true;
                }
            }
        }

        List<XRLoaderInfo> m_AllLoaderInfos = new();
        List<XRLoaderInfo> m_AllLoaderInfosForBuildTarget = new();
        List<XRLoaderInfo> m_AssignedLoaderInfos = new();

        BuildTargetGroup m_BuildTargetGroup = BuildTargetGroup.Unknown;
        internal BuildTargetGroup BuildTarget
        {
            get => m_BuildTargetGroup;
            set
            {
                if (m_BuildTargetGroup != value)
                {
                    m_BuildTargetGroup = value;
                    ShouldReload = true;
                }
            }
        }

        void AssetProcessorCallback()
        {
            ShouldReload = true;
        }

        public void OnEnable()
        {
            AssetCallbacks.Callback += AssetProcessorCallback;
            ShouldReload = true;
        }

        public bool ShouldReload
        {
            get
            {
                if (m_RequiresSettingsUpdate != null)
                {
                    SerializedObjectData.Update();
                    return m_RequiresSettingsUpdate.boolValue;
                }
                return false;
            }
            set
            {
                if (m_RequiresSettingsUpdate != null && m_RequiresSettingsUpdate.boolValue != value)
                {
                    m_RequiresSettingsUpdate.boolValue = value;
                    SerializedObjectData.ApplyModifiedProperties();
                }
            }
        }

        public void OnDisable()
        {
            AssetCallbacks.Callback -= null;
        }

        public void ReloadData()
        {
            if (m_LoaderList == null)
                return;

            PopulateAllLoaderInfos();
            PopulateLoadersForBuildTarget();
            PopulateAssignedLoaderInfos();

            ShouldReload = false;
        }

        void PopulateAllLoaderInfos()
        {
            m_AllLoaderInfos.Clear();
            XRLoaderInfo.GetAllKnownLoaderInfos(m_AllLoaderInfos);
        }

        void CleanupLostAssignedLoaders()
        {
            var missingLoaders = from info in m_AssignedLoaderInfos
                                 where info.instance == null
                                 select info;

            if (missingLoaders.Any())
            {
                m_AssignedLoaderInfos = m_AssignedLoaderInfos.Except(missingLoaders).ToList();
            }
        }

        void PopulateAssignedLoaderInfos()
        {
            m_AssignedLoaderInfos.Clear();
            for (int i = 0; i < m_LoaderList.arraySize; i++)
            {
                var prop = m_LoaderList.GetArrayElementAtIndex(i);

                XRLoaderInfo info = new XRLoaderInfo();
                info.loaderType = (prop.objectReferenceValue == null) ? null : prop.objectReferenceValue.GetType();
                info.assetName = AssetNameFromInstance(prop.objectReferenceValue);
                info.instance = prop.objectReferenceValue as XRLoader;

                m_AssignedLoaderInfos.Add(info);
            }
            CleanupLostAssignedLoaders();
        }

        static string AssetNameFromInstance(UnityEngine.Object asset)
        {
            if (asset == null)
                return "";

            string assetPath = AssetDatabase.GetAssetPath(asset);
            return Path.GetFileNameWithoutExtension(assetPath);
        }

        void PopulateLoadersForBuildTarget()
        {
            m_AllLoaderInfosForBuildTarget = FilteredLoaderInfos(m_AllLoaderInfos);
        }

        void PopulateProperty(string propertyPath, ref SerializedProperty prop)
        {
            if (SerializedObjectData != null && prop == null) prop = SerializedObjectData.FindProperty(propertyPath);
        }

        List<XRLoaderInfo> FilteredLoaderInfos(List<XRLoaderInfo> loaderInfos)
        {
            var ret = new List<XRLoaderInfo>();

            foreach (var info in loaderInfos)
            {
                if (info.loaderType == null)
                    continue;

                object[] attrs;

                try
                {
                    attrs = info.loaderType.GetCustomAttributes(typeof(XRSupportedBuildTargetAttribute), true);
                }
                catch (Exception)
                {
                    attrs = null;
                }

                if (attrs == null)
                    continue;

                if (attrs.Length == 0)
                {
                    // If unmarked we assume it will be applied to all build targets.
                    ret.Add(info);
                }
                else
                {
                    foreach (XRSupportedBuildTargetAttribute attr in attrs)
                    {
                        if (attr.buildTargetGroup == m_BuildTargetGroup)
                        {
                            ret.Add(info);
                            break;
                        }
                    }
                }
            }

            return ret;
        }

        void UpdateSerializedProperty()
        {
            if (m_LoaderList != null && m_LoaderList.isArray)
            {
                m_LoaderList.ClearArray();

                int index = 0;
                foreach (XRLoaderInfo info in m_AssignedLoaderInfos)
                {
                    m_LoaderList.InsertArrayElementAtIndex(index);
                    var prop = m_LoaderList.GetArrayElementAtIndex(index);
                    prop.objectReferenceValue = info.instance;
                    index++;
                }
            }

            SerializedObjectData.ApplyModifiedProperties();
        }

        #region IXRLoaderOrderManager
        List<XRLoaderInfo> IXRLoaderOrderManager.AssignedLoaders => m_AssignedLoaderInfos;

        void IXRLoaderOrderManager.AssignLoader(XRLoaderInfo assignedInfo)
        {
            m_AssignedLoaderInfos.Add(assignedInfo);
            UpdateSerializedProperty();
            ShouldReload = true;
        }

        void IXRLoaderOrderManager.Update()
        {
            UpdateSerializedProperty();
            ShouldReload = true;
        }

        #endregion
    }
}
