# End Users

## Add default loader and settings instances if requested

At package install time, the package may prompt you to create an instance of a loader and an instance of settings for the package. This step is entirely optional and is there simply to help the user get things going at installation time.

If you wish not to create this at installation time, the appropriate portion of the editor that require them (**XRManagerSettingsSettings** and **Unified Settings** will) prompt you to create them as well.

### Set up XR SDK settings with loaders you want to run.
* Navigate to **Unified Settings**.
* Select the top level **XR** node.
* Modify loaders

**NOTE**: You can always manually control the XR SDK system by accessing the **XRManagerSettingsSettings.activeLoader** field once XR SDK has been initialized.

### If you wish to start XR SDK on a per scene basis (i.e. start in 2D and transition into VR)
* Use the **XRGeneralSettings.Instance.m_LoaderManagerInstance** to add/create, remove and reorder the loaders you wish to use from the script.

## Customize build and runtime settings

Any package that needs build or runtime settings should provide a settings datatype for use. This will be surfaced in the **Unified Settings** UI window underneath a top level **XR** node. By default a custom settings data instance will not be created. If you wish to modify build or runtime settings for the package you must go to the package authors entry in **Unified Settings** and select **Create**. This will create an instance of the settings that you can then modify inside of **Unified Settings**.

# Installing *XR SDK Management*

Most likey the XR SDK Provider package you want to use already includes XR Management so you shouldn't need to install it. If you do you can follow the directions provided in the top level documentation or follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest/index.html).


## Document revision history

|Date|Reason|
|---|---|
|March 8, 2019|Create document.|
