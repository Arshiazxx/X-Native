using System;
using System.Threading.Tasks;

namespace XNative.App
{
    /// <summary>
    /// Development host of X-Native. It exists so that the composition root
    /// can be executed and verified outside of the Xbox environment.
    /// Running it performs an end-to-end smoke test of the whole stack on
    /// a desktop: it builds the container, prints the resolved modules and
    /// the device description, then scans the games root with the game
    /// catalog. Subcommands (<c>scan</c>, <c>inspect</c>, <c>launch</c>,
    /// <c>settings</c>) turn it into a small diagnostic CLI; see
    /// <see cref="Cli"/> for the full command list. All behaviour lives in
    /// <see cref="Cli"/> so the commands can be tested without a console.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point of the development host.
        /// </summary>
        /// <param name="args">
        /// The command and its argument, as parsed by
        /// <see cref="CommandLine.Parse(string[])"/>. With no arguments the
        /// <c>info</c> command runs against the default games root.
        /// </param>
        /// <returns>
        /// Zero when the command succeeded; a non-zero exit code when it
        /// failed, with a clear message written to standard error.
        /// </returns>
        public static async Task<int> Main(string[] args)
        {
            return await Cli.RunAsync(args, Console.Out, Console.Error).ConfigureAwait(false);
        }
    }
}
