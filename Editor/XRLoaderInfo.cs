using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.XR.Management;

namespace UnityEditor.XR.Management
{
    class XRLoaderInfo : IEquatable<XRLoaderInfo>
    {
        static string[] s_LoaderBlockList = { "DummyLoader", "SampleLoader", "XRLoaderHelper" };

        internal Type loaderType;
        internal string assetName;
        internal XRLoader instance;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is XRLoaderInfo info && Equals(info);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = loaderType != null ? loaderType.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (instance != null ? instance.GetHashCode() : 0);
                return hashCode;
            }
        }

        public bool Equals(XRLoaderInfo other)
        {
            return other != null && loaderType == other.loaderType && Equals(instance, other.instance);
        }

        internal static void GetAllKnownLoaderInfos(List<XRLoaderInfo> newInfos)
        {
            var loaderTypes = TypeLoaderExtensions.GetAllTypesWithInterface<XRLoader>();
            foreach (var loaderType in loaderTypes)
            {
                if (loaderType.IsAbstract)
                    continue;

                if (s_LoaderBlockList.Contains(loaderType.Name))
                    continue;

                var assets = AssetDatabase.FindAssets($"t:{loaderType}");
                if (!assets.Any())
                {
                    var info = new XRLoaderInfo { loaderType = loaderType };
                    newInfos.Add(info);
                }
                else
                {
                    foreach (var asset in assets)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(asset);

                        var info = new XRLoaderInfo
                        {
                            loaderType = loaderType,
                            instance = AssetDatabase.LoadAssetAtPath(path, loaderType) as XRLoader,
                            assetName = Path.GetFileNameWithoutExtension(path)
                        };
                        newInfos.Add(info);
                    }
                }
            }
        }
    }
}
