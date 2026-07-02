using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityEditor.XR.Management
{
    class XRPackageInitializationSettings : ScriptableObject
    {
        static XRPackageInitializationSettings s_PackageSettings;
        static object s_Lock = new();

        internal static string s_ProjectSettingsAssetName = "XRPackageSettings.asset";
        internal static string s_ProjectSettingsFolder = "../ProjectSettings";
        internal static string s_ProjectSettingsPath;
        internal static string s_PackageInitPath;

        [SerializeField]
        List<string> m_Settings = new();

        XRPackageInitializationSettings(){ }

        internal static XRPackageInitializationSettings Instance
        {
            get
            {
                if (s_PackageSettings == null)
                {
                    lock(s_Lock)
                    {
                        if (s_PackageSettings == null)
                        {
                            s_PackageSettings = CreateInstance<XRPackageInitializationSettings>();
                            s_PackageSettings.LoadSettings();
                        }
                    }
                }
                return s_PackageSettings;
            }
        }

        static void InitPaths()
        {
            if (string.IsNullOrEmpty(s_ProjectSettingsPath))
            {
                s_ProjectSettingsPath = Path.Combine(Application.dataPath, s_ProjectSettingsFolder);
            }

            if (string.IsNullOrEmpty(s_PackageInitPath))
            {
                s_PackageInitPath = Path.Combine(s_ProjectSettingsPath, s_ProjectSettingsAssetName);
            }
        }

        void OnEnable()
        {
            InitPaths();
        }

        internal void LoadSettings()
        {
            InitPaths();
            if (File.Exists(s_PackageInitPath))
            {
                using var streamReader = new StreamReader(s_PackageInitPath);
                string settings = streamReader.ReadToEnd();
                JsonUtility.FromJsonOverwrite(settings, this);
            }
        }

        internal void SaveSettings()
        {
            InitPaths();
            if (!Directory.Exists(s_ProjectSettingsPath))
                Directory.CreateDirectory(s_ProjectSettingsPath);

            using var streamWriter = new StreamWriter(s_PackageInitPath);
            string settings = JsonUtility.ToJson(this, true);
            streamWriter.Write(settings);
        }

        internal bool HasSettings(string key)
        {
            return m_Settings.Contains(key);
        }

        internal void AddSettings(string key)
        {
            if (!HasSettings(key))
                m_Settings.Add(key);
        }
    }
}
