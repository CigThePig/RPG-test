# Development commands

Use Godot **4.6.1 .NET**, SDK **8.0.408**, and Python 3. Run from repository root:

```
dotnet build
python3 tools/import-project.py
python3 tools/verify.py --headless
python3 tools/verify.py --visual
```

Set `GODOT` to the Godot .NET executable if it is not named `godot`. A real display is required for visual checks; CI uses `xvfb-run`. Tests fail on script/engine errors as well as failed assertions. Measurements are generated during the integration test. They do not represent minimum hardware requirements.

`tools/build-host.py` is a compiler fallback for Linux hosts where process inspection prevents normal MSBuild. It needs `DOTNET_ROOT`, `GODOT_MONO_ROOT`, and restored Godot source-generator packages; standard builds do not need it.

`python3 tools/pack-dungeon-rules.py` packages the original text grammar into data readable from exported PCK files. Re-run it after changing the grammar.

`node tools/export-art.mjs [output-directory] [--all] [--family=biped] [--seed=42]` builds the original dependency-free exporter. Default output preserves the old reference catalog. Native gameplay generates arbitrary identities live; it does not depend on this finite catalog.
