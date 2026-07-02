using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor.MPE;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine.XR.Management;

namespace UnityEditor.XR.Management.Metadata
{
    /// <summary>
    /// Provide access to the metadata store. Currently only usable as a way to assign and remove loaders
    /// to/from an <see cref="XRManagerSettings"/> instance.
    /// </summary>
    [InitializeOnLoad]
    public class XRPackageMetadataStore
    {
        enum InstallationState
        {
            New,
            RebuildInstalledCache,
            StartInstallation,
            Installing,
            Assigning,
            Complete,
            Uninstalling,
            Log
        }

        enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        [Serializable]
        struct LoaderAssignmentRequest : IEquatable<LoaderAssignmentRequest>
        {
            [SerializeField]
            internal string packageId;

            [SerializeField]
            internal string loaderType;

            [SerializeField]
            internal BuildTargetGroup buildTargetGroup;

            [SerializeField]
            internal bool needsAddRequest;

            [SerializeField]
            internal ListRequest packageListRequest;

            [SerializeField]
            internal AddRequest packageAddRequest;

            [SerializeField]
#pragma warning disable CS0649
            internal RemoveRequest packageRemoveRequest;
#pragma warning disable CS0649

            [SerializeField]
            internal float timeOut;

            [SerializeField]
            internal InstallationState installationState;

            [SerializeField]
            internal string logMessage;

            [SerializeField]
            internal LogLevel logLevel;

            public bool Equals(LoaderAssignmentRequest other)
            {
                return packageId == other.packageId
                    && loaderType == other.loaderType
                    && buildTargetGroup == other.buildTargetGroup
                    && needsAddRequest == other.needsAddRequest
                    && Equals(packageListRequest, other.packageListRequest)
                    && Equals(packageAddRequest, other.packageAddRequest)
                    && Equals(packageRemoveRequest, other.packageRemoveRequest)
                    && timeOut.Equals(other.timeOut)
                    && installationState == other.installationState
                    && logMessage == other.logMessage
                    && logLevel == other.logLevel;
            }

            public override bool Equals(object obj)
            {
                return obj is LoaderAssignmentRequest other && Equals(other);
            }

            public override int GetHashCode()
            {
                var hashCode = new HashCode();
                hashCode.Add(packageId);
                hashCode.Add(loaderType);
                hashCode.Add((int)buildTargetGroup);
                hashCode.Add(needsAddRequest);
                hashCode.Add(packageListRequest);
                hashCode.Add(packageAddRequest);
                hashCode.Add(packageRemoveRequest);
                hashCode.Add(timeOut);
                hashCode.Add((int)installationState);
                hashCode.Add(logMessage);
                hashCode.Add((int)logLevel);
                return hashCode.ToHashCode();
            }
        }

        [Serializable]
        struct CachedMDStoreInformation
        {
            internal bool hasAlreadyRequestedData;
            internal KnownPackageInfo[] knownPackageInfos;
            internal string[] installedPackages;
            internal string[] installablePackages;
        }

        [Serializable]
        struct KnownPackageInfo
        {
            internal string packageId;
            internal string verifiedVersion;
        }

        [Serializable]
        struct LoaderAssignmentRequests
        {
            [SerializeField]
            internal List<LoaderAssignmentRequest> activeRequests;
        }

        const string k_WaitingPackmanQuery = "XRMGT Waiting Packman Query.";
        const string k_RebuildCache = "XRMGT Rebuilding Cache.";
        const string k_InstallingPackage = "XRMGT Installing XR Package.";
        const string k_AssigningPackage = "XRMGT Assigning XR Package.";
        const string k_UninstallingPackage = "XRMGT Uninstalling XR Package.";
        const string k_CachedMDStoreKey = "XR Metadata Store";

        const float k_TimeOutDelta = 30f;
        const string k_DefaultSessionStateString = "DEADBEEF";

        static CachedMDStoreInformation s_CachedMDStoreInformation = new()
        {
            hasAlreadyRequestedData = false,
        };

        static void LoadCachedMDStoreInformation()
        {
            string data = SessionState.GetString(k_CachedMDStoreKey, "{}");
            s_CachedMDStoreInformation = JsonUtility.FromJson<CachedMDStoreInformation>(data);
        }

        static void StoreCachedMDStoreInformation()
        {
            SessionState.EraseString(k_CachedMDStoreKey);
            string data = JsonUtility.ToJson(s_CachedMDStoreInformation, true);
            SessionState.SetString(k_CachedMDStoreKey, data);
        }

        static Dictionary<string, IXRPackage> s_Packages;
        static SearchRequest s_SearchRequest;

        static Dictionary<string, IXRPackage> packages
        {
            get
            {
                if(s_Packages == null)
                {
                    s_Packages = new Dictionary<string, IXRPackage>();
                    InitKnownPluginPackages();
                    XRPackageInitializationBootstrap.BeginPackageInitialization();
                }

                return s_Packages;
            }
        }

        static bool SessionStateHasStoredData(string queueName)
        {
            return SessionState.GetString(queueName, k_DefaultSessionStateString) != XRPackageMetadataStore.k_DefaultSessionStateString;
        }

        internal static bool isCheckingInstallationRequirements => SessionStateHasStoredData(k_WaitingPackmanQuery);
        internal static bool isRebuildingCache => SessionStateHasStoredData(k_RebuildCache);
        internal static bool isInstallingPackages => SessionStateHasStoredData(k_InstallingPackage);
        internal static bool isUninstallingPackages => SessionStateHasStoredData(k_UninstallingPackage);
        internal static bool isAssigningLoaders => SessionStateHasStoredData(k_AssigningPackage);

        internal static bool isDoingQueueProcessing
            => isCheckingInstallationRequirements
                || isRebuildingCache
                || isInstallingPackages
                || isUninstallingPackages
                || isAssigningLoaders;

        internal struct LoaderBuildTargetQueryResult : IEquatable<LoaderBuildTargetQueryResult>
        {
            public string packageName;
            public string packageId;
            public string loaderName;
            public string loaderType;
            public bool disabled;

            public LoaderBuildTargetQueryResult(IXRPackageMetadata pm, IXRLoaderMetadata lm)
            {
                packageName = pm.packageName;
                packageId = pm.packageId;
                loaderName = lm.loaderName;
                loaderType = lm.loaderType;
                disabled = false;
                if (pm is KnownPackages.KnownPackageMetadata knownMetadata)
                    disabled = knownMetadata.disabled;
            }

            public bool Equals(LoaderBuildTargetQueryResult other)
            {
                return packageName == other.packageName
                    && packageId == other.packageId
                    && loaderName == other.loaderName
                    && loaderType == other.loaderType
                    && disabled == other.disabled;
            }

            public override bool Equals(object obj)
            {
                return obj is LoaderBuildTargetQueryResult other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(packageName, packageId, loaderName, loaderType, disabled);
            }
        }

        internal static void MoveMockInListToEnd(List<LoaderBuildTargetQueryResult> loaderList)
        {
            int index = loaderList.FindIndex(x => string.Compare(x.loaderType, KnownPackages.k_KnownPackageMockHmdLoader) == 0);
            if (index >= 0)
            {
                var mock = loaderList[index];
                loaderList.RemoveAt(index);
                loaderList.Add(mock);
            }
        }

        internal static List<LoaderBuildTargetQueryResult> GetAllLoadersForBuildTarget(BuildTargetGroup buildTarget)
        {
            var ret = from pm in from p in packages.Values select p.metadata
                      from lm in pm.loaderMetadata
                      where lm.supportedBuildTargets.Contains(buildTarget)
                      orderby lm.loaderName
                      select new LoaderBuildTargetQueryResult(pm, lm);
            var retList = ret.Distinct().ToList();
            MoveMockInListToEnd(retList);
            return retList;
        }


        internal static List<LoaderBuildTargetQueryResult> GetLoadersForBuildTarget(BuildTargetGroup buildTargetGroup)
        {
            var ret = from pm in from p in packages.Values select p.metadata
                      from lm in pm.loaderMetadata
                      where lm.supportedBuildTargets.Contains(buildTargetGroup)
                      orderby lm.loaderName
                      select new LoaderBuildTargetQueryResult(pm, lm);
            var retList = ret.ToList();
            MoveMockInListToEnd(retList);
            return retList;
        }

        /// <summary>
        /// Return a read only list of all package metadata information currently known.
        /// </summary>
        /// <returns>Read only list of <see cref="IXRPackage" />.</returns>
        public static IReadOnlyList<IXRPackage> GetAllPackageMetadata()
        {
            return packages.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// Return a read only list of all package metadata information currently known that has loaders that support the given build.
        /// </summary>
        /// <param name="buildTargetGroup">
        /// The <c>BuildTargetGroup</c> used to filter the package Metadata.
        /// </param>
        /// <returns>Read only list of <see cref="IXRPackage" />.</returns>
        public static IReadOnlyList<IXRPackage> GetAllPackageMetadataForBuildTarget(BuildTargetGroup buildTargetGroup)
        {
            var ret = new HashSet<IXRPackage>();
            foreach (var p in packages.Values)
            {
                foreach (var lm in p.metadata.loaderMetadata)
                {
                    if (lm.supportedBuildTargets.Contains(buildTargetGroup))
                    {
                        ret.Add(p);
                        break;
                    }
                }
            }

            return ret.ToList().AsReadOnly();
        }

        /// <summary>
        /// Given a package id, return the metadata for that package.
        /// </summary>
        /// <param name="packageId">The package id to check for.</param>
        /// <returns>An instance of <see cref="IXRPackageMetadata" /> if the package has metadata or null.</returns>
        public static IXRPackageMetadata GetMetadataForPackage(string packageId)
        {
            return packages.Values.
                Select(x => x.metadata).
                FirstOrDefault(xmd => String.Compare(xmd.packageId, packageId) == 0);
        }

        internal static bool HasInstallablePackageData()
        {
            return s_CachedMDStoreInformation.installablePackages?.Any() ?? false;
        }

        internal static bool IsPackageInstalled(string package)
        {
            return (s_CachedMDStoreInformation.installedPackages?.Contains(package) ?? false)
                && File.Exists($"Packages/{package}/package.json");
        }

        internal static bool IsPackageInstallable(string package)
        {
            return s_CachedMDStoreInformation.installablePackages?.Contains(package) ?? false;
        }

        /// <summary>
        /// Given a loader type and a build target group will return whether or not that loader
        /// is currently assigned to be active for that build target.
        /// </summary>
        /// <param name="loaderTypeName">Loader type to check.</param>
        /// <param name="buildTargetGroup">Build target group to check for assignment in.</param>
        /// <returns>True if the loader is assigned.</returns>
        public static bool IsLoaderAssigned(string loaderTypeName, BuildTargetGroup buildTargetGroup)
        {

            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(buildTargetGroup);
            if (settings == null)
                return false;

            foreach (var loader in settings.Manager.activeLoaders)
            {
                if (loader != null && String.Compare(loader.GetType().FullName, loaderTypeName) == 0)
                    return true;
            }
            return false;
        }

        internal static bool IsLoaderAssigned(XRManagerSettings settings, string loaderTypeName)
        {
            if (settings == null)
                return false;

            foreach (var l in settings.activeLoaders)
            {
                if (l != null && string.Compare(l.GetType().FullName, loaderTypeName) == 0)
                    return true;
            }
            return false;
        }

        internal static void InstallPackageAndAssignLoaderForBuildTarget(
            string package, string loaderType, BuildTargetGroup buildTargetGroup)
        {
            var req = new LoaderAssignmentRequest();
            req.packageId = package;
            req.loaderType = loaderType;
            req.buildTargetGroup = buildTargetGroup;
            req.installationState = InstallationState.New;
            QueueLoaderRequest(req);
        }

        /// <summary>
        /// Assigns a loader of type loaderTypeName to the settings instance. Will instantiate an
        /// instance if one can't be found in the users project folder before assigning it.
        /// </summary>
        /// <param name="settings">An instance of <see cref="XRManagerSettings"/> to add the loader to.</param>
        /// <param name="loaderTypeName">The full type name for the loader instance to assign to settings.</param>
        /// <param name="buildTargetGroup">The build target group being assigned to.</param>
        /// <returns>True if assignment succeeds, false if not.</returns>
        public static bool AssignLoader(XRManagerSettings settings, string loaderTypeName, BuildTargetGroup buildTargetGroup)
        {
            // Stops secondary process from doing any asset handling.
            if (ProcessService.level == ProcessLevel.Secondary)
                return false;

            if (EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                Debug.LogError(
                    $"Attempt to add {loaderTypeName} for {buildTargetGroup} while in Play mode. XR Plug-in Management can not make changes to the loader list when running.");
                return false;
            }

            var instance = EditorUtilities.GetInstanceOfTypeWithNameFromAssetDatabase(loaderTypeName);
            if (instance == null || !(instance is XRLoader))
            {
                instance = EditorUtilities.CreateScriptableObjectInstance(loaderTypeName,
                    EditorUtilities.GetAssetPathForComponents(EditorUtilities.k_DefaultLoaderPath));
                if (instance == null)
                    return false;
            }

            var newLoader = instance as XRLoader;
            if (settings.TryAddLoader(newLoader))
            {
                var assignedLoaders = settings.activeLoaders;
                var orderedLoaders = new List<XRLoader>();
                var allLoaders = GetAllLoadersForBuildTarget(buildTargetGroup);
                foreach (var ldr in allLoaders)
                {
                    var newInstance = EditorUtilities.GetInstanceOfTypeWithNameFromAssetDatabase(ldr.loaderType) as XRLoader;

                    if (newInstance != null && assignedLoaders.Contains(newInstance))
                    {
                        orderedLoaders.Add(newInstance);
                        var loaderHelper = newLoader as XRLoaderHelper;
                        loaderHelper?.WasAssignedToBuildTarget(buildTargetGroup);
                    }
                }

                if (!settings.TrySetLoaders(orderedLoaders))
                    return false;

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            return true;
        }

        /// <summary>
        /// Remove a previously assigned loader from settings. If the loader type is unknown or
        /// an instance of the loader can't be found in the project folder no action is taken.
        ///
        /// Removal will not delete the instance from the project folder.
        /// </summary>
        /// <param name="settings">An instance of <see cref="XRManagerSettings"/> to add the loader to.</param>
        /// <param name="loaderTypeName">The full type name for the loader instance to remove from settings.</param>
        /// <param name="buildTargetGroup">The build target group being removed from.</param>
        /// <returns>True if removal succeeds, false if not.</returns>
        public static bool RemoveLoader(XRManagerSettings settings, string loaderTypeName, BuildTargetGroup buildTargetGroup)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                Debug.LogError($"Attempt to remove {loaderTypeName} for {buildTargetGroup} while in Play mode. XR Plug-in Management can not make changes to the loader list when running.");
                return false;
            }

            var instance = EditorUtilities.GetInstanceOfTypeWithNameFromAssetDatabase(loaderTypeName);
            if (instance == null || !(instance is XRLoader))
                return false;

            var loader = instance as XRLoader;

            if (settings.TryRemoveLoader(loader))
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                var loaderHelper = loader as XRLoaderHelper;
                loaderHelper?.WasUnassignedFromBuildTarget(buildTargetGroup);
                return true;
            }

            return false;
        }

        internal static IXRPackage GetPackageForSettingsTypeNamed(string settingsTypeName)
        {
            var ret = packages.Values.
                Where(p => String.Compare(p.metadata.settingsType, settingsTypeName, true) == 0).
                Select(p => p);
            return ret.Any() ? ret.First() : null;

        }

        internal static string GetCurrentStatusDisplayText()
        {
            if (isCheckingInstallationRequirements)
                return "Checking installation requirements for packages...";

            if (isRebuildingCache)
                return "Querying Package Manager for currently installed packages...";

            if (isInstallingPackages)
                return "Installing packages...";

            if (isUninstallingPackages)
                return "Uninstalling packages...";

            if (isAssigningLoaders)
                return "Assigning all requested loaders...";

            return "";
        }

        internal static void AddPluginPackage(IXRPackage package)
        {
            if (s_CachedMDStoreInformation.installedPackages != null
                && !s_CachedMDStoreInformation.installedPackages.Contains(package.metadata.packageId))
            {
                var installedPackages = s_CachedMDStoreInformation.installedPackages.ToList();
                installedPackages.Add(package.metadata.packageId);
                s_CachedMDStoreInformation.installedPackages = installedPackages.ToArray();
                StoreCachedMDStoreInformation();
            }
            InternalAddPluginPackage(package);
        }

        static void InternalAddPluginPackage(IXRPackage package)
        {
            packages[package.metadata.packageId] = package;
        }

        internal static void InitKnownPluginPackages()
        {
            foreach (var knownPackage in KnownPackages.Packages)
            {
                InternalAddPluginPackage(knownPackage);
            }
        }

        static XRPackageMetadataStore()
        {
            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            EditorApplication.playModeStateChanged += PlayModeStateChanged;

            if (IsEditorInPlayMode())
                return;

            AssemblyReloadEvents.afterAssemblyReload += AssemblyReloadEvents_afterAssemblyReload;
        }


        static void AssemblyReloadEvents_afterAssemblyReload()
        {
            LoadCachedMDStoreInformation();

            if (!IsEditorInPlayMode())
            {
                if (!s_CachedMDStoreInformation.hasAlreadyRequestedData)
                {
                    s_SearchRequest = Client.SearchAll(true);
                }

                RebuildInstalledCache();
                StartAllQueues();
            }
        }

        static bool IsEditorInPlayMode()
        {
            return EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isPlaying ||
                EditorApplication.isPaused;
        }

        static void PlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    StopAllQueues();
                    StoreCachedMDStoreInformation();
                    break;

                case PlayModeStateChange.EnteredPlayMode:
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    LoadCachedMDStoreInformation();
                    StartAllQueues();
                    break;
            }
        }

        static void StopAllQueues()
        {
            EditorApplication.update -= UpdateInstallablePackages;
            EditorApplication.update -= WaitingOnSearchQuery;
            EditorApplication.update -= MonitorPackageInstallation;
            EditorApplication.update -= MonitorPackageUninstall;
            EditorApplication.update -= AssignAnyRequestedLoadersUpdate;
            EditorApplication.update -= RebuildCache;

        }

        static void StartAllQueues()
        {
            EditorApplication.update += UpdateInstallablePackages;
            EditorApplication.update += WaitingOnSearchQuery;
            EditorApplication.update += MonitorPackageInstallation;
            EditorApplication.update += MonitorPackageUninstall;
            EditorApplication.update += AssignAnyRequestedLoadersUpdate;
            EditorApplication.update += RebuildCache;
        }

        static void UpdateInstallablePackages()
        {
            EditorApplication.update -= UpdateInstallablePackages;

            if (s_SearchRequest == null || IsEditorInPlayMode() || s_CachedMDStoreInformation.hasAlreadyRequestedData)
            {
                return;
            }

            if (s_SearchRequest.IsCompleted && s_SearchRequest.Error != null)
            {
                Debug.LogError($"Error retrieving package information from Package Manager: {s_SearchRequest.Error.message}.");
                s_SearchRequest = null;
                return;
            }

            if (!s_SearchRequest.IsCompleted || s_SearchRequest.Result == null)
            {
                EditorApplication.update += UpdateInstallablePackages;
                return;
            }

            var installablePackages = new List<string>();
            var knownPackageInfos = new List<KnownPackageInfo>();

            foreach (var package in s_SearchRequest.Result)
            {
                if (packages.ContainsKey(package.name))
                {
                    var kpi = new KnownPackageInfo();
                    kpi.packageId = package.name;

                    kpi.verifiedVersion = package.versions.recommended;
                    if (string.IsNullOrEmpty(kpi.verifiedVersion))
                        kpi.verifiedVersion = package.versions.latestCompatible;
                    knownPackageInfos.Add(kpi);
                    installablePackages.Add(package.name);
                }
            }

            s_CachedMDStoreInformation.knownPackageInfos = knownPackageInfos.ToArray();
            s_CachedMDStoreInformation.installablePackages = installablePackages.ToArray();
            s_CachedMDStoreInformation.hasAlreadyRequestedData = true;

            s_SearchRequest = null;

            StoreCachedMDStoreInformation();
        }

        static void AddRequestToQueue(LoaderAssignmentRequest request, string queueName)
        {
            LoaderAssignmentRequests requests;

            if (SessionStateHasStoredData(queueName))
            {
                string fromJson = SessionState.GetString(queueName, k_DefaultSessionStateString);
                requests = JsonUtility.FromJson<LoaderAssignmentRequests>(fromJson);
            }
            else
            {
                requests = new LoaderAssignmentRequests();
                requests.activeRequests = new List<LoaderAssignmentRequest>();
            }

            requests.activeRequests.Add(request);
            string json = JsonUtility.ToJson(requests);
            SessionState.SetString(queueName, json);

        }

        static void SetRequestsInQueue(LoaderAssignmentRequests requests, string queueName)
        {
            string json = JsonUtility.ToJson(requests);
            SessionState.SetString(queueName, json);
        }

        static LoaderAssignmentRequests GetAllRequestsInQueue(string queueName)
        {
            var reqs = new LoaderAssignmentRequests();
            reqs.activeRequests = new List<LoaderAssignmentRequest>();

            if (SessionStateHasStoredData(queueName))
            {
                string fromJson = SessionState.GetString(queueName, k_DefaultSessionStateString);
                reqs = JsonUtility.FromJson<LoaderAssignmentRequests>(fromJson);
                SessionState.EraseString(queueName);
            }

            return reqs;
        }

        internal static void RebuildInstalledCache()
        {
            if (isRebuildingCache)
                return;

            var req = new LoaderAssignmentRequest();
            req.packageListRequest = Client.List(true, false);
            req.installationState = InstallationState.RebuildInstalledCache;
            req.timeOut = Time.realtimeSinceStartup + k_TimeOutDelta;
            QueueLoaderRequest(req);
        }

        static void RebuildCache()
        {
            EditorApplication.update -= RebuildCache;

            if (IsEditorInPlayMode())
            {
                return; // Use the cached data that should have been passed in the play state change.
            }

            LoaderAssignmentRequests reqs = GetAllRequestsInQueue(k_RebuildCache);

            if (reqs.activeRequests == null || reqs.activeRequests.Count == 0)
            {
                return;
            }

            var req = reqs.activeRequests[0];
            reqs.activeRequests.Remove(req);

            if (req.timeOut < Time.realtimeSinceStartup)
            {
                req.logMessage = $"Timeout trying to get package list after {k_TimeOutDelta}s.";
                req.logLevel = LogLevel.Warning;
                req.installationState = InstallationState.Log;
                QueueLoaderRequest(req);
            }
            else if (req.packageListRequest.IsCompleted)
            {
                if (req.packageListRequest.Status == StatusCode.Success)
                {
                    var installedPackages = new List<string>();

                    foreach (var packageInfo in req.packageListRequest.Result)
                    {
                        installedPackages.Add(packageInfo.name);
                    }

                    var packageIds = packages.Values.
                        Where(p => installedPackages.Contains(p.metadata.packageId)).
                        Select(p => p.metadata.packageId);
                    s_CachedMDStoreInformation.installedPackages = packageIds.ToArray();
                }

                StoreCachedMDStoreInformation();
            }
            else if (!req.packageListRequest.IsCompleted)
            {
                QueueLoaderRequest(req);
            }
            else
            {
                req.logMessage = $"Unable to rebuild installed package cache. Some state may be missing or incorrect.";
                req.logLevel = LogLevel.Warning;
                req.installationState = InstallationState.Log;
                QueueLoaderRequest(req);
            }

            if (reqs.activeRequests.Count > 0)
            {
                SetRequestsInQueue(reqs, k_RebuildCache);
                EditorApplication.update += RebuildCache;
            }
        }

        static void ResetManagerUiIfAvailable()
        {
            if (XRSettingsManager.Instance != null) XRSettingsManager.Instance.ResetUi = true;
        }

        static void AssignAnyRequestedLoadersUpdate()
        {
            EditorApplication.update -= AssignAnyRequestedLoadersUpdate;

            var requests = GetAllRequestsInQueue(k_AssigningPackage);

            if (requests.activeRequests == null || requests.activeRequests.Count == 0)
                return;

            while (requests.activeRequests.Count > 0)
            {
                var req = requests.activeRequests[0];
                requests.activeRequests.RemoveAt(0);

                var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(req.buildTargetGroup);

                if (settings == null)
                    continue;

                if (settings.Manager == null)
                {
                    var manager = ScriptableObject.CreateInstance<XRManagerSettings>() as XRManagerSettings;
                    settings.Manager = manager;
                    EditorUtility.SetDirty(settings);
                }

                if (!AssignLoader(settings.Manager, req.loaderType, req.buildTargetGroup))
                {
                    req.installationState = InstallationState.Log;
                    req.logMessage = $"Unable to assign {req.packageId} for build target {req.buildTargetGroup}.";
                    req.logLevel = LogLevel.Error;
                    QueueLoaderRequest(req);
                }
            }

            ResetManagerUiIfAvailable();
        }

        static void MonitorPackageInstallation()
        {
            EditorApplication.update -= MonitorPackageInstallation;
            var requests = GetAllRequestsInQueue(k_InstallingPackage);

            if (requests.activeRequests.Count > 0)
            {
                var request = requests.activeRequests[0];
                requests.activeRequests.RemoveAt(0);

                if (request.needsAddRequest)
                {
                    if (s_CachedMDStoreInformation.knownPackageInfos == null)
                    {
                        request.logMessage = $"No package information to query against. Unable to load package {request.packageId}.";
                        request.logLevel = LogLevel.Error;
                        request.installationState = InstallationState.Log;
                    }
                    else
                    {
                        var versionToInstallQ = s_CachedMDStoreInformation.knownPackageInfos.
                            Where(kpi => String.Compare(request.packageId, kpi.packageId) == 0).
                            Select(kpi => kpi.verifiedVersion);
                        var versionToInstall = versionToInstallQ.FirstOrDefault();
                        var packageToInstall = String.IsNullOrEmpty(versionToInstall) ?
                            request.packageId :
                            $"{request.packageId}@{versionToInstall}";
                        request.packageAddRequest = Client.Add(packageToInstall);
                        request.needsAddRequest = false;
                        request.installationState = InstallationState.Installing;

                        s_CachedMDStoreInformation.hasAlreadyRequestedData = true;
                        StoreCachedMDStoreInformation();
                    }

                    QueueLoaderRequest(request);
                }
                else switch (request.packageAddRequest.IsCompleted)
                {
                    case true when File.Exists($"Packages/{request.packageId}/package.json")
                        && request.packageAddRequest.Status == StatusCode.Success:
                    {
                        if (!string.IsNullOrEmpty(request.loaderType))
                        {
                            request.packageAddRequest = null;
                            request.installationState = InstallationState.Assigning;
                        }
                        else
                        {
                            request.logMessage = $"Missing loader type. Unable to assign loader.";
                            request.logLevel = LogLevel.Error;
                            request.installationState = InstallationState.Log;
                        }

                        QueueLoaderRequest(request);
                        break;
                    }
                    case true when request.packageAddRequest.Status != StatusCode.Success:
                    {
                        request.logMessage = string.IsNullOrEmpty(request.packageId)
                            ? "Error installing package with no package id."
                            : $"Error Message: {request.packageAddRequest.Error?.message ?? "UNKNOWN" }.\nError installing package {request.packageId }.";

                        request.logLevel = LogLevel.Error;
                        request.installationState = InstallationState.Log;
                        QueueLoaderRequest(request);
                        break;
                    }
                    default:
                    {
                        if (request.timeOut < Time.realtimeSinceStartup)
                        {
                            request.logMessage = String.IsNullOrEmpty(request.packageId)
                                ? "Time out while installing package with no package id."
                                : $"Error installing package {request.packageId}. Package installation timed out. Check Package Manager UI to see if the package is installed and/or retry your operation.";

                            request.logLevel = LogLevel.Error;

                            if (request.packageAddRequest.IsCompleted)
                            {
                                request.logMessage += $" Error message: {request.packageAddRequest.Error.message}";
                            }

                            request.installationState = InstallationState.Log;
                        }

                        QueueLoaderRequest(request);
                        break;
                    }
                }
            }
        }

        static void WaitingOnSearchQuery()
        {
            EditorApplication.update -= WaitingOnSearchQuery;
            if (s_SearchRequest != null)
            {
                if (s_SearchRequest.IsCompleted)
                    EditorApplication.update += UpdateInstallablePackages;
                else
                    EditorApplication.update += WaitingOnSearchQuery;
                return;
            }

            var requests = GetAllRequestsInQueue(k_WaitingPackmanQuery);
            if (requests.activeRequests.Count > 0)
            {
                foreach (var request in requests.activeRequests)
                {
                    var req = request;
                    req.installationState = IsPackageInstalled(req.packageId)
                        ? InstallationState.Assigning
                        : InstallationState.StartInstallation;
                    req.timeOut = Time.realtimeSinceStartup + k_TimeOutDelta;
                    QueueLoaderRequest(req);
                }
            }
        }

        static void MonitorPackageUninstall()
        {
            EditorApplication.update -= MonitorPackageUninstall;
            var requests = GetAllRequestsInQueue(k_UninstallingPackage);
            if (requests.activeRequests.Count > 0)
            {
                for (int i = 0; i < requests.activeRequests.Count; i++)
                {
                    var req = requests.activeRequests[i];
                    if (!req.packageRemoveRequest.IsCompleted)
                        QueueLoaderRequest(req);

                    if (req.packageRemoveRequest.Status == StatusCode.Failure)
                    {
                        req.installationState = InstallationState.Log;
                        req.logMessage = req.packageRemoveRequest.Error.message;
                        req.logLevel = LogLevel.Warning;
                        QueueLoaderRequest(req);
                    }
                }
            }
        }

        static void QueueLoaderRequest(LoaderAssignmentRequest req)
        {
            switch (req.installationState)
            {
                case InstallationState.New:
                    if (!s_CachedMDStoreInformation.hasAlreadyRequestedData
                        && !HasInstallablePackageData() && s_SearchRequest == null)
                    {
                        s_SearchRequest = Client.SearchAll(false);
                        EditorApplication.update += UpdateInstallablePackages;
                    }
                    AddRequestToQueue(req, k_WaitingPackmanQuery);
                    EditorApplication.update += WaitingOnSearchQuery;
                    break;

                case InstallationState.RebuildInstalledCache:
                    AddRequestToQueue(req, k_RebuildCache);
                    EditorApplication.update += RebuildCache;
                    break;

                case InstallationState.StartInstallation:
                    req.needsAddRequest = true;
                    req.packageAddRequest = null;
                    req.timeOut = Time.realtimeSinceStartup + k_TimeOutDelta;
                    AddRequestToQueue(req, k_InstallingPackage);
                    EditorApplication.update += MonitorPackageInstallation;
                    break;

                case InstallationState.Installing:
                    AddRequestToQueue(req, k_InstallingPackage);
                    EditorApplication.update += MonitorPackageInstallation;
                    break;

                case InstallationState.Assigning:
                    AddRequestToQueue(req, k_AssigningPackage);
                    EditorApplication.update += AssignAnyRequestedLoadersUpdate;
                    break;

                case InstallationState.Uninstalling:
                    AddRequestToQueue(req, k_UninstallingPackage);
                    EditorApplication.update += MonitorPackageUninstall;
                    break;

                case InstallationState.Log:
                    const string header = "XR Plug-in Management";
                    switch(req.logLevel)
                    {
                        case LogLevel.Info:
                        Debug.Log($"{header}: {req.logMessage}");
                        break;

                        case LogLevel.Warning:
                        Debug.LogWarning($"{header} Warning: {req.logMessage}");
                        break;

                        case LogLevel.Error:
                        Debug.LogError($"{header} error. Failure reason: {req.logMessage}.\n Check if there are any other errors in the console and make sure they are corrected before trying again.");
                        break;
                    }
                    ResetManagerUiIfAvailable();
                    break;
            }
        }
    }
}
