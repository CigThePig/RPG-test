"""Package the pinned upstream grammar for Godot's exported PCK filesystem."""
from pathlib import Path
import json
root = Path(__file__).resolve().parents[1]
source = root / "third_party/graph_dungeon"
paths = sorted((source / "grammar").rglob("*.txt"))
paths += [source / "graphStart.txt", source / "graphRecipe.txt"]
(root / "game/data/dungeon_rules.json").write_text(json.dumps({str(p.relative_to(source)): p.read_text() for p in paths}, indent=2) + "\n")
