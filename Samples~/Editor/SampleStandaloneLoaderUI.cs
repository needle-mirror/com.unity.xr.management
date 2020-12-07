using System;

using UnityEditor;
using UnityEditor.XR.Management;

using UnityEngine;

namespace Samples
{
    [XRCustomLoaderUI("Samples.SampleLoader", BuildTargetGroup.Standalone)]
    public class SampleStandaloneLoaderUI : IXRCustomLoaderUI
    {
        static readonly string[] features = new string[]{
            "Feature One",
            "Feature Two",
            "Feature Three"
        };

        struct Content
        {
            public static readonly GUIContent k_LoaderName = new GUIContent("Sample Loader One Custom <SAMPLE ONLY YOU MUST REIMPLEMENT>");
            public static readonly GUIContent k_Download = new GUIContent("Download");
            public static readonly GUIContent k_WarningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
        }

        private float renderLineHeight =0;

        public bool IsLoaderEnabled { get; set; }

        public string[] IncompatibleLoaders => new string[] { "UnityEngine.XR.WindowsMR.WindowsMRLoader" };

        public float RequiredRenderHeight { get; private set; }

        public void SetRenderedLineHeight(float height)
        {
            renderLineHeight = height;
            RequiredRenderHeight = height;

            if (IsLoaderEnabled)
            {
                RequiredRenderHeight += features.Length * height;
            }
        }

        public BuildTargetGroup ActiveBuildTargetGroup { get; set; }

        public void OnGUI(Rect rect)
        {
            var size = EditorStyles.toggle.CalcSize(Content.k_LoaderName);
            var labelRect = new Rect(rect);
            labelRect.width = size.x;
            labelRect.height = renderLineHeight;
            IsLoaderEnabled = EditorGUI.ToggleLeft(labelRect, Content.k_LoaderName, IsLoaderEnabled);

            size = EditorStyles.label.CalcSize(Content.k_WarningIcon);
            var imageRect = new Rect(rect);
            imageRect.xMin = labelRect.xMax + 1;
            imageRect.width = size.y;
            imageRect.height = renderLineHeight;
            var iconWithTooltip = new GUIContent("", Content.k_WarningIcon.image, "Warning: This is tooltip text!");
            EditorGUI.LabelField(imageRect, iconWithTooltip);

            if (IsLoaderEnabled)
            {
                EditorGUI.indentLevel++;
                var featureRect = new Rect(rect);
                featureRect.yMin = labelRect.yMax + 1;
                featureRect.height = renderLineHeight;
                foreach (var feature in features)
                {
                    var buttonSize = EditorStyles.toggle.CalcSize(Content.k_Download);

                    var featureLabelRect = new Rect(featureRect);
                    featureLabelRect.width -= buttonSize.x;
                    EditorGUI.ToggleLeft(featureLabelRect, feature, false);

                    var buttonRect = new Rect(featureRect);
                    buttonRect.xMin = featureLabelRect.xMax + 1;
                    buttonRect.width = buttonSize.x;
                    if (GUI.Button(buttonRect, Content.k_Download))
                    {
                        Debug.Log($"{feature} download button pressed. Do something here!");
                    }

                    featureRect.yMin += renderLineHeight;
                    featureRect.height = renderLineHeight;
                }
                EditorGUI.indentLevel--;
            }
        }
    }
}
