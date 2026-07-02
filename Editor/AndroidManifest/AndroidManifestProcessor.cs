using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Unity.XR.Management.AndroidManifest.Editor
{
    /// <summary>
    /// Class that retrieves Android manifest entries required by classes that implement the IAndroidManifestEntryProvider interface.
    /// </summary>
    class AndroidManifestProcessor
    {
        const string k_AndroidManifestFileName = "AndroidManifest.xml";
        const string k_XrLibraryDirectoryName = "xrmanifest.androidlib";
        static readonly List<string> k_ActivityElementPath = new() { "manifest", "application", "activity" };
        static readonly string k_XrLibraryManifestRelativePath = string.Join(
            Path.DirectorySeparatorChar.ToString(), k_XrLibraryDirectoryName, k_AndroidManifestFileName);

        readonly string m_UnityLibraryManifestFilePath;
        readonly string m_XrPackageManifestTemplateFilePath;
        readonly string m_XrLibraryManifestFilePath;
        readonly XRManagerSettings m_XrSettings;

        internal AndroidManifestProcessor(string gradleProjectPath, XRManagerSettings settings)
        {
            m_UnityLibraryManifestFilePath = string.Join(
                Path.DirectorySeparatorChar.ToString(), gradleProjectPath, "src", "main", k_AndroidManifestFileName);

            m_XrSettings = settings;
        }

        internal AndroidManifestProcessor(
            string gradleProjectPath, string xrManagementPackagePath, XRManagerSettings settings)
        {
            m_XrPackageManifestTemplateFilePath = string.Join(
                Path.DirectorySeparatorChar.ToString(), xrManagementPackagePath, k_XrLibraryManifestRelativePath);

            m_XrLibraryManifestFilePath = string.Join(
                Path.DirectorySeparatorChar.ToString(), gradleProjectPath, k_XrLibraryManifestRelativePath);

            m_UnityLibraryManifestFilePath = string.Join(
                Path.DirectorySeparatorChar.ToString(), gradleProjectPath, "src", "main", k_AndroidManifestFileName);

            m_XrSettings = settings;
        }

        internal bool UseActivityAppEntry { get; set; } = true;
        internal bool UseGameActivityAppEntry { get; set; } = false;

        internal void ProcessManifestRequirements(List<IAndroidManifestRequirementProvider> manifestProviders)
        {
            var activeLoaders = GetActiveLoaderList();

            // Get manifest entries from providers
            var manifestRequirements = manifestProviders
                .Select(provider => provider.ProvideManifestRequirement())
                .OfType<ManifestRequirement>()
                .Distinct()
                // Requirements can apply to different platforms, so we filter out those whose loaders aren't currently active
                .Where(requirement => requirement.SupportedXRLoaders.Any(activeLoaders.Contains))
                .ToList();

            var mergedRequiredElements =
                MergeElements(
                    manifestRequirements
                    .SelectMany(requirement => requirement.OverrideElements));
            var elementsToBeRemoved = manifestRequirements
                .SelectMany(requirement => requirement.RemoveElements)
                .OfType<ManifestElement>();

            // The intent-filter elements are not merged by default,
            // so we separate them from the XR manifest to add them later.
            // Otherwise, the application won't load correctly.
            var newRequiredElements = manifestRequirements
                .SelectMany(requirement => requirement.NewElements)
                .Where(element => !element.ElementPath.Contains("activity"));
            var newActivityElements = manifestRequirements
                .SelectMany(requirement => requirement.NewElements)
                .Where(element =>
                    element.ElementPath.Contains("activity")
                    && !element.ElementPath.Contains("intent-filter"));
            var newIntentElements = manifestRequirements
                .SelectMany(requirement => requirement.NewElements)
                .Where(element => element.ElementPath.Contains("intent-filter"));

            {
                var xrLibraryManifest = new AndroidManifestDocument(m_XrPackageManifestTemplateFilePath);
                var unityLibraryManifest = new AndroidManifestDocument(m_UnityLibraryManifestFilePath);

                // Create activity elements depending on the selected application entry in Player Settings
                if (UseActivityAppEntry)
                {
                    xrLibraryManifest.CreateNewElement(k_ActivityElementPath, new Dictionary<string, string> { { "name", "com.unity3d.player.UnityPlayerActivity" } });
                }

                if (UseGameActivityAppEntry)
                {
                    xrLibraryManifest.CreateNewElement(k_ActivityElementPath, new Dictionary<string, string> { { "name", "com.unity3d.player.UnityPlayerGameActivity" } });
                }

                AddExportedAttributeToActivity(xrLibraryManifest, newIntentElements);

                if (UseActivityAppEntry && UseGameActivityAppEntry)
                {
                    xrLibraryManifest.CreateElements(newRequiredElements);
                    // Add all related activity elements to each element
                    foreach (var newActivityElement in newActivityElements)
                    {
                        xrLibraryManifest.CreateNewElementInAllPaths(newActivityElement.ElementPath, newActivityElement.Attributes);
                    }

                    xrLibraryManifest.OverrideElements(mergedRequiredElements);

                    // Add all related activity elements to each element
                    foreach (var newIntentElement in newIntentElements)
                    {
                        unityLibraryManifest.CreateNewElementInAllPaths(newIntentElement.ElementPath, newIntentElement.Attributes);
                    }
                }
                else
                {
                    xrLibraryManifest.CreateElements(newRequiredElements);
                    xrLibraryManifest.CreateElements(newActivityElements);
                    xrLibraryManifest.OverrideElements(mergedRequiredElements);
                    unityLibraryManifest.CreateElements(newIntentElements, false); // Add the intents in the unity library manifest
                }

                unityLibraryManifest.RemoveElements(elementsToBeRemoved);

                // Write updated manifests
                xrLibraryManifest.SaveAs(m_XrLibraryManifestFilePath);
                unityLibraryManifest.Save();
            }
        }

        /// <summary>
        /// Merges the elements of given <see cref="IEnumerable{T}"/> of type <see cref="ManifestElement"/> based on
        /// their <see cref="ManifestElement.ElementPath"/> and their <c>name</c> attribute, so that distinct elements
        /// that share a path (for example, two <c>meta-data</c> entries with different names) are not collapsed into one.
        /// Their key-value pair attributes are deduped and merged into a single element. When the same attribute key
        /// holds conflicting values, the following rules are applied:
        /// <list type="bullet">
        /// <item><description><c>required</c>: if any value is <c>true</c>, the merged value is <c>true</c>.</description></item>
        /// <item><description><c>version</c>: the highest version number is kept.</description></item>
        /// <item><description>any other key: the last provided value is kept.</description></item>
        /// </list>
        /// </summary>
        /// <param name="source"><see cref="IEnumerable{T}"/> of type <see cref="ManifestElement"/> containing all elements to be merged.</param>
        /// <returns>Filtered <see cref="IEnumerable{T}"/> of type <see cref="ManifestElement"/> with unique elements.</returns>
        static IEnumerable<ManifestElement> MergeElements(IEnumerable<ManifestElement> source)
        {
            var mergedElements = new Dictionary<ManifestElement, ManifestElement>(ManifestElementKeyComparer.instance);
            foreach (var element in source)
            {
                if (mergedElements.TryGetValue(element, out var mergedElement))
                {
                    MergeAttributesInto(mergedElement.Attributes, element.Attributes);
                }
                else
                {
                    mergedElement = new ManifestElement
                    {
                        ElementPath = element.ElementPath,
                        Attributes = element.Attributes != null
                            ? new Dictionary<string, string>(element.Attributes)
                            : new Dictionary<string, string>()
                    };
                    mergedElements.Add(mergedElement, mergedElement);
                }
            }

            return mergedElements.Values;
        }

        /// <summary>
        /// Merges the attributes of <paramref name="source"/> into <paramref name="target"/>, resolving conflicts for
        /// known keys.
        /// </summary>
        static void MergeAttributesInto(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var attribute in source)
            {
                target[attribute.Key] = target.TryGetValue(attribute.Key, out var existingValue)
                    ? MergeAttributeValue(attribute.Key, existingValue, attribute.Value)
                    : attribute.Value;
            }
        }

        /// <summary>
        /// Resolves the value to keep when an attribute key holds two conflicting values.
        /// </summary>
        static string MergeAttributeValue(string key, string currentValue, string newValue)
        {
            if (string.Equals(currentValue, newValue, StringComparison.Ordinal))
            {
                return currentValue;
            }

            return key switch
            {
                "required" => IsTrue(currentValue) || IsTrue(newValue) ? "true" : "false",
                "version" => GetHigherVersion(currentValue, newValue),
                _ => newValue
            };
        }

        static bool IsTrue(string value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the higher of two versions if both strings can be parsed as a `System.Version`.
        /// Otherwise, returns either valid version string, or `currentValue` if neither string is a parseable version number.
        /// </summary>
        static string GetHigherVersion(string currentValue, string newValue)
        {
            var currentParsed = TryParseVersion(currentValue, out var currentVersion);
            var newParsed = TryParseVersion(newValue, out var newVersion);

            if (currentParsed && newParsed)
            {
                return newVersion > currentVersion ? newValue : currentValue;
            }

            if (newParsed)
            {
                Debug.LogWarning($"{currentValue} isn't a valid version number, using {newValue} from another manifest element.");
                return newValue;
            }
            if (currentParsed)
            {
                Debug.LogWarning($"{newValue} isn't a valid version number, using {currentValue} from another manifest element.");
                return currentValue;
            }

            Debug.LogWarning($"Neither {currentValue} or {newValue} are valid version numbers. Choosing {currentValue}, but you should confirm that your Android App Manifest is correct!");
            return currentValue;
        }

        static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            // System.Version requires at least a major and minor component, so a single number is normalized to "N.0".
            var normalizedValue = value.Contains(".") ? value : string.Concat(value, ".0");
            return Version.TryParse(normalizedValue, out version);
        }

        List<Type> GetActiveLoaderList()
        {
            if (!m_XrSettings)
            {
                // No loaders active, don't throw error
                Debug.LogWarning("No XR Manager settings found, manifest entries will not be updated.");
                return new List<Type>();
            }

            return m_XrSettings.activeLoaders
                .Select(loader => loader.GetType())
                .ToList();
        }

        static void AddExportedAttributeToActivity(
            AndroidManifestDocument xrLibraryManifest,
            IEnumerable<ManifestElement> newIntentElements)
        {
            if (newIntentElements.Any())
            {
                // Add exported attribute to all activities in the XR library manifest, as required by the Android manifest
                var activityPath = string.Join("/", k_ActivityElementPath);
                var activityNodes = xrLibraryManifest.SelectNodes(activityPath);
                foreach (var activity in activityNodes)
                {
                    XmlAttribute exportedAttribute = xrLibraryManifest.CreateAttribute("android:exported", "http://schemas.android.com/apk/res/android");
                    exportedAttribute.Value = "true";
                    ((XmlElement)activity).Attributes.Append(exportedAttribute);
                }
            }
        }
    }
}
