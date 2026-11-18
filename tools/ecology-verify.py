"""Render actual planned encounters and record the scripted gameplay observation.
Run under xvfb-run or an existing X11 display; four fixed ticks per rendered frame.
This is behavior evidence, not physical-device performance validation.
"""
from pathlib import Path
import subprocess,os,shutil
root=Path(__file__).resolve().parents[1]
out=root/'build/ecology'
out.mkdir(parents=True,exist_ok=True)
movie=None
if shutil.which('ffmpeg'):
    movie=subprocess.Popen(['ffmpeg','-hide_banner','-loglevel','error','-y','-f','x11grab','-framerate','15','-video_size','2340x1080','-i',os.environ['DISPLAY'],'-c:v','libx264','-preset','ultrafast','-crf','28','-pix_fmt','yuv420p',str(out/'encounters.mp4')],stdin=subprocess.PIPE)
try:
    result=subprocess.run([os.environ.get('GODOT','godot'),'--path',str(root),'--audio-driver','Dummy','--','--visual-check','--mobile-ui','--ecology-only'],cwd=root,stdout=open(out/'rendered.log','w'),stderr=subprocess.STDOUT,timeout=600)
    log=(out/'rendered.log').read_text()
    if result.returncode or 'REACH ECOLOGY RENDERED CHECKS PASSED' not in log or 'ERROR:' in log:
        raise RuntimeError(log[-5000:])
    print('REACH ECOLOGY RENDERED CHECKS PASSED',flush=True)
finally:
    if movie:
        try:movie.communicate(b'q',timeout=20)
        except subprocess.TimeoutExpired:movie.kill();movie.wait()
        if movie.returncode:raise RuntimeError('Encounter recording failed')
