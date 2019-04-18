# End Users

## Add default loader and settings instances if requested

At package install time, the package may prompt you to create an instance of a loader and an instance of settings for the package. This step is entirely optional and is there simply to help the user get things going at installation time.

If you wish not to create this at installation time, the appropriate portion of the editor that require them (**XRManagerSettingsSettings** and **Unified Settings** will) prompt you to create them as well.

### Set up XR SDK settings with loaders you want to run.
* Navigate to **Unified Settings**.
* Select the top level **XR** node.
* Modify loaders

**NOTE**: You can always manually control the XR SDK system by accessing the **XRManagerSettingsSettings.activeLoader** field once XR SDK has been initialized.

### Automatic manager loading of XR
By default XR Management will automatically initialize and start your XR environment on application load. At runtime this happens immediately before first scene load. In Play mode this happens immediately after first scene load but before Start is called on your game objects. In either case XR should be setup before Start is called so you should be able to query the state of XR in the Start method of your game objects.

### If you wish to start XR SDK on a per scene basis (i.e. start in 2D and transition into VR)
* Make sure you disable the **Initialize on Startup** toggle for each platform you support.
* At rutime use the **XRGeneralSettings.Instance.m_LoaderManagerInstance** to add/create, remove and reorder the loaders you wish to use from the script.
* To setup the XR environment to run manually call **InitializeLoader(Async)** on the **m_LoaderManagerInstance**.
* To start call **StartSubsystems** on **m_LoaderManagerInstance**. This will put you into XR mode.
* To stop call **StopSubsystems** on the **m_LoaderManagerInstance** to stop XR. This will take you out of XR but should allow you to call **StartSubsystems** again to restart XR.
* To shutdown XR entirely, call **DeinitializeLoader** on the **m_LoaderManagerInstance**. This will clean up the XR environment and remove XR entirely. You must call **InitializeLoader(Async)** before you can run XR again.

## Customize build and runtime settings

Any package that needs build or runtime settings should provide a settings datatype for use. This will be surfaced in the **Unified Settings** UI window underneath a top level **XR** node. By default a custom settings data instance will not be created. If you wish to modify build or runtime settings for the package you must go to the package authors entry in **Unified Settings** and select **Create**. This will create an instance of the settings that you can then modify inside of **Unified Settings**.

# Installing *XR SDK Management*

Most likey the XR SDK Provider package you want to use already includes XR Management so you shouldn't need to install it. If you do you can follow the directions provided in the top level documentation or follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest/index.html).


## Document revision history

|Date|Reason|
|---|---|
|April 15th, 2019|Update documentation to outline init expectations and clarify steps around manual use of management.|
|March 8, 2019|Create document.|
