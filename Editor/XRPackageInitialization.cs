using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.MPE;
using UnityEditor.XR.Management.Metadata;

namespace UnityEditor.XR.Management
{
    /// <summary>
    /// Interface for specifying package initialization information
    /// </summary>
    public interface XRPackageInitializationBase
    {
        /// <summary>
        /// Package name property
        /// </summary>
        /// <value>The name of the package</value>
        string PackageName { get; }

        /// <summary>
        /// The loader full type name for this package
        /// </summary>
        /// <value>Loader full type name</value>
        string LoaderFullTypeName { get; }

        /// <summary>
        /// The loader type name for this package
        /// </summary>
        /// <value>Loader type name</value>
        string LoaderTypeName { get; }

        /// <summary>
        /// The settings full type name for this package
        /// </summary>
        /// <value>Settings full type name</value>
        ///
        string SettingsFullTypeName { get; }

        /// <summary>
        /// The settings type name for this package
        /// </summary>
        /// <value>Settings type name</value>
        string SettingsTypeName { get; }

        /// <summary>
        /// Package initialization key
        /// </summary>
        /// <value>The init key for the package</value>
        string PackageInitKey { get; }

        /// <summary>
        /// Initialize package settings
        /// </summary>
        /// <param name="obj">The scriptable object instance to initialize</param>
        /// <returns>True if successful, false if not.</returns>
        bool PopulateSettingsOnInitialization(ScriptableObject obj);
    }

    [InitializeOnLoad]
    class XRPackageInitializationBootstrap
    {
        static XRPackageInitializationBootstrap()
        {
            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        static void PlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                case PlayModeStateChange.EnteredEditMode:
                    BeginPackageInitialization();
                    break;
            }
        }

        internal static void BeginPackageInitialization()
        {
            EditorApplication.update -= BeginPackageInitialization;

            // Stops secondary process from doing any asset handling.
            if (ProcessService.level == ProcessLevel.Secondary)
                return;

            foreach (var t in TypeLoaderExtensions.GetAllTypesWithInterface<IXRPackage>())
            {
                if (t.FullName != null && (t.IsInterface
                        || t.FullName.Contains("Unity.XR.Management.TestPackage")
                        || t.FullName.Contains("UnityEditor.XR.Management.Metadata.KnownPackages")))
                    continue;

                if (Activator.CreateInstance(t) is not IXRPackage package)
                {
                    Debug.LogError($"Unable to find an implementation for expected package type {t.FullName}.");
                    continue;
                }
                InitPackage(package);
            }

            foreach (var t in TypeLoaderExtensions.GetAllTypesWithInterface<XRPackageInitializationBase>())
            {
                if (t.IsInterface)
                    continue;

                if (Activator.CreateInstance(t) is not XRPackageInitializationBase packageInit)
                {
                    Debug.LogError($"Unable to find an implementation for expected package type {t.FullName}.");
                    continue;
                }
                InitPackage(packageInit);
            }

            if (XRSettingsManager.Instance != null) XRSettingsManager.Instance.ResetUi = true;
        }

        internal static void InitPackage(IXRPackage package)
        {
            var packageMetadata = package.metadata;
            if (packageMetadata == null)
            {
                Debug.LogError(
                    $"Package {package.GetType().Name} has a package definition but has no metadata. Skipping initialization.");
                return;
            }

            XRPackageMetadataStore.AddPluginPackage(package);

            if (!InitializePackageFromMetadata(package, packageMetadata))
            {
                Debug.LogWarning(
                    $"{packageMetadata.packageName} package Initialization not completed. You will need to create any instances of the loaders and settings manually before you can use the intended XR Plug-in Package.");
            }

        }

        static bool InitializePackageFromMetadata(IXRPackage package, IXRPackageMetadata packageMetadata)
        {
            bool ret = InitializeLoaderFromMetadata(packageMetadata.packageName, packageMetadata.loaderMetadata);
            ret = ret && InitializeSettingsFromMetadata(package, packageMetadata.packageName, packageMetadata.settingsType);
            return ret;
        }

        static bool InitializeLoaderFromMetadata(string packageName, List<IXRLoaderMetadata> loaderMetadatas)
        {
            if (string.IsNullOrEmpty(packageName))
                return false;

            if (loaderMetadatas == null || loaderMetadatas.Count == 0)
            {
                Debug.LogWarning($"Package {packageName} has no loader metadata. Skipping loader initialization.");
                return true;
            }

            foreach (var loader in loaderMetadatas)
            {
                bool hasInstance = EditorUtilities.AssetDatabaseHasInstanceOfType(loader.loaderType);

                if (!hasInstance)
                {
                    var obj = EditorUtilities.CreateScriptableObjectInstance(loader.loaderType,
                        EditorUtilities.GetAssetPathForComponents(EditorUtilities.k_DefaultLoaderPath));
                    hasInstance = obj != null;
                    if (!hasInstance)
                    {
                        Debug.LogError($"Error creating instance of loader {loader.loaderName} for package {packageName}");
                    }
                }
            }

            return true;
        }

        static bool InitializeSettingsFromMetadata(IXRPackage package, string packageName, string settingsType)
        {
            if (string.IsNullOrEmpty(packageName))
                return false;

            if (settingsType == null)
            {
                Debug.LogWarning($"Package {packageName} has no settings metadata. Skipping settings initialization.");
                return true;
            }

            bool ret = EditorUtilities.AssetDatabaseHasInstanceOfType(settingsType);

            if (!ret)
            {
                var obj = EditorUtilities.CreateScriptableObjectInstance( settingsType,
                    EditorUtilities.GetAssetPathForComponents(EditorUtilities.k_DefaultSettingsPath));
                ret = package.PopulateNewSettingsInstance(obj);
            }

            return ret;
        }

        static void InitPackage(XRPackageInitializationBase packageInit)
        {
            if (!InitializeLoaderInstance(packageInit))
            {
                Debug.LogWarning(
                    $"{packageInit.PackageName} Loader Initialization not completed. You will need to create an instance of the loader manually before you can use the intended XR Plug-in Package.");
            }

            if (!InitializeSettingsInstance(packageInit))
            {
                Debug.LogWarning(
                    $"{packageInit.PackageName} Settings Initialization not completed. You will need to create an instance of settings to customize options specific to this package.");
            }
        }

        static bool InitializeLoaderInstance(XRPackageInitializationBase packageInit)
        {
            bool ret = EditorUtilities.AssetDatabaseHasInstanceOfType(packageInit.LoaderTypeName);

            if (!ret)
            {
                var obj = EditorUtilities.CreateScriptableObjectInstance(packageInit.LoaderFullTypeName,
                    EditorUtilities.GetAssetPathForComponents(EditorUtilities.k_DefaultLoaderPath));
                ret = obj != null;
            }

            return ret;
        }

        static bool InitializeSettingsInstance(XRPackageInitializationBase packageInit)
        {
            bool ret = EditorUtilities.AssetDatabaseHasInstanceOfType(packageInit.SettingsTypeName);

            if (!ret)
            {
                var obj = EditorUtilities.CreateScriptableObjectInstance(packageInit.SettingsFullTypeName,
                    EditorUtilities.GetAssetPathForComponents(EditorUtilities.k_DefaultSettingsPath));
                ret = packageInit.PopulateSettingsOnInitialization(obj);
            }

            return ret;
        }

    }
}
