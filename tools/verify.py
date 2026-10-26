"""Run the real Godot application and fail on engine/script errors as well as test failures."""
from pathlib import Path
import os, subprocess, sys
root = Path(__file__).resolve().parents[1]
visual = "--visual" in sys.argv
command = [os.environ.get("GODOT", "godot"), "--path", str(root), "--audio-driver", "Dummy"]
if not visual:
    command.append("--headless")
command += ["--", "--visual-check" if visual else "--test"]
result = subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=240)
text = result.stdout + result.stderr
(root / "build").mkdir(exist_ok=True)
(root / "build" / ("verification-visual.log" if visual else "verification-integration.log")).write_text(text)
print(text)
marker = "VISUAL CHECKS PASSED" if visual else "FOUNDATION CHECKS PASSED"
if result.returncode or marker not in text or "SCRIPT ERROR:" in text or "ERROR:" in text:
    raise SystemExit(1)
