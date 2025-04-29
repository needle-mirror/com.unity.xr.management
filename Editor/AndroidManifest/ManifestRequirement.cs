using System;
using System.Collections.Generic;

namespace Unity.XR.Management.AndroidManifest.Editor
{
    /// <summary>
    /// This class contains lists of Android manifest elements that need to be added, overriden or removed from the application manifest.
    /// </summary>
    public class ManifestRequirement : IEquatable<ManifestRequirement>
    {
        /// <summary>
        /// Set of supported <see cref="UnityEngine.XR.Management.XRLoader"/> types by these requirements.
        /// If none of the listed loaders is active at the moment of building, the requirements will be ignored.
        /// </summary>
        public HashSet<Type> SupportedXRLoaders { get; set; } = new HashSet<Type>();

        /// <summary>
        /// List of <see cref="ManifestElement"/> elements that will be added to the Android manifest.
        /// Each entry represents a single element within its specified node path, and it won't overwrite or override any other element to be added.
        /// </summary>
        public List<ManifestElement> NewElements { get; set; } = new List<ManifestElement>();

        /// <summary>
        /// List of <see cref="ManifestElement"/> elements whose attirbutes will be merged or overriden with existing the Android manifest elements.
        /// If the manifest element doesn't exist in the file, it will be created.
        /// </summary>
        public List<ManifestElement> OverrideElements { get; set; } = new List<ManifestElement>();

        /// <summary>
        /// List of <see cref="ManifestElement"/> elements which will be removed from the Android manifest.
        /// Entries not found will be ignored.
        /// Only entries that specify the same attributes and its respective values in the manifest will be taken in account for deletion.
        /// </summary>
        public List<ManifestElement> RemoveElements { get; set; } = new List<ManifestElement>();

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return obj is ManifestRequirement && Equals(obj as ManifestRequirement);
        }

        /// <inheritdoc/>
        public bool Equals(ManifestRequirement other)
        {
            return other != null &&
                ((NewElements == null && other.NewElements == null) || (NewElements != null && NewElements.Equals(other.NewElements))) &&
                ((OverrideElements == null && other.OverrideElements == null) || (OverrideElements != null && OverrideElements.Equals(other.OverrideElements))) &&
                ((RemoveElements == null && other.RemoveElements == null) || (RemoveElements != null && RemoveElements.Equals(other.RemoveElements)));
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(NewElements, OverrideElements, RemoveElements);
        }
    }
}
