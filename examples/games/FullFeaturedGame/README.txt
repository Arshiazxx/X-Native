FullFeaturedGame — the full v2 manifest example.

game.json uses every field the catalog reads: name, executable,
workingDirectory, description, publisher, version, plus the v2 fields
arguments and icon. The executable lives in the game subfolder (paths in a
manifest are relative to the manifest folder and may point into
subfolders, but never outside it), and the working directory is that
subfolder too.
The icon is recorded because icon.png sits inside this folder tree and
exists; a missing or escaping icon would simply be ignored.

See examples/README.md for the full walkthrough.
