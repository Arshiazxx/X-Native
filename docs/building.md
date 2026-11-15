# Building X-Native

There are two builds: the managed solution (libraries, the development head,
and the tests), which builds anywhere with the .NET SDK, and the Xbox host,
which needs Windows, Visual Studio, and MSBuild. Most contributors only need
the first.

## The managed solution

Prerequisites: .NET SDK 8.0.x. [global.json](../global.json) pins `8.0.423`
with `latestPatch` roll-forward, so any current 8.0.x SDK satisfies it.

```bash
git clone https://github.com/Arshiazxx/X-Native.git
cd X-Native
dotnet restore X-Native.sln
dotnet build X-Native.sln -c Release
dotnet test X-Native.sln -c Release
```

Warnings are errors in this repo (`TreatWarningsAsErrors` in
`Directory.Build.props`, analyzers at `latest`), so a clean build means a
warning-free build.

Why doesn't this build the Xbox host? `XNative.XboxHost` is a legacy UWP
project that the .NET SDK cannot evaluate. In `X-Native.sln` it is mapped to
the `x64` platform under the `Any CPU` solution configuration but has no
build entry there, so `dotnet build` and `dotnet test` skip it and succeed
on the whole solution. CI does exactly the restore / build / test sequence
above, on Windows and Linux.

## The Xbox host (UWP)

Prerequisites:

- Windows with Visual Studio 2022 and the **Universal Windows Platform
  development** workload.
- An Xbox console in Developer Mode (for deployment; the host also builds
  without one).

Build the package from a developer command prompt:

```bat
msbuild src\XNative.XboxHost\XNative.XboxHost.csproj /p:Configuration=Debug /p:Platform=x64
```

Deploy through the Device Portal:

1. Put the console into Developer Mode and note its IP address.
2. Open the Device Portal at `https://<console-ip>:11443`.
3. Under **Home > Add**, upload the `.msix` from
   `src\XNative.XboxHost\AppPackages\`, together with the dependency `.appx`
   files from the `Dependencies\x64` folder and the `.cer` file.
4. Start **X-Native** from the Dev Home.

Or, from Visual Studio: select `Debug | x64`, choose **Remote Machine**,
enter the console address, and deploy.

On startup the host shows a module health report: every expected module is
listed as loaded or missing, so a wiring mistake is visible on the console
screen instead of failing silently.

Host configuration, target platform versions, signing, and the startup
sequence are documented in [xbox-host.md](xbox-host.md) — that page is the
source of truth for the host; this page only summarises the build steps.

## See also

- [architecture.md](architecture.md) — project layout and dependency rules.
- [game-manifest.md](game-manifest.md) — the `game.json` format the catalog
  reads.
- [compatibility.md](compatibility.md) — what the platform allows, and what
  X-Native can and cannot do today.
