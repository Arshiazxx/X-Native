namespace XNative.Core.Games
{
    /// <summary>
    /// Result of a game launch operation.
    /// </summary>
    public sealed class LaunchResult
    {
        private LaunchResult(bool success, string? message, int? processId, LaunchFailureReason reason)
        {
            Success = success;
            Message = message;
            ProcessId = processId;
            Reason = reason;
        }

        /// <summary>
        /// Indicates whether the launch was successful.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// A message describing the result.
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// The process ID if the launch succeeded, otherwise null.
        /// </summary>
        public int? ProcessId { get; }

        /// <summary>
        /// Machine-readable reason the launch failed, or
        /// <see cref="LaunchFailureReason.None"/> when it succeeded.
        /// </summary>
        public LaunchFailureReason Reason { get; }

        /// <summary>
        /// Creates a successful launch result.
        /// </summary>
        /// <param name="message">Optional message.</param>
        /// <param name="processId">Optional process ID.</param>
        /// <returns>A successful <see cref="LaunchResult"/>.</returns>
        public static LaunchResult Ok(string? message = null, int? processId = null)
        {
            return new LaunchResult(true, message, processId, LaunchFailureReason.None);
        }

        /// <summary>
        /// Creates a failed launch result.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="reason">Machine-readable failure reason.</param>
        /// <returns>A failed <see cref="LaunchResult"/>.</returns>
        public static LaunchResult Fail(string message, LaunchFailureReason reason = LaunchFailureReason.Unknown)
        {
            return new LaunchResult(false, message, null, reason);
        }
    }
}
