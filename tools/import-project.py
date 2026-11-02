"""Fresh clones must import textures before plugins preload their SVGs."""
from pathlib import Path
import os, signal, subprocess, sys
root = Path(__file__).resolve().parents[1]
project = root / "project.godot"
text = project.read_text()
# CI cancellation and timeouts send SIGTERM; unwind so `finally` restores the plugins.
signal.signal(signal.SIGTERM, lambda *_: sys.exit(1))
try:
    lines = text.splitlines()
    lines = ['enabled=PackedStringArray()' if line.startswith('enabled=PackedStringArray(') else line for line in lines]
    project.write_text('\n'.join(lines) + '\n')
    subprocess.run([os.environ.get("GODOT", "godot"), "--headless", "--editor", "--path", str(root), "--import"], check=True)
finally:
    project.write_text(text)
# Import again with editor integrations enabled and stable path-based icon references.
subprocess.run([os.environ.get("GODOT", "godot"), "--headless", "--editor", "--path", str(root), "--import"], check=True)
