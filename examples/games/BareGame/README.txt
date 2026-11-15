BareGame — the no-manifest example.

There is no game.json in this folder on purpose. When the catalog finds
no (valid) manifest in a folder, it scans the folder for executables
matching its patterns (*.exe by default) and catalogues each one under
its file name, with no description, publisher, version or arguments.

See examples/README.md for the full walkthrough.
