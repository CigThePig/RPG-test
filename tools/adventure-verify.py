"""Record the real production-API adventure with live Dialogue Manager choices.
Three simulation ticks per rendered frame accelerate playback; this is not OS-touch evidence.
"""
from pathlib import Path
import os, subprocess, json, shutil
root=Path(__file__).resolve().parents[1]
out=root/'build/adventure';out.mkdir(parents=True,exist_ok=True)
shutil.rmtree(out/'frames',ignore_errors=True) # Generated captures from earlier runs may have obsolete names.
godot=os.environ.get('GODOT','godot')
cmd=[godot,'--path',str(root),'--audio-driver','Dummy','--fixed-fps','60','--write-movie',str(out/'adventure.avi'),'--','--adventure-visual','--mobile-ui']
result=subprocess.run(cmd,cwd=root,capture_output=True,text=True,timeout=900)
logs=result.stdout+result.stderr;(out/'engine.log').write_text(logs)
if result.returncode or 'RENDERED ADVENTURE PASSED' not in logs or 'ERROR:' in logs:raise RuntimeError(logs[-5000:])
report=json.loads((out/'normal-health.json').read_text())
assert report['health']>0 and report['attacks']>0 and report['damage']>0
assert any(c['name']=='quest-completed-rewarded' for c in report['checkpoints'])
subprocess.run(['ffmpeg','-y','-i',str(out/'adventure.avi'),'-c:v','libx264','-crf','24','-pix_fmt','yuv420p',str(out/'adventure.mp4')],check=True,timeout=180,capture_output=True)
(out/'adventure.avi').unlink()
print('RENDERED ADVENTURE VIDEO VERIFIED: build/adventure/adventure.mp4')
