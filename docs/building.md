# Building X-Native

There are two builds: the managed solution (libraries, the development head,
and the tests), which builds anywhere with the .NET SDK, and the Xbox host,
which needs Windows, Visual Studio, and MSBuild. Most contributors only need
the first.

## The managed solution

Prerequisites: .NET SDK 8.0.x. [global.json](../global.json) pins `8.0.100`
with `latestFeature` roll-forward, so any 8.0.x SDK from that feature band
upward satisfies it.

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

The solution references all eight test projects, so
`dotnet test X-Native.sln` runs the whole suite. (It previously referenced
only four — Core, Platform, UI, and Wine tests — and silently skipped the
rest; if you are working from an older checkout, test each project
explicitly with `for proj in tests/*/*.csproj; do dotnet test "$proj" -c Release; done`.)

The source of truth for which test projects exist is the `TEST_PROJECTS`
list at the top of [.github/workflows/ci.yml](../.github/workflows/ci.yml);
keep it in sync when adding a test project. CI restores and builds the
solution, then restores, builds, and tests every project in that list
explicitly, on Windows and Linux — and a separate guard job fails CI if a
test project ever exists that is in neither the solution nor the list.

Why doesn't this build the Xbox host? `XNative.XboxHost` is a legacy UWP
project that the .NET SDK cannot evaluate. In `X-Native.sln` it is mapped to
the `x64` platform under the `Any CPU` solution configuration but has no
build entry there, so `dotnet build` and `dotnet test` skip it and succeed
on the whole solution.

## Smoke check: the console head

`XNative.App` is a runnable .NET 8 console application — the development
composition root — with a small diagnostic CLI. It is the quickest way to
see the modules wired together without an Xbox:

```bash
dotnet run --project src/XNative.App -c Release -- info [path]
dotnet run --project src/XNative.App -c Release -- scan [path]
dotnet run --project src/XNative.App -c Release -- inspect <exe>
dotnet run --project src/XNative.App -c Release -- launch <id|path>
dotnet run --project src/XNative.App -c Release -- help
```

- `info` prints the resolved modules, the device description, the input
  status, and the games in the games root (`[path]` overrides the default
  root). With no command at all, `info` is what runs.
- `scan` lists the games a catalog scan finds, with architecture,
  executable type, and path.
- `inspect` prints one executable's PE header information (validity,
  architecture, DLL, managed, subsystem, section count) and exits non-zero
  for a missing or invalid file.
- `launch` sends one game — by catalog id or executable path — through the
  game runner and prints the structured result. No compatibility backend
  can run a PC game yet, so a failure result with its reason is the honest
  outcome today; the exit code is non-zero when the launch fails.

Exit codes are `0` for success, `1` for failure, and `2` for a usage
error.

The repo ships a sample pack for trying this out: [examples/](../examples)
contains three example game folders under `examples/games`
(`MinimalGame`, `FullFeaturedGame`, `BareGame`) and a sample profile
database at `examples/profiles/profiles.json`. Point `scan` at
`examples/games` to see the three sample games catalogued;
[examples/README.md](../examples/README.md) explains what each example
demonstrates.

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
