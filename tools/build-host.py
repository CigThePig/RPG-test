"""Fallback for restricted Linux hosts whose /proc breaks dotnet CLI/MSBuild process inspection.
Normal development and CI should use `dotnet build`. Requires one successful SDK restore.
"""
from pathlib import Path
import json, os, subprocess
root = Path(__file__).resolve().parents[1]
sdk_root = Path(os.environ["DOTNET_ROOT"])
godot_root = Path(os.environ["GODOT_MONO_ROOT"])
sdk = sorted((sdk_root / "sdk").glob("8.0.*"))[-1]
refs = sorted((sdk_root / "packs/Microsoft.NETCore.App.Ref").glob("8.0.*"))[-1] / "ref/net8.0"
api = godot_root / "GodotSharp/Api/Debug"
out = root / ".godot/mono/temp/bin/Debug"
out.mkdir(parents=True, exist_ok=True)
config = root / ".godot/mono/temp/obj/Debug/host.editorconfig"
config.parent.mkdir(parents=True, exist_ok=True)
config.write_text(f"is_global = true\nbuild_property.GodotProjectDir = {root}/\nbuild_property.RootNamespace = Mosslight\n")
sources = []
for location in ["game", "vendor/ppc/Core", "third_party/spell", "third_party/graph_dungeon/src", "addons/procedural_creatures", "addons/procedural_creatures_plantfolk", "addons/dialogue_manager"]:
    sources.extend(sorted((root / location).rglob("*.cs")))
args = ["-nologo", "-target:library", "-langversion:12", "-nullable:enable", "-optimize+", f"-out:{out / 'RpgTest.dll'}", f"-analyzerconfig:{config}"]
args += [f"-r:{p}" for p in refs.glob("*.dll")]
args += [f"-r:{api / name}" for name in ["GodotSharp.dll", "GodotSharpEditor.dll"]]
generators = Path.home() / ".nuget/packages/godot.sourcegenerators/4.6.1/analyzers/dotnet/cs"
args += [f"-analyzer:{p}" for p in generators.glob("*.dll")]
if not list(generators.glob("*.dll")):
    raise SystemExit("Run dotnet restore once to acquire the pinned Godot source generator")
args += list(map(str, sources))
rsp = config.parent / "host-sources.rsp"
rsp.write_text("\n".join('"' + arg + '"' for arg in args))
subprocess.run([str(sdk_root / "dotnet"), str(sdk / "Roslyn/bincore/csc.dll"), "@" + str(rsp)], cwd=root, check=True)
(out / "RpgTest.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": "8.0.0"}}}))
