using System;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;
using XNative.Core.Games;
using XNative.Core.Logging;

namespace XNative.Wine.Runtime
{
    /// <summary>
    /// The single launch pipeline shared by
    /// <see cref="CompatibilityGameRunner"/> and
    /// <see cref="WineCompatibilityLayer"/>. The pipeline validates the
    /// request in a fixed order and always ends in a structured
    /// <see cref="LaunchResult"/> failure: no compatibility backend exists
    /// yet, and the UWP sandbox forbids loading foreign executables, so
    /// claiming success here would be a lie. Precise failure is the
    /// product. Any dependency may be <c>null</c>, in which case its step
    /// is skipped; with all dependencies <c>null</c> the pipeline
    /// reproduces the exact legacy stub results.
    /// </summary>
    internal static class CompatibilityLaunchPipeline
    {
        /// <summary>
        /// Runs the launch pipeline for one game.
        /// </summary>
        /// <param name="fileSystem">File system used for the existence check, or <c>null</c> to skip it.</param>
        /// <param name="profileStore">Compatibility database, or <c>null</c> to launch unprofiled.</param>
        /// <param name="log">Log sink receiving every outcome, or <c>null</c> to skip logging.</param>
        /// <param name="game">The game to launch.</param>
        /// <param name="options">Per-launch options, or <c>null</c> for defaults.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The structured outcome of the launch attempt.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="game"/> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public static Task<LaunchResult> LaunchAsync(
            IFileSystemService? fileSystem,
            IGameProfileStore? profileStore,
            ILogService? log,
            GameInfo game,
            LaunchOptions? options,
            CancellationToken cancellationToken)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            cancellationToken.ThrowIfCancellationRequested();

            LaunchOptions effectiveOptions = options ?? LaunchOptions.Default;

            Log(log, LogLevel.Information, "Launch requested for '" + game.Name + "' (" + game.ExecutablePath + ").");

            if (game.ExecutableType != ExecutableType.WindowsExecutable)
            {
                return Fail(log, "Executable type '" + game.ExecutableType + "' is not supported.", LaunchFailureReason.UnsupportedExecutableType);
            }

            if (fileSystem != null && !fileSystem.FileExists(game.ExecutablePath))
            {
                return Fail(log, "Executable file not found: '" + game.ExecutablePath + "'.", LaunchFailureReason.ExecutableNotFound);
            }

            GameProfile? profile = ResolveProfile(profileStore, log, game, effectiveOptions);

            string arguments = effectiveOptions.Arguments ?? game.LaunchArguments ?? profile?.RecommendedArguments ?? string.Empty;
            Log(log, LogLevel.Information, arguments.Length == 0
                ? "Launch arguments for '" + game.Name + "': (none)."
                : "Launch arguments for '" + game.Name + "': '" + arguments + "'.");

            if (profile != null)
            {
                Log(log, LogLevel.Information, "Compatibility profile '" + profile.Id + "' for '" + game.Name + "' has rating " + profile.Rating + ".");

                if (profile.Rating == CompatibilityRating.Broken)
                {
                    string brokenMessage = "Game '" + game.Name + "' is known to be broken (profile '" + profile.Id + "').";
                    if (!string.IsNullOrWhiteSpace(profile.Notes))
                    {
                        brokenMessage += " " + profile.Notes;
                    }

                    return Fail(log, brokenMessage, LaunchFailureReason.KnownBroken);
                }
            }

            string backendMessage = profile != null
                ? "Windows executable detected. Compatibility backend not implemented. Profile rating: " + profile.Rating + "."
                : "Windows executable detected. Compatibility backend not implemented.";
            return Fail(log, backendMessage, LaunchFailureReason.BackendNotImplemented);
        }

        private static GameProfile? ResolveProfile(
            IGameProfileStore? profileStore,
            ILogService? log,
            GameInfo game,
            LaunchOptions options)
        {
            if (profileStore == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(options.ProfileId))
            {
                foreach (GameProfile candidate in profileStore.GetProfiles())
                {
                    if (string.Equals(candidate.Id, options.ProfileId, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }

                Log(log, LogLevel.Warning, "Compatibility profile '" + options.ProfileId + "' was requested for '" + game.Name + "' but was not found; continuing without the requested profile.");
                return null;
            }

            return profileStore.FindForGame(game);
        }

        private static Task<LaunchResult> Fail(ILogService? log, string message, LaunchFailureReason reason)
        {
            Log(log, LogLevel.Warning, message);
            return Task.FromResult(LaunchResult.Fail(message, reason));
        }

        private static void Log(ILogService? log, LogLevel level, string message)
        {
            if (log != null)
            {
                log.Log(level, message);
            }
        }
    }
}
