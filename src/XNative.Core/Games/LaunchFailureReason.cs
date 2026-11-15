namespace XNative.Core.Games
{
    /// <summary>
    /// Machine-readable reason a launch failed, so the UI can explain
    /// the failure and suggest a fix instead of showing a raw string.
    /// </summary>
    public enum LaunchFailureReason
    {
        /// <summary>
        /// No failure (the launch succeeded) or no specific reason.
        /// </summary>
        None = 0,

        /// <summary>
        /// The failure reason is not known.
        /// </summary>
        Unknown = 1,

        /// <summary>
        /// No compatibility backend is implemented or available yet.
        /// </summary>
        BackendNotImplemented = 2,

        /// <summary>
        /// The executable type is not one this runner handles.
        /// </summary>
        UnsupportedExecutableType = 3,

        /// <summary>
        /// The executable file does not exist (any more).
        /// </summary>
        ExecutableNotFound = 4,

        /// <summary>
        /// The executable targets a processor architecture the device
        /// or layer cannot run.
        /// </summary>
        UnsupportedArchitecture = 5,

        /// <summary>
        /// The executable is not a valid portable executable.
        /// </summary>
        InvalidExecutable = 6,

        /// <summary>
        /// The platform sandbox forbids what the launch needs (process
        /// creation, loading foreign code, and similar).
        /// </summary>
        BlockedBySandbox = 7,

        /// <summary>
        /// The compatibility profile for the game rates it as broken.
        /// </summary>
        KnownBroken = 8,

        /// <summary>
        /// The launch was cancelled by the caller.
        /// </summary>
        Cancelled = 9,
    }
}
