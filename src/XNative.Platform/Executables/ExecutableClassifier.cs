using System;
using XNative.Core.Devices;
using XNative.Core.Games;

namespace XNative.Platform.Executables
{
    /// <summary>
    /// Maps a <see cref="PeInfo"/> inspection result onto the core domain
    /// vocabulary (<see cref="ExecutableType"/> and
    /// <see cref="ProcessorArchitecture"/>) used by the game catalog.
    /// </summary>
    public static class ExecutableClassifier
    {
        /// <summary>
        /// Classifies an inspected file.
        /// </summary>
        /// <param name="info">The inspection result to classify.</param>
        /// <returns>
        /// A tuple of the executable type and the architecture. A valid PE
        /// image (executable or DLL) maps to
        /// <see cref="ExecutableType.WindowsExecutable"/> together with the
        /// parsed architecture; an invalid inspection maps to
        /// (<see cref="ExecutableType.Unknown"/>,
        /// <see cref="ProcessorArchitecture.Unknown"/>).
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="info"/> is <c>null</c>.</exception>
        public static (ExecutableType ExecutableType, ProcessorArchitecture Architecture) Classify(PeInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            if (!info.IsValid)
            {
                return (ExecutableType.Unknown, ProcessorArchitecture.Unknown);
            }

            return (ExecutableType.WindowsExecutable, info.Architecture);
        }
    }
}
