"""Run the real Godot application and fail on engine/script errors as well as test failures."""
from pathlib import Path
import os, subprocess, sys
root = Path(__file__).resolve().parents[1]
visual = "--visual" in sys.argv
command = [os.environ.get("GODOT", "godot"), "--path", str(root), "--audio-driver", "Dummy"]
if not visual:
    command.append("--headless")
command += ["--", "--visual-check" if visual else "--test"]
if "--mobile" in sys.argv: command.append("--mobile-ui")
try:
    result = subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=240)
    text, returncode = result.stdout + result.stderr, result.returncode
except subprocess.TimeoutExpired as timeout:
    # Keep the partial engine output: a hang is exactly when the log matters most.
    def decode(stream): return stream.decode(errors="replace") if isinstance(stream, bytes) else stream or ""
    text, returncode = decode(timeout.stdout) + decode(timeout.stderr) + "\nVERIFY TIMEOUT after 240 s\n", 1
(root / "build").mkdir(exist_ok=True)
(root / "build" / ("verification-mobile.log" if "--mobile" in sys.argv else "verification-visual.log" if visual else "verification-integration.log")).write_text(text)
print(text)
marker = "VISUAL CHECKS PASSED" if visual else "FOUNDATION CHECKS PASSED"
if returncode or marker not in text or "SCRIPT ERROR:" in text or "ERROR:" in text:
    raise SystemExit(1)
