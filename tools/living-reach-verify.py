"""Capture comparable production streaming traces and a real-time desktop movie.
Run inside xvfb-run (CI) or an existing X11 display. No synthetic frames or FPS claims.
"""
from pathlib import Path
import os, subprocess, shutil, json
root=Path(__file__).resolve().parents[1]
out=root/'build/living-reach'
out.mkdir(parents=True,exist_ok=True)
for tag in ['before','after','video']:
    movie=None
    if tag=='video' and shutil.which('ffmpeg'):
        movie=subprocess.Popen(['ffmpeg','-hide_banner','-loglevel','error','-y','-f','x11grab','-framerate','30','-video_size','2340x1080','-i',os.environ['DISPLAY'],'-c:v','libx264','-preset','ultrafast','-crf','28','-pix_fmt','yuv420p',str(out/'exploration.mp4')],stdin=subprocess.PIPE)
    try:
        args=[os.environ.get('GODOT','godot'),'--path',str(root),'--audio-driver','Dummy','--','--visual-check','--mobile-ui','--living-reach-only']
        if tag=='before':args.append('--stream-baseline')
        if tag=='video':args.append('--living-reach-video')
        result=subprocess.run(args,cwd=root,capture_output=True,text=True,timeout=600)
        log=result.stdout+result.stderr
        (out/(tag+'.log')).write_text(log)
        if result.returncode or 'LIVING REACH RENDERED CHECKS PASSED' not in log or 'ERROR:' in log:
            raise RuntimeError(log[-6000:])
        print(tag,json.loads((out/tag/'performance.json').read_text()),flush=True)
    finally:
        if movie:
            try:movie.communicate(b'q',timeout=15)
            except subprocess.TimeoutExpired:movie.kill();movie.wait()
            if movie.returncode:raise RuntimeError('Desktop recording failed')
