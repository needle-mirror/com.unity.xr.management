using System.Linq;

namespace UnityEditor.XR.Management
{
    static class BuildHelpers
    {
        internal static void CleanOldSettings<T>()
        {
            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            if (preloadedAssets == null)
                return;

            var oldSettings = from s in preloadedAssets
                where s != null && s.GetType() == typeof(T)
                select s;

            if (!oldSettings.Any())
                return;

            var assets = preloadedAssets.ToList();
            foreach (var s in oldSettings)
            {
                assets.Remove(s);
            }

            PlayerSettings.SetPreloadedAssets(assets.ToArray());
        }
    }
}
