using System;
using System.Collections.Generic;

namespace Unity.XR.Management.AndroidManifest.Editor
{
    /// <summary>
    /// This class holds information for a single Android manifest element, including its path and attributes.
    /// </summary>
    public class ManifestElement
    {
        /// <summary>
        ///     <para>
        ///         List of element names representing the full XML path to the element. It must include last the element that this object represents.
        ///     </para>
        /// </summary>
        ///
        /// <remarks>
        ///     <para>
        ///         The order in which the elements are added is important,
        ///         as each list member represents an XML element name in the order,
        ///         so specifying a list as { "manifest", "application" } is not the same
        ///         as { "application", "manifest"}.
        ///     </para>
        /// </remarks>
        ///
        /// <example>
        ///     <para>
        ///     Example for accessing a meta-data element:
        ///     </para>
        ///     <code>
        ///         new ManifestElement {
        ///             ElementPath = new List() {
        ///                 "manifest", "application", "meta-data"
        ///             }
        ///         }
        ///     </code>
        /// </example>
        public List<string> ElementPath { get; set; }

        /// <summary>
        /// Dictionary of Name-Value pairs of the represented element's attributes.
        /// </summary>
        public Dictionary<string, string> Attributes { get; set; }
    }

    /// <summary>
    /// Compares <see cref="ManifestElement"/> instances by their <see cref="ManifestElement.ElementPath"/> and
    /// <c>name</c> attribute, reading both in place so no intermediate keys are allocated while grouping.
    /// </summary>
    sealed class ManifestElementKeyComparer : IEqualityComparer<ManifestElement>
    {
        internal static readonly ManifestElementKeyComparer instance = new();

        public bool Equals(ManifestElement x, ManifestElement y)
        {
            return PathEquals(x!.ElementPath, y!.ElementPath)
                && string.Equals(GetName(x), GetName(y), StringComparison.Ordinal);
        }

        public int GetHashCode(ManifestElement element)
        {
            unchecked
            {
                var hash = 17;
                var path = element.ElementPath;
                if (path != null)
                {
                    for (var i = 0; i < path.Count; i++)
                    {
                        hash = hash * 31 + (path[i]?.GetHashCode() ?? 0);
                    }
                }

                var name = GetName(element);
                return hash * 31 + (name?.GetHashCode() ?? 0);
            }
        }

        static bool PathEquals(List<string> a, List<string> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        static string GetName(ManifestElement element)
        {
            return element.Attributes != null
                && element.Attributes.TryGetValue("name", out var name)
                ? name
                : null;
        }
    }
}
