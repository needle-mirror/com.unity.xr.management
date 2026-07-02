using System;
using UnityEngine;

namespace UnityEditor.XR.Management
{
    class XRCustomLoaderUIManager
    {
        public static IXRCustomLoaderUI GetCustomLoaderUI(string loaderTypeName, BuildTargetGroup buildTargetGroup)
        {
            IXRCustomLoaderUI ret = null;

            var customLoaderTypes = TypeCache.GetTypesDerivedFrom(typeof(IXRCustomLoaderUI));
            foreach (var customLoader in customLoaderTypes)
            {
                var attributes = customLoader.GetCustomAttributes(typeof(XRCustomLoaderUIAttribute), true);
                foreach (var attribute in attributes)
                {
                    if (attribute is XRCustomLoaderUIAttribute customUiAttribute)
                    {
                        if (string.Compare(loaderTypeName, customUiAttribute.loaderTypeName, true) == 0 &&
                            buildTargetGroup == customUiAttribute.buildTargetGroup)
                        {
                            if (ret != null)
                            {
                                Debug.Log($"Multiple custom ui renderers found for ({loaderTypeName}, {buildTargetGroup}). Defaulting to built-in rendering instead.");
                                return null;
                            }
                            ret = Activator.CreateInstance(customLoader) as IXRCustomLoaderUI;
                        }
                    }
                }
            }

            return ret;
        }
    }
}
