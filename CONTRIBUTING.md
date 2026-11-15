# Contributing to X-Native

Thanks for helping. X-Native is an early-stage, open research project: the
goal is to catalogue, analyse, and — eventually, through credible
compatibility techniques — run PC games on Xbox consoles in Developer Mode.
Today the launcher, cataloguing, and analysis work; no game runs yet. See
[docs/compatibility.md](docs/compatibility.md) for the honest picture.
Contributions that keep that picture accurate are as welcome as code.

## Prerequisites

For the managed libraries and tests (most contributions):

- .NET SDK 8.0.x. The repo pins SDK `8.0.100` with `latestFeature`
  roll-forward in [global.json](global.json), so any 8.0.x SDK from that
  feature band upward works.

Only for the Xbox host (`src/XNative.XboxHost`):

- Windows with Visual Studio 2022 and the **Universal Windows Platform
  development** workload.
- An Xbox console in Developer Mode, for deploying and testing on hardware.

You do **not** need a console to contribute to the core libraries, the
catalog, the PE inspector, profiles, tests, or documentation.

## Build and test

```bash
dotnet restore X-Native.sln
dotnet build X-Native.sln -c Release
dotnet test X-Native.sln -c Release
```

The solution references all eight test projects, so the `dotnet test`
step above runs the whole suite. (Older checkouts referenced only four —
on those, run every project explicitly:
`for proj in tests/*/*.csproj; do dotnet test "$proj" -c Release; done`.)

The `TEST_PROJECTS` list at the top of
[.github/workflows/ci.yml](.github/workflows/ci.yml) is the source of truth
for the full set; [docs/building.md](docs/building.md) explains why.

`dotnet build` on the solution builds every managed project and skips
`XNative.XboxHost`: the .NET SDK cannot evaluate a legacy UWP project, so the
host is mapped into the solution without a build entry under the `Any CPU`
configuration and is built with MSBuild / Visual Studio instead. Full
instructions, including host deployment via the Device Portal, are in
[docs/building.md](docs/building.md).

CI runs on Windows and Linux for every push to `main` and every pull
request: it restores and builds the solution, then restores, builds, and
tests every test project explicitly from the `TEST_PROJECTS` list, and a
guard job fails if a test project exists that is covered by neither the
solution nor that list.

For a quick smoke check without a console, the `XNative.App` console head
has a diagnostic CLI (`info` / `scan` / `inspect` / `launch`) and the repo
ships sample games and profiles under [examples/](examples/) — see
"Smoke check: the console head" in [docs/building.md](docs/building.md).

## Architecture rules

These are not style preferences; a unit test enforces the first one. The full
document is [docs/architecture.md](docs/architecture.md).

1. **`XNative.Core` has no dependencies.** It targets `netstandard2.0` and
   references nothing. Interfaces are declared in the core.
2. **Modules never reference each other.** `Platform`, `Graphics`, `Wine`,
   `Plugins`, and `UI` each reference only the core.
3. **Composition roots wire everything.** `XNative.App` (development head) and
   the Xbox host's `Bootstrapper` are the only places that know about every
   module. Adding a module means adding a project, an `AddXNativeX()`
   extension method, and one line in each composition root.
4. Implementations live in the modules; Windows Runtime code lives only in
   `XNative.XboxHost`.

## Code style

- Warnings are errors: `Directory.Build.props` sets
  `TreatWarningsAsErrors` and enables the latest .NET analyzers. A change is
  not done while the build warns.
- Public members carry XML documentation comments. (`CS1591` is suppressed
  repo-wide, but documentation is the project's convention — follow the
  surrounding code.)
- Formatting follows [.editorconfig](.editorconfig): 4-space indentation,
  block-scoped namespaces, Allman braces. Style rules there are deliberately
  kept at suggestion severity; formatting should never be the reason a build
  fails.
- `LangVersion` is `latest` and nullable reference types are enabled. Write
  nullability honestly rather than suppressing it.

## Branch and pull request workflow

1. Branch from `main` with a descriptive name, e.g. `fix/catalog-depth` or
   `feat/pe-inspector`.
2. Keep the change scoped. Unrelated cleanups go in their own PR.
3. Make sure `dotnet build` and the full test suite (every project under
   `tests/`, not just the solution — see "Build and test" above) pass
   locally.
4. Open a pull request and complete the PR template, including the testing
   and honesty checklists.
5. If the change touches the Xbox host, say whether it was deployed to a
   console and what the startup screen reported.

## Good first contributions

Real, bounded work that does not need a console:

- **PE test vectors** — small, hand-built or freely redistributable PE files
  (and their expected parse results) that exercise the PE inspector's edge
  cases: unusual section layouts, missing optional headers, malformed files
  that must be rejected cleanly.
- **Game profiles** — compatibility profiles for real games: executable name,
  known behaviour, recommended arguments, honest rating. Use the game
  compatibility issue template to report results even before writing code.
- **Documentation** — clarify a doc, document a `game.json` manifest edge
  case (see [docs/game-manifest.md](docs/game-manifest.md)), or record a
  Dev Mode platform finding in [docs/compatibility.md](docs/compatibility.md).
- **Catalogue edge cases** — tests for `LocalGameCatalog`: manifests with
  missing fields, nested folders, duplicate executables.

## Honesty rule

Never document, comment, or describe behaviour as working unless it is
implemented and verified. X-Native cannot run PC games yet, and the docs say
so. A contribution that overstates what works will be asked to correct the
claim before it is merged — this protects the project's credibility, which is
its main asset at this stage.

## Conduct and security

Participation is governed by the [Code of Conduct](CODE_OF_CONDUCT.md).
Security issues are reported privately as described in
[SECURITY.md](SECURITY.md) — please do not open public issues for them.
