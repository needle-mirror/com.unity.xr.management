# End Users

## Add default loader and settings instances if requested

At package install time, the package may prompt you to create an instance of a loader and an instance of settings for the package. This step is entirely optional and is there simply to help the user get things going at installation time.

If you wish not to create this at installation time, the appropriate portion of the editor that require them (**XRManager** and **Unified Settings** will) prompt you to create them as well.

## Add an **XRManager** instance to your scene

### If you wish to start XR SDK on a per scene basis (i.e. start in 2D and transition into VR)
* Create a new empty Game Object in your scene.
* Add an instance of the **XRManager** component to this new Game Object.
* Use the **XRManager** component Inspector UI to add/create, remove and reorder the loaders you wish to use.

When the scene loads, XR Manager will attempt to create and start the set of subsystems for the first loader that successfully initializes. Unless otherwise specified, XR Manager will manage the lifetime of the XR SDK session within the life time of the scene.

### If you wish to start XR SDK at launch and keep it running throughout the app lifetime.
* Open and scene and create an empty **Game Object**. Add an **XR Manager** component to that **Game Object**. 
* Drag the **Game Object** to the Project heirarchy and create a prefab from that instance. Delete the **Game Object** instance from the scene.
* Open the prefab in the Prefab Editor and go to the inspector for the prefab.
* Populate the **XR Manager** instance with the set of loaders you wish to use for your application.
* Navigate to **Unified Settings**.
* Select the top level **XR** node. Drag the prefab to the XR Manager Instance and drop it. This prefab is now assigned to the global XR settings system and will be used to manage the lifetime of the XR SDK for the lifetime of the application.

The instance of the **Game Object** that contains the **XR Manager** component instance you wish to use can be set/accessed using **XRGeneralSettings.Instance.m_LoaderManagerInstance**. This allows you to change the prefab instance that you want to use at build time so that you can change loader configuration depending on build target.

**NOTE**: You can always manually control the XR SDK system by accessing the **XRManager.activeLoader** field once XR SDK has been initialized.

## Customize build and runtime settings

Any package that needs build or runtime settings should provide a settings datatype for use. This will be surfaced in the **Unified Settings** UI window underneath a top level **XR** node. By default a custom settings data instance will not be created. If you wish to modify build or runtime settings for the package you must go to the package authors entry in **Unified Settings** and select **Create**. This will create an instance of the settings that you can then modify inside of **Unified Settings**.

# Installing *XR SDK Management*

Most likey the XR SDK Provider package you want to use already includes XR Management so you shouldn't need to install it. If you do you can follow the directions provided in the top level documentation or follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest/index.html).


## Document revision history

|Date|Reason|
|---|---|
|March 8, 2019|Create document.|
