using System;
using System.Collections;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;
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
