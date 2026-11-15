using XNative.Core.Devices;
using XNative.Core.Games;
using XNative.UI.ViewModels;
using Xunit;

namespace XNative.UI.Tests.ViewModels
{
    public sealed class GameViewModelDetailTests
    {
        private static GameInfo MakeGame(
            GameMetadata? metadata = null,
            ProcessorArchitecture architecture = ProcessorArchitecture.Unknown,
            string? launchArguments = null,
            ExecutableType executableType = ExecutableType.WindowsExecutable)
        {
            return new GameInfo(
                "detail-id",
                "Detail Game",
                @"C:\Games\Detail\game.exe",
                @"C:\Games\Detail",
                executableType,
                metadata,
                architecture,
                launchArguments);
        }

        [Fact]
        public void MetadataProperties_ComeFromMetadata()
        {
            var metadata = new GameMetadata(
                description: "A detail-rich game",
                publisher: "DetailSoft",
                version: "1.2.3");
            var viewModel = new GameViewModel(MakeGame(metadata));

            Assert.Equal("A detail-rich game", viewModel.Description);
            Assert.Equal("DetailSoft", viewModel.Publisher);
            Assert.Equal("1.2.3", viewModel.Version);
        }

        [Fact]
        public void MetadataProperties_AreEmptyStrings_WhenNoMetadata()
        {
            var viewModel = new GameViewModel(MakeGame());

            Assert.Equal(string.Empty, viewModel.Description);
            Assert.Equal(string.Empty, viewModel.Publisher);
            Assert.Equal(string.Empty, viewModel.Version);
        }

        [Fact]
        public void MetadataProperties_AreEmptyStrings_WhenFieldsMissing()
        {
            var metadata = new GameMetadata(publisher: "DetailSoft");
            var viewModel = new GameViewModel(MakeGame(metadata));

            Assert.Equal(string.Empty, viewModel.Description);
            Assert.Equal("DetailSoft", viewModel.Publisher);
            Assert.Equal(string.Empty, viewModel.Version);
        }

        [Theory]
        [InlineData(ProcessorArchitecture.X64, "x64")]
        [InlineData(ProcessorArchitecture.X86, "x86")]
        [InlineData(ProcessorArchitecture.Arm64, "ARM64")]
        [InlineData(ProcessorArchitecture.Arm, "ARM")]
        [InlineData(ProcessorArchitecture.Unknown, "Unknown")]
        public void ArchitectureText_MapsArchitecture(ProcessorArchitecture architecture, string expected)
        {
            var viewModel = new GameViewModel(MakeGame(architecture: architecture));

            Assert.Equal(expected, viewModel.ArchitectureText);
        }

        [Fact]
        public void WorkingDirectory_Id_AndLaunchArguments_AreExposed()
        {
            var viewModel = new GameViewModel(MakeGame(launchArguments: "-novid"));

            Assert.Equal("detail-id", viewModel.Id);
            Assert.Equal(@"C:\Games\Detail", viewModel.WorkingDirectory);
            Assert.Equal("-novid", viewModel.LaunchArguments);
        }

        [Fact]
        public void DetailText_IncludesArchitecture()
        {
            var viewModel = new GameViewModel(MakeGame(architecture: ProcessorArchitecture.X64));

            Assert.Contains("Path: C:\\Games\\Detail\\game.exe", viewModel.DetailText);
            Assert.Contains("Runner: wine (research)", viewModel.DetailText);
            Assert.Contains("Architecture: x64", viewModel.DetailText);
        }

        [Theory]
        [InlineData(ExecutableType.WindowsExecutable, "wine (research)")]
        [InlineData(ExecutableType.UwpApplication, "uwp")]
        [InlineData(ExecutableType.Unknown, "unknown")]
        public void Runner_MapsExecutableType(ExecutableType executableType, string expected)
        {
            var viewModel = new GameViewModel(MakeGame(executableType: executableType));

            Assert.Equal(expected, viewModel.Runner);
        }

        [Fact]
        public void Runner_ForWindowsExecutable_IsTheWineResearchLabel()
        {
            // Pins the honest label: a Windows executable
            // must not be presented as having a working "wine" runner;
            // the label matches WineCompatibilityLayer.DisplayName.
            var viewModel = new GameViewModel(MakeGame());

            Assert.Equal("wine (research)", viewModel.Runner);
            Assert.Equal("Detail Game (wine (research))", viewModel.DisplayText);
        }
    }
}
