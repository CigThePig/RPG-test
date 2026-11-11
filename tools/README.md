# Development commands

Use Godot **4.6.1 .NET**, SDK **8.0.408**, and Python 3. Run from repository root:

```
dotnet build
python3 tools/import-project.py
python3 tools/verify.py --headless
python3 tools/verify.py --visual
```

Set `GODOT` to the Godot .NET executable if it is not named `godot`. A real display is required for visual checks; CI uses `xvfb-run`. Tests fail on script/engine errors as well as failed assertions. Measurements are generated during the integration test. They do not represent minimum hardware requirements.

For interface design review, `godot --path . -- --visual-check --mobile-ui --ui-gallery` renders every player screen (HUD, menu, Pack/Craft/Trade, Character, Journal, Map, Save & Load, Settings, dialogue and high contrast) into `build/ui-gallery/` in about thirty seconds and restores the world afterwards. Add `--gallery-compact` (900×560), `--gallery-all` (three window sizes), `--gallery-only=pack,map` to choose screens, or `--gallery-perf` to print per-screen frame times. Omit `--mobile-ui` for the desktop hotbar.

For enemy work, `godot --headless --path . -- --enemy-animation-sheets[=prowler,brute]` writes contact sheets of every posture, reaction, attack and death to `build/enemy-animation/` without a display, and `godot --path . -- --visual-check --enemy-preview` stages each behaviour beat in the combat lab and captures it to `build/enemy-preview/`. Neither asserts anything. `python3 tools/verify.py --headless --test-only=EnemyBehaviorChecks,EnemyAnimationChecks` runs only the named suites; add `--test-keep-going` to list every failure instead of stopping at the first. See [enemy behaviour and animation](../docs/ENEMY_BEHAVIOR.md).

For overworld art review, `godot --path . -- --visual-check --mobile-ui --overworld-preview` writes five production captures and the prop atlas to `build/preview/` in about twenty seconds; add `--preview-clean` to hide the interface. It asserts nothing.

`tools/build-host.py` is a compiler fallback for Linux hosts where process inspection prevents normal MSBuild. It needs `DOTNET_ROOT`, `GODOT_MONO_ROOT`, and restored Godot source-generator packages; standard builds do not need it.

`python3 tools/pack-dungeon-rules.py` packages the original text grammar into data readable from exported PCK files. Re-run it after changing the grammar.

`node tools/export-art.mjs [output-directory] [--all] [--family=biped] [--seed=42]` builds the original dependency-free exporter. Default output preserves the old reference catalog. Native gameplay generates arbitrary identities live; it does not depend on this finite catalog.
