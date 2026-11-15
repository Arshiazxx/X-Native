# The `game.json` manifest format

A folder that contains a `game.json` file describes one game to X-Native:
its display name, which executable belongs to it, and its metadata. Manifests
are how a game gets a proper name and publisher in the launcher instead of a
bare executable file name.

The format is implemented today by `LocalGameCatalog` in
`src/XNative.Platform/Games/LocalGameCatalog.cs`. Property names are matched
case-insensitively. Unknown properties are ignored.

## Fields

### v1 — implemented today

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `name` | string | No | Display name in the launcher. Defaults to the executable's file name without its extension. |
| `executable` | string | **Yes** | Executable file name, relative to the manifest folder. A manifest without an `executable`, or whose executable does not exist on disk, is skipped. |
| `workingDirectory` | string | No | Working directory for the game, relative to the manifest folder. Defaults to the manifest folder itself. |
| `description` | string | No | Free-form description of the game. |
| `publisher` | string | No | Publisher name. |
| `version` | string | No | The game's version string, as free text (not parsed). |

### v2 — being added

These fields are part of the format's next revision. They are documented here
so manifests written now keep working, but the current catalog does not read
them yet.

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `arguments` | string | No | Command-line arguments to pass when the game is launched. |
| `icon` | string | No | Path to an icon image, relative to the manifest folder, shown in the launcher. |

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

(`arguments` and `icon` are the v2 fields described above.)

## Path rules

- Every path in a manifest (`executable`, `workingDirectory`, and the v2
  `icon`) is **relative to the folder containing the `game.json` file**.
- Paths must stay inside that folder. A manifest is data read from disk, and
  X-Native treats it as untrusted: an entry that resolves outside its own
  folder is not a valid way to point at a game. See
  [SECURITY.md](../SECURITY.md) — path-handling reports are in scope.
- `executable` is a file name (subfolders below the manifest folder are
  fine, e.g. `"bin/Game.exe"`); the file must exist or the manifest is
  skipped silently and the folder falls back to a bare executable scan.

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
