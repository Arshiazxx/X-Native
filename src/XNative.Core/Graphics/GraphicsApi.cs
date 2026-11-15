using System;

namespace XNative.Core.Graphics
{
    /// <summary>
    /// Graphics APIs a backend can accept. Flags, because one backend
    /// commonly translates several source APIs.
    /// </summary>
    [Flags]
    public enum GraphicsApi
    {
        /// <summary>
        /// No API.
        /// </summary>
        None = 0,

        /// <summary>
        /// Direct3D 9.
        /// </summary>
        Direct3D9 = 1 << 0,

        /// <summary>
        /// Direct3D 11.
        /// </summary>
        Direct3D11 = 1 << 1,

        /// <summary>
        /// Direct3D 12.
        /// </summary>
        Direct3D12 = 1 << 2,

        /// <summary>
        /// OpenGL.
        /// </summary>
        OpenGL = 1 << 3,

        /// <summary>
        /// Vulkan.
        /// </summary>
        Vulkan = 1 << 4,
    }
}
