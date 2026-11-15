# Examples

Worked examples of the two files X-Native reads from disk: the per-game
`game.json` manifest and the compatibility profile database. Every field
name and value here was checked against the code that reads it
(`LocalGameCatalog` in `src/XNative.Platform/Games` and
`JsonGameProfileStore` in `src/XNative.Wine/Profiles`), not just the
docs.

```text
examples/
  games/
    MinimalGame/        a minimal v1 manifest (one required field)
    FullFeaturedGame/   a full v2 manifest (every field the catalog reads)
    BareGame/           no manifest at all — bare executable scan
  profiles/
    profiles.json       a profile database loadable by JsonGameProfileStore
```

## Trying the game examples

Copy the three folders under `games/` into the folder your catalog scans
(its root path — for example the games folder you point X-Native at) and
refresh the catalog. Each folder's `.exe` is a plain-text placeholder,
not a real executable; replace it with the real game binary to use an
example for real. The placeholders are enough for the catalog: it checks
that the named executable exists, and a file that turns out not to be a
valid PE image is still catalogued — as a Windows executable with
`Unknown` architecture — instead of being dropped.

### MinimalGame — minimal v1 manifest

`games/MinimalGame/game.json` contains only the one required field:

```json
{ "executable": "MinimalGame.exe" }
```

The catalog creates the game with defaults everywhere else: the display
name comes from the executable file name (`MinimalGame`), the working
directory is the manifest folder, and there is no description, publisher,
version or launch arguments.

### FullFeaturedGame — full v2 manifest

`games/FullFeaturedGame/game.json` uses every field the catalog reads:
`name`, `executable`, `workingDirectory`, `description`, `publisher`,
`version`, and the v2 fields `arguments` and `icon`.

Things worth noticing:

- Paths are relative to the manifest folder and may point into
  subfolders — the executable is `game/FullFeaturedGame.exe` — but a path
  that escapes the folder (for example `../OtherGame/game.exe`) rejects
  the manifest and the folder falls back to a bare executable scan.
- `arguments` (`-windowed -skipintro`) is recorded on the game as its
  launch arguments.
- `icon` is recorded only because `icon.png` is inside the folder tree
  **and** exists. A missing or escaping icon is ignored; it never
  invalidates the game.
- Manifest property names are matched case-insensitively and unknown
  properties are ignored.

See [docs/game-manifest.md](../docs/game-manifest.md) for the full
format reference, including how game IDs are derived (an FNV-1a hash of
the executable's full path, so IDs are stable across rescans but change
if you move the game).

### BareGame — no manifest

`games/BareGame/` has no `game.json` at all. The scanner finds
`BareGame.exe` via its executable patterns (`*.exe` by default) and
catalogues it under its file name with no metadata: no description, no
publisher, no version, no launch arguments. A folder with a manifest
that is malformed, or whose named executable is missing, is treated the
same way — the manifest is skipped and the bare scan applies.

## Trying the profile examples

`profiles/profiles.json` is a compatibility database in the exact shape
`JsonGameProfileStore` loads: a JSON array of objects with the
properties `id`, `executableName` and `rating` (all required), plus
optional `notes` and `recommendedArguments`. Unlike manifest fields,
these property names are case-sensitive. `rating` is a
`CompatibilityRating` member name — `Unknown`, `Broken`, `Starts`,
`Playable` or `Perfect` — matched case-insensitively.

Profiles are matched to catalogued games by executable file name only
(case-insensitive, directories ignored), so `FullFeaturedGame.exe`
matches the Full Featured Game above wherever it is installed.

The store is strict on purpose: a malformed file, a missing required
property, or an unknown rating name throws `InvalidDataException` at
load time rather than silently loading as "no profiles".

The sample file carries one profile per example game:

| Profile | Game | Rating | Recommended arguments |
| --- | --- | --- | --- |
| `full-featured-game` | `FullFeaturedGame.exe` | `Playable` | `-windowed` |
| `bare-game` | `BareGame.exe` | `Starts` | `-novid -windowed` |
| `minimal-game` | `MinimalGame.exe` | `Broken` | `-safe-mode` |

## What the runner reports

Launching a game runs it through the compatibility launch pipeline,
which resolves the launch arguments first — per-launch options win, then
the manifest's `arguments`, then the profile's `recommendedArguments` —
and then reports a structured `LaunchResult`. Today every launch ends in
a failure result, and the profile rating decides which one:

- **`Broken`** — the launch is refused up front. The result fails with
  reason `KnownBroken` and a message like
  `Game 'MinimalGame' is known to be broken (profile 'minimal-game').`
  followed by the profile's notes. This is the one rating that changes
  the outcome today.
- **`Starts`, `Playable`, `Perfect` (and `Unknown`)** — the pipeline
  runs to its end and fails with reason `BackendNotImplemented` and the
  message `Windows executable detected. Compatibility backend not
  implemented. Profile rating: <Rating>.` The rating is reported so the
  UI can show what the database expects of the game, but it does not
  change the result.
- **No profile at all** — the same `BackendNotImplemented` failure,
  without the `Profile rating:` suffix.

Two other failures can occur before the rating is even consulted: a game
whose executable type is not a Windows executable fails with
`UnsupportedExecutableType`, and an executable that has disappeared from
disk fails with `ExecutableNotFound`.

## The honest part

No compatibility backend exists yet, and the Xbox UWP sandbox forbids
loading arbitrary executables or spawning processes — so **nothing in
these examples actually runs a game**, and no rating above should be
read as "this works today". The ratings are the compatibility database's
record of what is known or expected; the infrastructure around them —
cataloguing, profiles, argument resolution, structured failure — is real
and is what these examples exercise. Precise, honest failure is the
current product.
