# The `game.json` manifest format

A folder that contains a `game.json` file describes one game to X-Native:
its display name, which executable belongs to it, and its metadata. Manifests
are how a game gets a proper name and publisher in the launcher instead of a
bare executable file name.

The format is implemented today by `LocalGameCatalog` in
`src/XNative.Platform/Games/LocalGameCatalog.cs`. Property names are matched
case-insensitively. Unknown properties are ignored.

## Fields

All of these fields are implemented today — v1 and v2 alike are read by the
current catalog.

### v1

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `name` | string | No | Display name in the launcher. Defaults to the executable's file name without its extension. |
| `executable` | string | **Yes** | Executable file name, relative to the manifest folder. A manifest without an `executable`, or whose executable does not exist on disk, is skipped. |
| `workingDirectory` | string | No | Working directory for the game, relative to the manifest folder. Defaults to the manifest folder itself. |
| `description` | string | No | Free-form description of the game. |
| `publisher` | string | No | Publisher name. |
| `version` | string | No | The game's version string, as free text (not parsed). |

### v2

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `arguments` | string | No | Command-line arguments to pass when the game is launched. Recorded on the game as `GameInfo.LaunchArguments`; absent or whitespace-only means `null` (no arguments). |
| `icon` | string | No | Path to an icon image, relative to the manifest folder, shown in the launcher. Recorded on the game as `GameMetadata.IconPath` **only if** the resolved path stays inside the manifest folder tree **and** the file exists. A missing or escaping icon is ignored — it never invalidates the game. |

## Example

```json
{
  "name": "Example Game",
  "executable": "ExampleGame.exe",
  "workingDirectory": ".",
  "description": "A short example game used to document the manifest format.",
  "publisher": "Example Studio",
  "version": "1.0.0",
  "arguments": "-windowed",
  "icon": "icon.png"
}
```

## Path rules

- Every path in a manifest (`executable`, `workingDirectory`, and `icon`)
  is **relative to the folder containing the `game.json` file**.
- Paths must stay inside that folder. A manifest is data read from disk, and
  X-Native treats it as untrusted: paths are resolved to full paths and
  checked against the manifest folder before use. See
  [SECURITY.md](../SECURITY.md) — path-handling reports are in scope.
- An `executable` or `workingDirectory` that escapes the manifest folder
  (for example `"../OtherGame/game.exe"`) **rejects the manifest**: no game
  is created from it, and the folder falls back to a bare executable scan.
- An `icon` that escapes the folder is treated more gently: the icon is
  simply ignored (and a missing icon file is ignored the same way). Only
  the executable and working directory can reject a manifest.
- `executable` is a file name (subfolders below the manifest folder are
  fine, e.g. `"bin/Game.exe"`); the file must exist or the manifest is
  skipped silently and the folder falls back to a bare executable scan.

## Game IDs

Every catalogued game gets an ID of the form `<slug>-<hash>`, where the slug
is the game's lowercased name (spaces become dashes) and the hash is the
FNV-1a 64-bit hash of the executable's normalised full path, rendered as 16
lowercase hexadecimal characters.

IDs are **stable**: the same executable at the same path gets the same ID on
every scan, in every process, across restarts — rescanning never changes a
game's identity. The flip side of deriving the ID from the path: **moving or
renaming a game's executable changes its ID**, because the game is then, as
far as the catalog is concerned, at a different location.

## Discovery rules

How `LocalGameCatalog` finds games, in order:

1. The catalog scans the configured root folder recursively, down to a
   maximum depth of 3 levels below the root by default (`MaxDepth` in
   `LocalGameCatalogOptions`).
2. In each folder, it looks for `game.json` first. If a manifest is present
   **and valid** (parses as JSON and names an executable that exists), the
   game is catalogued from the manifest — and the bare executable scan is
   skipped for that folder. The manifest wins.
3. If there is no manifest, or the manifest is invalid or names a missing
   executable, the folder is scanned for executables matching the configured
   patterns (`*.exe` by default). Each executable becomes a game entry named
   after its file name, with no metadata.
4. Folders the process cannot read are skipped; a scan never fails because
   one folder was inaccessible.

A malformed `game.json` never breaks the scan — it is treated as "no
manifest" and rule 3 applies.
