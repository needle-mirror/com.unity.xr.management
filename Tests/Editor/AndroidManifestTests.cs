using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Unity.XR.Management.AndroidManifest.Editor;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

public class AndroidManifestTests
{
    private const string k_androidXmlNamespace = "http://schemas.android.com/apk/res/android";
    private const string k_unityActivityName = "com.unity3d.player.UnityPlayerActivity";
    private const string k_unityGameActivityName = "com.unity3d.player.UnityPlayerGameActivity";
    private readonly List<string> k_activityPath = new List<string> { "manifest", "application", "activity" };
    private readonly List<string> k_categoryPath = new List<string> { "manifest", "application", "activity", "intent-filter", "category" };

    private string tempProjectPath;
    private string xrManifestTemplateFilePath;
    private string xrLibraryManifestFilePath;
    private string unityLibraryManifestFilePath;
    private DirectoryInfo dirInfo;
    private XRManagerSettings mockXrSettings;
    private Type supportedLoaderType;

    [SetUp]
    public void SetUp()
    {
        tempProjectPath = FileUtil.GetUniqueTempPathInProject();
        dirInfo = Directory.CreateDirectory(tempProjectPath);

        var xrPackagePath = dirInfo.CreateSubdirectory(string.Join(Path.DirectorySeparatorChar.ToString(), "xrPackage", "xrmanifest.androidlib"));
        var xrLibraryPath = dirInfo.CreateSubdirectory("xrmanifest.androidlib");
        var unityLibraryPath = dirInfo.CreateSubdirectory(string.Join(Path.DirectorySeparatorChar.ToString(), "src", "main"));

        xrManifestTemplateFilePath = string.Join(Path.DirectorySeparatorChar.ToString(), xrPackagePath.FullName,  "AndroidManifest.xml");
        xrLibraryManifestFilePath = string.Join(Path.DirectorySeparatorChar.ToString(), xrLibraryPath.FullName, "AndroidManifest.xml");
        unityLibraryManifestFilePath = string.Join(Path.DirectorySeparatorChar.ToString(), unityLibraryPath.FullName, "AndroidManifest.xml");

        CreateMockManifestDocument(xrManifestTemplateFilePath);
        CreateMockManifestDocument(xrLibraryManifestFilePath);
        CreateMockManifestDocument(unityLibraryManifestFilePath);

        mockXrSettings = ScriptableObject.CreateInstance<XRManagerSettings>();
        supportedLoaderType = typeof(MockXrLoader);
        mockXrSettings.TrySetLoaders(new List<XRLoader>
        {
            ScriptableObject.CreateInstance<MockXrLoader>()
        });
    }

    [TearDown]
    public void TearDown()
    {
        dirInfo.Delete(true);
        ScriptableObject.DestroyImmediate(mockXrSettings);
    }

    [Test]
    public void AndroidManifestProcessor_AddOneNewManifestElement()
    {
        var processor = CreateProcessor();

        // Initialize data
        var newElementPath = new List<string> { "manifest", "application", "meta-data" };
        var newElementAttributes = new Dictionary<string, string>()
        {
            { "name", "custom-data" },
            { "value", "test-data" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                NewElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = newElementPath,
                        Attributes = newElementAttributes
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", newElementPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Additional elements exist in the Manifest when expecting 1");

        var attributeList = nodes[0].Attributes;
        Assert.AreEqual(
            newElementAttributes.Count,
            attributeList.Count,
            "Attribute count in element doesn't match expected count");

        AssertAttributesAreEqual(nodes[0].Name, newElementAttributes, attributeList);
    }

    [Test]
    public void AndroidManifestProcessor_AddTwoNewManifestElements()
    {
        var processor = CreateProcessor();

        // Initialize data
        var newElementPath = new List<string> { "manifest", "application", "meta-data" };
        var newElementAttributes = new Dictionary<string, string>()
        {
            { "name", "custom-data" },
            { "value", "test-data" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                NewElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = newElementPath,
                        Attributes = newElementAttributes
                    },
                    new ManifestElement()
                    {
                        ElementPath = newElementPath,
                        Attributes = newElementAttributes
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", newElementPath));
        Assert.AreEqual(
            2,
            nodes.Count,
            "Additional elements exist in the Manifest when expecting 2");

        foreach(XmlElement node in nodes)
        {
            var attributeList = node.Attributes;
            Assert.AreEqual(
                newElementAttributes.Count,
                attributeList.Count,
                "Attribute count in element doesn't match expected count");

            AssertAttributesAreEqual(node.Name, newElementAttributes, attributeList);
        }
    }

    [Test]
    public void AndroidManifestProcessor_CreateSingleNewManifestElementFromTwoOverridenElements()
    {
        // Use the Assert class to test conditions
        var processor = CreateProcessor();

        // Initialize data
        var overrideElementPath = new List<string> { "manifest", "application" };
        var overrideElement1Attributes = new Dictionary<string, string>();
        var overrideElement2Attributes = new Dictionary<string, string>();
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = overrideElementPath,
                        Attributes = overrideElement1Attributes
                    },
                    new ManifestElement()
                    {
                        ElementPath = overrideElementPath,
                        Attributes = overrideElement2Attributes
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", overrideElementPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Additional elements exist in the Manifest when expecting 1");

        var attributeList = nodes[0].Attributes;
        var expectedElementAttrributes = MergeDictionaries(overrideElement1Attributes, overrideElement2Attributes);
        Assert.AreEqual(
            expectedElementAttrributes.Count,
            attributeList.Count,
            $"Attribute count in element doesn't match expected {expectedElementAttrributes.Count}");

        AssertAttributesAreEqual(nodes[0].Name, expectedElementAttrributes, attributeList);
    }


    [Test]
    public void AndroidManifestProcessor_UpdateExistingElementWithOverridenElement()
    {
        // Use the Assert class to test conditions
        var processor = CreateProcessor();

        // Initialize data
        var overrideElementPath = new List<string> { "manifest", "test-tag" };
        var existingElementAttributes = new Dictionary<string, string>()
        {
            { "name", "com.test.app" }
        };
        var overrideElementAttributes = new Dictionary<string, string>()
        {
            { "isGame", "true" },
            { "testOnly", "true" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = overrideElementPath,
                        Attributes = overrideElementAttributes
                    }
                }
            })
        };

        // Prepare test document
        var libManifest = GetXrLibraryManifest();
        libManifest.CreateNewElement(overrideElementPath, existingElementAttributes);
        libManifest.Save();

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", overrideElementPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Additional elements exist in the Manifest when expecting 1");

        var attributeList = nodes[0].Attributes;
        var expectedElementAttrributes = MergeDictionaries(existingElementAttributes, overrideElementAttributes);
        Assert.AreEqual(
            expectedElementAttrributes.Count,
            attributeList.Count,
            $"Attribute count {attributeList.Count} in element doesn't match expected {expectedElementAttrributes.Count}");

        AssertAttributesAreEqual(nodes[0].Name, expectedElementAttrributes, attributeList);
    }

    [Test]
    public void AndroidManifestProcessor_UpdateExistingActivityElementWithOverridenElement()
    {
        // Use the Assert class to test conditions
        var processor = CreateProcessor();

        // Initialize data
        var existingElementAttributes = new Dictionary<string, string>()
        {
            { "name", k_unityActivityName }
        };
        var overrideElementAttributes = new Dictionary<string, string>()
        {
            { "isGame", "true" },
            { "testOnly", "true" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = k_activityPath,
                        Attributes = overrideElementAttributes
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", k_activityPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Additional elements exist in the Manifest when expecting 1");

        var attributeList = nodes[0].Attributes;
        var expectedElementAttrributes = MergeDictionaries(existingElementAttributes, overrideElementAttributes);
        Assert.AreEqual(
            expectedElementAttrributes.Count,
            attributeList.Count,
            $"Attribute count {attributeList.Count} in element doesn't match expected {expectedElementAttrributes.Count}");

        AssertAttributesAreEqual(nodes[0].Name, expectedElementAttrributes, attributeList);
    }

    [Test]
    public void AndroidManifestProcessor_UpdateAllActivityElementWithOverridenElement()
    {
        IgnoreIfGameActivityIsNotSupported();

        // Use the Assert class to test conditions
        var processor = CreateProcessor();
        processor.UseActivityAppEntry = true;
        processor.UseGameActivityAppEntry = true;

        // Initialize data
        var existingElementAttributes = new Dictionary<string, string>()
        {
            { "name", k_unityActivityName }
        };
        var overrideElementAttributes = new Dictionary<string, string>()
        {
            { "isGame", "true" },
            { "testOnly", "true" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = k_activityPath,
                        Attributes = overrideElementAttributes
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", k_activityPath));
        Assert.AreEqual(
            2,
            nodes.Count,
            "Additional elements exist in the Manifest when expecting 2");

        foreach (XmlNode node in nodes)
        {
            var attributeList = node.Attributes;
            var expectedElementAttrributes = MergeDictionaries(existingElementAttributes, overrideElementAttributes);
            Assert.AreEqual(
                expectedElementAttrributes.Count,
                attributeList.Count,
                $"Attribute count {attributeList.Count} in element doesn't match expected {expectedElementAttrributes.Count}");

            foreach (XmlAttribute attrib in attributeList)
            {
                var attributeName = attrib.Name.Split(':').Last(); // Values are returned with preffixed namespace name, pick only the attribute name
                if ("name".Equals(attributeName))
                {
                    // Check if the activity name is UnityPlayerActivity or UnityPlayerGameActivity
                    bool isUnityActivity =
                        k_unityActivityName.Equals(attrib.Value)
                        || k_unityGameActivityName.Equals(attrib.Value);
                    Assert.IsTrue(isUnityActivity, "Activity name is not UnityPlayerActivity or UnityPlayerGameActivity");
                }
                else if (!expectedElementAttrributes.Contains(new KeyValuePair<string, string>(attributeName, attrib.Value)))
                {
                    Assert.Fail($"Unexpected attribute \"{attrib.Name}\" " +
                        $"with value \"{attrib.Value}\" found in element {node.Name}");
                }
            }
        }
    }

    [Test]
    public void AndroidManifestProcessor_DeleteExistingManifestElement()
    {
        var processor = CreateProcessor();

        // Initialize data
        var deletedElementPath = new List<string> { "manifest", "uses-permission" };
        var deletedElementAttributes = new Dictionary<string, string>()
        {
            { "name", "BLUETOOTH" }
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                RemoveElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = deletedElementPath,
                        Attributes = deletedElementAttributes
                    }
                }
            })
        };

        // Prepare test document
        var appManifest = GetUnityLibraryManifest();
        appManifest.CreateNewElement(deletedElementPath, deletedElementAttributes);
        appManifest.Save();

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedAppManifest = GetXrLibraryManifest();
        var removedElementPath = string.Join("/", deletedElementPath);
        var removedNodes = updatedAppManifest.SelectNodes(removedElementPath);
        Assert.AreEqual(
            0,
            removedNodes.Count,
            $"Expected element in path \"{removedElementPath}\" wasn't deleted");
    }

    [Test]
    public void AndroidManifestProcessor_DontModifyManifestIfNoSupportedLoadersAdded()
    {
        var processor = CreateProcessor();

        // Initialize data
        var newElementPath = new List<string> { "manifest", "application", "meta-data" };
        var newElementAttributes = new Dictionary<string, string>()
        {
            { "name", "custom-data" },
            { "value", "test-data" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    typeof(object) // Dummy object representing an inactive loader
                },
                NewElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = newElementPath,
                        Attributes = newElementAttributes
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", newElementPath));
        Assert.AreEqual(
            0,
            nodes.Count,
            "Elements exist in the Manifest when expecting 0");
    }

    [Test]
    public void AndroidManifestProcessor_CheckThatActivityElementHasExportedAttributeWithIntents()
    {
        var processor = CreateProcessor();

        var newElementAttributes = new Dictionary<string, string>()
        {
            { "name", "com.oculus.intent.category.VR" }
        };
        var requirementPrvoider = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    typeof(object) // Dummy object representing an inactive loader
                },
                NewElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = k_categoryPath,
                        Attributes = newElementAttributes
                    }
                }
            })
        };

        processor.ProcessManifestRequirements(requirementPrvoider);

        var xrLibManifest = GetXrLibraryManifest();
        var activityNodes = xrLibManifest.SelectNodes(string.Join("/", k_activityPath));

        Assert.AreEqual(1, activityNodes.Count, "Expected 1 activity node in the manifest");

        bool foundExportedAttribute = false;
        foreach (XmlElement activityNode in activityNodes)
        {
            var attributeValue = activityNode.GetAttribute("exported", k_androidXmlNamespace);
            if ("true".Equals(attributeValue))
            {
                foundExportedAttribute = true;
                break;
            }
        }

        Assert.IsFalse(foundExportedAttribute, "exported attribute shouldn't be present");
    }

    [Test]
    public void AndroidManifestProcessor_CheckThatActivityElementDoesntHaveExportedAttributeWithoutIntents()
    {
        var processor = CreateProcessor();

        processor.ProcessManifestRequirements(new List<IAndroidManifestRequirementProvider>());

        var xrLibManifest = GetXrLibraryManifest();
        var activityNodes = xrLibManifest.SelectNodes(string.Join("/", k_activityPath));

        Assert.AreEqual(1, activityNodes.Count, "Expected 1 activity node in the manifest");

        bool foundExportedAttribute = false;
        foreach (XmlElement activityNode in activityNodes)
        {
            var attributeValue = activityNode.GetAttribute("exported", k_androidXmlNamespace);
            if ("true".Equals(attributeValue))
            {
                foundExportedAttribute = true;
                break;
            }
        }

        Assert.IsFalse(foundExportedAttribute, "exported attribute shouldn't be present");
    }

    [Test]
    public void AndroidManifestProcessor_CheckThatGameActivityCanBeCreated()
    {
        IgnoreIfGameActivityIsNotSupported();

        var processor = CreateProcessor();
        processor.UseActivityAppEntry = true;
        processor.UseGameActivityAppEntry = false;

        processor.ProcessManifestRequirements(new List<IAndroidManifestRequirementProvider>());

        var xrLibManifest = GetXrLibraryManifest();
        var activityNodes = xrLibManifest.SelectNodes(string.Join("/", k_activityPath));

        Assert.AreEqual(1, activityNodes.Count, "Expected 1 activity node in the manifest");

        bool foundUnityActivity = false;
        foreach (XmlElement activityNode in activityNodes)
        {
            var attributeValue = activityNode.GetAttribute("name", k_androidXmlNamespace);
            if (k_unityActivityName.Equals(attributeValue))
            {
                foundUnityActivity = true;
                break;
            }
        }

        Assert.IsTrue(foundUnityActivity, "UnityPlayerActivity not found in the manifest");
    }

    [Test]
    public void AndroidManifestProcessor_CheckThatNormalActivityAndGameActivityCanBeCreated()
    {
        IgnoreIfGameActivityIsNotSupported();

        var processor = CreateProcessor();
        processor.UseActivityAppEntry = true;
        processor.UseGameActivityAppEntry = true;

        processor.ProcessManifestRequirements(new List<IAndroidManifestRequirementProvider>());

        var xrLibManifest = GetXrLibraryManifest();
        var activityNodes = xrLibManifest.SelectNodes(string.Join("/", k_activityPath));

        Assert.AreEqual(2, activityNodes.Count, "Expected 2 activity nodes in the manifest");

        bool foundUnityActivity = false;
        bool foundUnityGameActivity = false;
        foreach (XmlElement activityNode in activityNodes)
        {
            var attributeValue = activityNode.GetAttribute("name", k_androidXmlNamespace);
            switch (attributeValue)
            {
                case k_unityActivityName:
                    foundUnityActivity = true;
                    break;
                case k_unityGameActivityName:
                    foundUnityGameActivity = true;
                    break;
            }
        }

        Assert.IsTrue(foundUnityActivity, "UnityPlayerActivity not found in the manifest");
        Assert.IsTrue(foundUnityGameActivity, "UnityPlayerGameActivity not found in the manifest");
    }

    [Test]
    public void AndroidManifestProcessor_NewCategoryElementsAreAddedAlongExistingCategoryElements()
    {
        var categoryElementName = "category";
        var processor = CreateProcessor();
        var categoryPath = new List<string>(k_categoryPath);
        categoryPath.Append(categoryElementName);

        // Existing Intent-Filter Category element
        var unityLibManifest = GetUnityLibraryManifest();
        var categoryAttributes = new Dictionary<string, string>()
        {
            { "name", "android.intent.category.LAUNCHER" }
        };
        unityLibManifest.CreateNewElement(categoryPath, categoryAttributes);
        unityLibManifest.Save();

        // New Intent-Filter Category element
        var newElementAttributes = new Dictionary<string, string>()
        {
            { "name", "com.oculus.intent.category.VR" }
        };
        var requirementPrvoider = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                NewElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = k_categoryPath,
                        Attributes = newElementAttributes
                    }
                }
            })
        };

        processor.ProcessManifestRequirements(requirementPrvoider);

        // Reload the manifest
        unityLibManifest = GetUnityLibraryManifest();
        var nodes = unityLibManifest.SelectNodes(string.Join("/", k_categoryPath));
        Assert.AreEqual(
            2,
            nodes.Count,
            "Additional elements exist in the Manifest when expecting 2");

        bool existingCategoryFound = false;
        bool newCategoryFound = false;
        foreach (XmlElement node in nodes)
        {
            if (categoryElementName.Equals(node.Name))
            {
                var categoryAttribValue = node.Attributes.GetNamedItem("name", k_androidXmlNamespace).Value;
                existingCategoryFound |= "android.intent.category.LAUNCHER".Equals(categoryAttribValue);
                newCategoryFound |= "com.oculus.intent.category.VR".Equals(categoryAttribValue);
            }
        }
        Assert.IsTrue(existingCategoryFound, "Existing category element not found");
        Assert.IsTrue(newCategoryFound, "New category element not found");
    }

    [Test]
    public void AndroidManifestProcessor_AddNewIntentsOnlyInUnityLibraryManifest()
    {
        var processor = CreateProcessor();

        // Initialize data
        var newElementAttributes = new Dictionary<string, string>()
        {
            { "name", "com.oculus.intent.category.VR" }
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                NewElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = k_categoryPath,
                        Attributes = newElementAttributes
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        var elementPath = string.Join("/", k_categoryPath);

        // Validate that the intent is created in Unity library manifest
        var unityLibManifest = GetUnityLibraryManifest();
        var addedNodes = unityLibManifest.SelectNodes(elementPath);
        Assert.AreEqual(
            1,
            addedNodes.Count,
            $"Expected new element in path \"{elementPath}\" in Unity Library manifest");

        // Validate that the intent isn't created in XR Library manifest
        var xrLibManifest = GetXrLibraryManifest();
        var emptyNodes = xrLibManifest.SelectNodes(elementPath);
        Assert.AreEqual(
            0,
            emptyNodes.Count,
            $"Expected no new element in path \"{elementPath}\" in XR Library manifest");
    }

    [Test]
    public void AndroidManifestProcessor_KeepOnlyOneIntentOfTheSameType()
    {
        var processor = CreateProcessor();

        // Initialize data
        var newElementAttributes = new Dictionary<string, string>()
        {
            { "name", "com.oculus.intent.category.VR" }
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                NewElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = k_categoryPath,
                        Attributes = newElementAttributes
                    }
                }
            })
        };

        // Prepare test document
        var appManifest = GetUnityLibraryManifest();
        appManifest.CreateNewElement(k_categoryPath, newElementAttributes);
        appManifest.Save();

        // Execute
        processor.ProcessManifestRequirements(providers);

        var elementPath = string.Join("/", k_categoryPath);

        // Validate that only one intent of the same kind is in the manifest
        var unityLibManifest = GetUnityLibraryManifest();
        var addedNodes = unityLibManifest.SelectNodes(elementPath);
        Assert.AreEqual(
            1,
            addedNodes.Count,
            $"Expected only 1 element in path \"{elementPath}\" in Unity Library manifest");

    }

    [Test]
    public void AndroidManifestProcessor_AddManyIntentsOfTheSameTypeButKeepOnlyOne()
    {
        var processor = CreateProcessor();

        // Initialize data
        var newElementAttributes = new Dictionary<string, string>()
        {
            { "name", "com.oculus.intent.category.VR" }
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type>
                {
                    supportedLoaderType
                },
                NewElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = k_categoryPath,
                        Attributes = newElementAttributes
                    },
                    new ManifestElement()
                    {
                        ElementPath = k_categoryPath,
                        Attributes = newElementAttributes
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        var elementPath = string.Join("/", k_categoryPath);

        // Validate that only one intent of the same kind is in the manifest
        var unityLibManifest = GetUnityLibraryManifest();
        var addedNodes = unityLibManifest.SelectNodes(elementPath);
        Assert.AreEqual(
            1,
            addedNodes.Count,
            $"Expected only 1 element in path \"{elementPath}\" in Unity Library manifest");

    }

    [Test]
    public void AndroidManifestProcessor_OverrideElementsWithDifferentNamesAreNotCollapsed()
    {
        var processor = CreateProcessor();

        var elementPath = new List<string> { "manifest", "application", "meta-data" };
        var firstAttributes = new Dictionary<string, string>()
        {
            { "name", "com.example.first" },
            { "value", "first-value" },
        };
        var secondAttributes = new Dictionary<string, string>()
        {
            { "name", "com.example.second" },
            { "value", "second-value" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> { supportedLoaderType },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement() { ElementPath = elementPath, Attributes = firstAttributes },
                    new ManifestElement() { ElementPath = elementPath, Attributes = secondAttributes }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", elementPath));
        Assert.AreEqual(
            2,
            nodes.Count,
            "Expected 2 distinct elements with different names");

        bool foundFirst = false;
        bool foundSecond = false;
        foreach (XmlElement node in nodes)
        {
            var nameValue = node.GetAttribute("name", k_androidXmlNamespace);
            foundFirst |= "com.example.first".Equals(nameValue);
            foundSecond |= "com.example.second".Equals(nameValue);
        }
        Assert.IsTrue(foundFirst, "First named element not found");
        Assert.IsTrue(foundSecond, "Second named element not found");
    }

    [Test]
    public void AndroidManifestProcessor_MergeOverrideElementsResolvesRequiredToTrue()
    {
        var processor = CreateProcessor();

        var elementPath = new List<string> { "manifest", "uses-feature" };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> { supportedLoaderType },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = elementPath,
                        Attributes = new Dictionary<string, string>()
                        {
                            { "name", "feature.x" },
                            { "required", "false" },
                        }
                    },
                    new ManifestElement()
                    {
                        ElementPath = elementPath,
                        Attributes = new Dictionary<string, string>()
                        {
                            { "name", "feature.x" },
                            { "required", "true" },
                        }
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", elementPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Expected same-named elements to be merged into 1");
        Assert.AreEqual(
            "true",
            ((XmlElement)nodes[0]).GetAttribute("required", k_androidXmlNamespace),
            "Expected required attribute to resolve to true");
    }

    [Test]
    public void AndroidManifestProcessor_MergeOverrideElementsKeepsHighestVersion()
    {
        var processor = CreateProcessor();

        var elementPath = new List<string> { "manifest", "uses-feature" };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> { supportedLoaderType },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = elementPath,
                        Attributes = new Dictionary<string, string>()
                        {
                            { "name", "feature.y" },
                            { "version", "1.2.0" },
                        }
                    },
                    new ManifestElement()
                    {
                        ElementPath = elementPath,
                        Attributes = new Dictionary<string, string>()
                        {
                            { "name", "feature.y" },
                            { "version", "1.10.0" },
                        }
                    },
                    new ManifestElement()
                    {
                        ElementPath = elementPath,
                        Attributes = new Dictionary<string, string>()
                        {
                            { "name", "feature.y" },
                            { "version", "1.9.0" },
                        }
                    }
                }
            })
        };

        // Execute
        processor.ProcessManifestRequirements(providers);

        // Validate
        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", elementPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Expected same-named elements to be merged into 1");
        Assert.AreEqual(
            "1.10.0",
            ((XmlElement)nodes[0]).GetAttribute("version", k_androidXmlNamespace),
            "Expected the highest version to be kept");
    }

    [Test]
    public void AndroidManifestProcessor_OverrideElementWithMatchingNameUpdatesExistingNodeInPlace()
    {
        var processor = CreateProcessor();

        var elementPath = new List<string> { "manifest", "application", "meta-data" };
        var existingElementAttributes = new Dictionary<string, string>()
        {
            { "name", "com.example.config" },
            { "value", "old-value" },
        };
        var overrideElementAttributes = new Dictionary<string, string>()
        {
            { "name", "com.example.config" },
            { "value", "new-value" },
            { "extra", "added" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> { supportedLoaderType },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement() { ElementPath = elementPath, Attributes = overrideElementAttributes }
                }
            })
        };

        // Prepare test document with an existing element sharing the override's name
        var libManifest = GetXrLibraryManifest();
        libManifest.CreateNewElement(elementPath, existingElementAttributes);
        libManifest.Save();

        processor.ProcessManifestRequirements(providers);

        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", elementPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Expected the named element to be updated in place, not duplicated into a new sibling");

        var node = (XmlElement)nodes[0];
        Assert.AreEqual(
            "com.example.config",
            node.GetAttribute("name", k_androidXmlNamespace),
            "Expected the element name to be preserved");
        Assert.AreEqual(
            "new-value",
            node.GetAttribute("value", k_androidXmlNamespace),
            "Expected the existing attribute to be overridden in place");
        Assert.AreEqual(
            "added",
            node.GetAttribute("extra", k_androidXmlNamespace),
            "Expected the new attribute to be added to the existing element");
    }

    [Test]
    public void AndroidManifestProcessor_UnnamedOverrideElementAppliesToAllSiblingsIncludingNamedOnes()
    {
        var processor = CreateProcessor();

        var elementPath = new List<string> { "manifest", "application", "meta-data" };
        var alphaAttributes = new Dictionary<string, string>()
        {
            { "name", "com.example.alpha" },
            { "value", "alpha" },
        };
        var betaAttributes = new Dictionary<string, string>()
        {
            { "name", "com.example.beta" },
            { "value", "beta" },
        };
        var unnamedAttributes = new Dictionary<string, string>()
        {
            { "shared", "shared-value" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> { supportedLoaderType },
                // Order matters: the named elements are created first as distinct siblings, then the unnamed
                // override (which has no name attribute) falls into the branch that overrides every matching
                // sibling at this path, including the named ones.
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement() { ElementPath = elementPath, Attributes = alphaAttributes },
                    new ManifestElement() { ElementPath = elementPath, Attributes = betaAttributes },
                    new ManifestElement() { ElementPath = elementPath, Attributes = unnamedAttributes },
                }
            })
        };

        processor.ProcessManifestRequirements(providers);

        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", elementPath));
        Assert.AreEqual(
            2,
            nodes.Count,
            "Expected the two distinct named elements to remain separate siblings");

        bool foundAlpha = false;
        bool foundBeta = false;
        foreach (XmlElement node in nodes)
        {
            var nameValue = node.GetAttribute("name", k_androidXmlNamespace);
            foundAlpha |= "com.example.alpha".Equals(nameValue);
            foundBeta |= "com.example.beta".Equals(nameValue);

            Assert.AreEqual(
                "shared-value",
                node.GetAttribute("shared", k_androidXmlNamespace),
                $"Expected the unnamed override to apply to the named sibling \"{nameValue}\"");
        }
        Assert.IsTrue(foundAlpha, "Named element \"com.example.alpha\" not found");
        Assert.IsTrue(foundBeta, "Named element \"com.example.beta\" not found");
    }

    [Test]
    public void AndroidManifestProcessor_OverrideDoesNotResolveRequiredOrVersionAgainstExistingAttributes()
    {
        var processor = CreateProcessor();

        // The required/version conflict resolution only applies while merging the elements provided by requirement
        // providers; it is not applied against attributes already present in the manifest. OverrideNodeAttributes
        // raw-sets the override values, so the override wins even when it lowers the value.
        var elementPath = new List<string> { "manifest", "uses-feature" };
        var existingElementAttributes = new Dictionary<string, string>()
        {
            { "name", "feature.z" },
            { "required", "true" },
            { "version", "2.0.0" },
        };
        var overrideElementAttributes = new Dictionary<string, string>()
        {
            { "name", "feature.z" },
            { "required", "false" },
            { "version", "1.0.0" },
        };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> { supportedLoaderType },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement() { ElementPath = elementPath, Attributes = overrideElementAttributes }
                }
            })
        };

        // Prepare test document with an existing element sharing the override's name
        var libManifest = GetXrLibraryManifest();
        libManifest.CreateNewElement(elementPath, existingElementAttributes);
        libManifest.Save();

        processor.ProcessManifestRequirements(providers);

        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", elementPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Expected the named element to be updated in place");

        var node = (XmlElement)nodes[0];
        Assert.AreEqual(
            "false",
            node.GetAttribute("required", k_androidXmlNamespace),
            "Expected the override to win over the pre-existing required attribute (no true-wins resolution)");
        Assert.AreEqual(
            "1.0.0",
            node.GetAttribute("version", k_androidXmlNamespace),
            "Expected the override to win over the pre-existing version attribute (no highest-version resolution)");
    }

    [Test]
    public void AndroidManifestProcessor_MergeOverrideElementsNormalizesSingleIntegerVersion()
    {
        var processor = CreateProcessor();

        // "5" has no minor component, so it is normalized to "5.0" before parsing, which is higher than "4.5.0".
        var elementPath = new List<string> { "manifest", "uses-feature" };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> { supportedLoaderType },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = elementPath,
                        Attributes = new Dictionary<string, string>()
                        {
                            { "name", "feature.single" },
                            { "version", "4.5.0" },
                        }
                    },
                    new ManifestElement()
                    {
                        ElementPath = elementPath,
                        Attributes = new Dictionary<string, string>()
                        {
                            { "name", "feature.single" },
                            { "version", "5" },
                        }
                    }
                }
            })
        };

        processor.ProcessManifestRequirements(providers);

        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", elementPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Expected same-named elements to be merged into 1");
        Assert.AreEqual(
            "5",
            ((XmlElement)nodes[0]).GetAttribute("version", k_androidXmlNamespace),
            "Expected single-integer \"5\" to normalize to \"5.0\" and be kept as the highest version");
    }

    [Test]
    public void AndroidManifestProcessor_MergeOverrideElementsTreatsSingleIntegerVersionAsLessThanThreeComponentVersion()
    {
        var processor = CreateProcessor();

        // "5" normalizes to "5.0", whose build/revision components are unset (-1). System.Version treats those as
        // lower than the explicit "0" components in "5.0.0", so "5.0.0" is considered the higher version.
        var elementPath = new List<string> { "manifest", "uses-feature" };
        var providers = new List<IAndroidManifestRequirementProvider>()
        {
            new MockManifestRequirementProvider(new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> { supportedLoaderType },
                OverrideElements = new List<ManifestElement>()
                {
                    new ManifestElement()
                    {
                        ElementPath = elementPath,
                        Attributes = new Dictionary<string, string>()
                        {
                            { "name", "feature.threeComponent" },
                            { "version", "5" },
                        }
                    },
                    new ManifestElement()
                    {
                        ElementPath = elementPath,
                        Attributes = new Dictionary<string, string>()
                        {
                            { "name", "feature.threeComponent" },
                            { "version", "5.0.0" },
                        }
                    }
                }
            })
        };

        processor.ProcessManifestRequirements(providers);

        var updatedLibraryManifest = GetXrLibraryManifest();
        var nodes = updatedLibraryManifest.SelectNodes(string.Join("/", elementPath));
        Assert.AreEqual(
            1,
            nodes.Count,
            "Expected same-named elements to be merged into 1");
        Assert.AreEqual(
            "5.0.0",
            ((XmlElement)nodes[0]).GetAttribute("version", k_androidXmlNamespace),
            "Expected \"5.0.0\" to be kept since \"5\" (5.0) compares as less than \"5.0.0\"");
    }

    private AndroidManifestDocument GetXrLibraryManifest()
    {
        return new AndroidManifestDocument(xrLibraryManifestFilePath);
    }

    private AndroidManifestDocument GetUnityLibraryManifest()
    {
        return new AndroidManifestDocument(unityLibraryManifestFilePath);
    }

    private AndroidManifestProcessor CreateProcessor()
    {
        return new AndroidManifestProcessor(
            tempProjectPath,
            tempProjectPath,
            mockXrSettings);
    }

    private void CreateMockManifestDocument(string filePath)
    {
        var manifestDocument = new AndroidManifestDocument();
        var manifestNode = manifestDocument.CreateElement("manifest");
        manifestNode.SetAttribute("xmlns:android", k_androidXmlNamespace);
        manifestDocument.AppendChild(manifestNode);
        var applicationNode = manifestDocument.CreateElement("application");
        manifestNode.AppendChild(applicationNode);
        manifestDocument.SaveAs(filePath);
    }

    private void AssertAttributesAreEqual(
        string elementName,
        Dictionary<string, string> expectedAttributes,
        XmlAttributeCollection attributes)
    {
        foreach (XmlAttribute attrib in attributes)
        {
            var attributeName = attrib.Name.Split(':').Last(); // Values are returned with preffixed namespace name, pick only the attribute name
            if (!expectedAttributes.Contains(new KeyValuePair<string, string>(attributeName, attrib.Value)))
            {
                Assert.Fail($"Unexpected attribute \"{attrib.Name}\" " +
                    $"with value \"{attrib.Value}\" found in element {elementName}");
            }
        }
    }

    private Dictionary<TKey, TValue> MergeDictionaries<TKey, TValue>(Dictionary<TKey, TValue> dict1, Dictionary<TKey, TValue> dict2)
    {
        return new List<Dictionary<TKey, TValue>> { dict1, dict2 }
        .SelectMany(dict => dict)
        .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private void IgnoreIfGameActivityIsNotSupported()
    {
#if !UNITY_2023_1_OR_NEWER
        Assert.Ignore("Ignoring test as GameActivity is not supported in Unity versions before 2023.1");
#endif
    }

    private class MockManifestRequirementProvider : IAndroidManifestRequirementProvider
    {
        private readonly ManifestRequirement requirement;

        public MockManifestRequirementProvider(ManifestRequirement mockRequirments)
        {
            requirement = mockRequirments;
        }

        public ManifestRequirement ProvideManifestRequirement()
        {
            return requirement;
        }
    }

    private class MockXrLoader : XRLoader
    {
        public override T GetLoadedSubsystem<T>()
        {
            throw new NotImplementedException();
        }
    }
}
