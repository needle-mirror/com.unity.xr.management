using System;
using System.Collections;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif
using UnityEngine.Serialization;
using UnityEngine.XR.Management;

namespace UnityEngine.XR.Management
{
    [Serializable]
    public class CuratedInfo
    {
        public string MenuTitle;
        public string PackageName;
        public string LoaderTypeInfo;
    }

    public sealed class XRCuratedPackages : ScriptableObject
    {
        [SerializeField]
        public CuratedInfo[] CuratedPackages;
    }
}
