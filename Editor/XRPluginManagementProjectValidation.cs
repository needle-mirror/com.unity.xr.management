using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.XR.CoreUtils.Editor;
using UnityEngine;
#if INPUT_SYSTEM_1_4_OR_NEWER
using UnityEngine.InputSystem;
#endif

namespace UnityEditor.XR.Management
{
    /// <summary>
    /// Unity Editor class which registers Project Validation rules for XR Plugin Management.
    /// These are global rules which apply no matter the plug-in provider enabled.
    /// </summary>
    static class XRPluginManagementProjectValidation
    {
        static readonly string k_Category = XRConstants.k_XRPluginManagement;

        const string k_RunInBackgroundMessage = "Run In Background must be enabled or Input System Background Behavior changed to avoid head-locked rendering of the main camera when focus is lost, such as when the universal menu/system shell is opened.";

        static readonly BuildTargetGroup[] s_BuildTargetGroups =
            ((BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup))).Distinct().ToArray();

        static readonly List<BuildValidationRule> s_BuildValidationRules = new List<BuildValidationRule>
        {
#if INPUT_SYSTEM_1_4_OR_NEWER
            new BuildValidationRule
            {
                IsRuleEnabled = () => GetPlayerSettingsProperty("runInBackground") != null && GetInputSettingsProperty("m_BackgroundBehavior") != null,
                Category = k_Category,
                Message = k_RunInBackgroundMessage,
                CheckPredicate = () => PlayerSettings.runInBackground && IsBackgroundBehaviorValid(),
                FixIt = () =>
                {
                    SetRunInBackground(true);

                    if (!IsBackgroundBehaviorValid())
                    {
                        // Don't modify the input settings asset if it is the default one that is not editable.
                        // A user must click **Create settings asset** in the Input System Package project settings.
                        // This protects against the Input System package changing the default background behavior setting value.
                        if (IsInputSettingsEditable())
                            SetBackgroundBehavior(InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices);
                        else
                            SettingsService.OpenProjectSettings("Project/Input System Package");
                    }
                },
                FixItAutomatic = IsInputSettingsEditable() || IsBackgroundBehaviorValid(),
                HelpText = !IsInputSettingsEditable() && !IsBackgroundBehaviorValid()
                    ? "Go to Edit > Project Settings > Input System Package and select Create settings asset to allow automatic fix."
                    : null,
                FixItMessage = "Go to Edit > Project Settings > Player > Resolution and Presentation and enable Run In Background." +
                    "\nGo to Edit > Project Settings > Input System Package and set Background Behavior to Reset And Disable Non Background Devices.",
                Error = false,
            },
#else
            new BuildValidationRule
            {
                IsRuleEnabled = () => GetPlayerSettingsProperty("runInBackground") != null,
                Category = k_Category,
                Message = k_RunInBackgroundMessage,
                CheckPredicate = () => PlayerSettings.runInBackground,
                FixIt = () => SetRunInBackground(true),
                FixItMessage = "Go to Edit > Project Settings > Player > Resolution and Presentation and enable Run In Background.",
            },
#endif
        };

        [InitializeOnLoadMethod]
        static void RegisterProjectValidationRules()
        {
            foreach (var buildTargetGroup in s_BuildTargetGroups)
            {
                BuildValidator.AddRules(buildTargetGroup, s_BuildValidationRules);
            }
        }

        static SerializedProperty GetPlayerSettingsProperty(string propertyPath)
        {
            var serializedObject = (SerializedObject)typeof(PlayerSettings).GetMethod("GetSerializedObject", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
            return serializedObject?.FindProperty(propertyPath);
        }

        static void SetRunInBackground(bool value)
        {
            // Setting PlayerSettings.runInBackground directly does not properly create undo/redo state,
            // so do so through the SerializedProperty API.
            var prop = GetPlayerSettingsProperty("runInBackground");
            if (prop == null)
                return;

            prop.serializedObject.Update();
            prop.boolValue = value;
            prop.serializedObject.ApplyModifiedProperties();
        }

#if INPUT_SYSTEM_1_4_OR_NEWER
        static SerializedProperty GetInputSettingsProperty(string propertyPath)
        {
            var serializedObject = new SerializedObject(InputSystem.settings);
            return serializedObject.FindProperty(propertyPath);
        }

        static void SetBackgroundBehavior(InputSettings.BackgroundBehavior backgroundBehavior)
        {
            // Setting InputSystem.settings.backgroundBehavior directly does not properly create undo/redo state,
            // so do so through the SerializedProperty API.
            var prop = GetInputSettingsProperty("m_BackgroundBehavior");
            if (prop == null)
                return;

            prop.serializedObject.Update();
            prop.intValue = (int)backgroundBehavior;
            prop.serializedObject.ApplyModifiedProperties();
        }

        static bool IsInputSettingsEditable()
        {
            return (InputSystem.settings.hideFlags & HideFlags.HideAndDontSave) == 0;
        }

        static bool IsBackgroundBehaviorValid()
        {
            var backgroundBehavior = InputSystem.settings.backgroundBehavior;
            return backgroundBehavior == InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices ||
                backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus;
        }
#endif
    }
}
