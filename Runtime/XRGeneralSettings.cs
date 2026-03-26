using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.XR.Management
{
    /// <summary>
    /// General settings container used to house the instance of the active settings as well as the manager
    /// instance used to load the loaders with.
    /// </summary>
    public class XRGeneralSettings : ScriptableObject
    {
        /// <summary>The key used to query to get the current loader settings.</summary>
        public static string k_SettingsKey = "com.unity.xr.management.loader_settings";

        internal static XRGeneralSettings s_Instance;

        /// <summary>
        /// The current settings instance.
        /// </summary>
        public static XRGeneralSettings Instance
        {
            get => s_Instance;
#if UNITY_EDITOR
            set => s_Instance = value;
#endif
        }

        [SerializeField]
        internal XRManagerSettings m_LoaderManagerInstance;

        /// <summary>
        /// The current active manager used to manage XR lifetime.
        /// </summary>
        public XRManagerSettings Manager
        {
            get => m_LoaderManagerInstance;
            set => m_LoaderManagerInstance = value;
        }

        /// <summary>
        /// The current active manager used to manage XR lifetime.
        /// </summary>
        public XRManagerSettings AssignedSettings
        {
            get => m_LoaderManagerInstance;
#if UNITY_EDITOR
            set => m_LoaderManagerInstance = value;
#endif
        }

        [SerializeField]
        [Tooltip("Toggling this on/off will enable/disable the automatic startup of XR at run time.")]
        internal bool m_InitManagerOnStart = true;

        /// <summary>
        /// Used to set if the manager is activated and initialized on startup.
        /// </summary>
        public bool InitManagerOnStart
        {
            get => m_InitManagerOnStart;
#if UNITY_EDITOR
            set => m_InitManagerOnStart = value;
#endif
        }

        XRManagerSettings m_XRManager;

#if !UNITY_EDITOR
        void Awake()
        {
            Debug.Log("XRGeneral Settings awakening...");
            s_Instance = this;
            Application.quitting += Quit;
            DontDestroyOnLoad(s_Instance);
        }
#endif

        static void Quit()
        {
            if (Instance == null)
                return;

            Instance.DeInitXRSDK();
        }

        void OnDestroy()
        {
            DeInitXRSDK();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void AttemptInitializeXRSDKOnLoad()
        {
            if (Instance == null || !Instance.InitManagerOnStart)
                return;

            Instance.InitXRSDK();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        internal static void AttemptStartXRSDKOnBeforeSplashScreen()
        {
            if (Instance == null || !Instance.InitManagerOnStart)
                return;

            Instance.StartXRSDK();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void SubscribeToEditorQuit()
        {
            // This is a static delegate that is reset on domain reload.
            EditorApplication.quitting += Quit;
        }
#endif
        void InitXRSDK()
        {
            if (Instance == null || Instance.m_LoaderManagerInstance == null || !Instance.m_InitManagerOnStart)
                return;

            m_XRManager = Instance.m_LoaderManagerInstance;
            if (m_XRManager == null)
            {
                Debug.LogError("Assigned GameObject for XR Management loading is invalid. No XR Providers will be automatically loaded.");
                return;
            }

            m_XRManager.automaticLoading = false;
            m_XRManager.automaticRunning = false;
            m_XRManager.InitializeLoaderSync();
        }

        void StartXRSDK()
        {
            if (m_XRManager != null && m_XRManager.activeLoader != null)
            {
                m_XRManager.StartSubsystems();
            }
        }

        void DeInitXRSDK()
        {
            if (m_XRManager != null && m_XRManager.activeLoader != null)
            {
                m_XRManager.DeinitializeLoader();
                m_XRManager = null;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// For internal use only.
        /// </summary>
        [Obsolete("Deprecating internal only API.")]
        public void InternalPauseStateChanged(PauseState state)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// For internal use only.
        /// </summary>
        public void InternalPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                Quit();
        }
#endif
    }
}
