using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.XR.Management
{
    /// <summary>
    /// XR Loader abstract class used as a base class for specific provider implementations. Providers should implement
    /// subclasses of this to provide specific initialization and management implementations that make sense for their supported
    /// scenarios and needs.
    /// </summary>
    public abstract class XRLoader : ScriptableObject
    {
        /// <summary>
        /// Create all subsystems that this loader supports.
        /// </summary>
        /// <returns>`true` if initialization succeeded. Otherwise, `false`.</returns>
        /// <remarks>
        /// This is the only method on XRLoader that Management uses to determine the active loader to use. If this
        /// method returns `true`, Management locks this loader as the <see cref="XRManagerSettings.activeLoader"/>
        /// and and stops fall through processing on the <see cref="XRManagerSettings.loaders"/> list of current loaders.
        ///
        /// If this method returns `false`, <see cref="XRManagerSettings"/> continues to process the next loader
        /// in the <see cref="XRManagerSettings.loaders"/> list, or fails completely when the list is exhausted.
        /// </remarks>
        public virtual bool Initialize() { return true; }

        /// <summary>
        /// Start all subsystems that were created by this loader.
        /// </summary>
        /// <returns>`true` if all subsystems were successfully started. Otherwise, `false`.</returns>
        public virtual bool Start() { return true; }

        /// <summary>
        /// Stop all subsystems that were created by this loader.
        /// </summary>
        /// <returns>`true` if all subsystems were successfully stopped. Otherwise, `false`.</returns>
        public virtual bool Stop() { return true; }

        /// <summary>
        /// Destroy all subsystems that were created by this loader.
        /// </summary>
        /// <returns>`true` if all subsystems were successfully destroyed. Otherwise, `false`.</returns>
        public virtual bool Deinitialize() { return true; }

        /// <summary>
        /// Gets the loaded subsystem of the specified type. (Implementation-dependent)
        /// </summary>
        /// <typeparam name="T">Type of the subsystem to get.</typeparam>
        /// <returns>The loaded subsystem, or `null` if not found.</returns>
        public abstract T GetLoadedSubsystem<T>() where T : class, ISubsystem;

        /// <summary>
        /// Gets the loader's supported graphics device types. If the list is empty, it is assumed that it supports all
        /// graphics device types.
        /// </summary>
        /// <param name="buildingPlayer">`true` if the player is being built. Otherwise, `false`.</param>
        /// <returns>The loader's supported graphics device types.</returns>
        public virtual List<GraphicsDeviceType> GetSupportedGraphicsDeviceTypes(bool buildingPlayer)
        {
            return new List<GraphicsDeviceType>();
        }
    }
}
