"""Render actual planned encounters and record the scripted gameplay observation.
Run under xvfb-run or an existing X11 display; four fixed ticks per rendered frame.
This is behavior evidence, not physical-device performance validation.
"""
from pathlib import Path
import subprocess,os,shutil,time
root=Path(__file__).resolve().parents[1]
out=Path(os.environ.get('MOSSLIGHT_ECOLOGY_OUTPUT',str(root/'build/ecology')))
out.mkdir(parents=True,exist_ok=True)
movie=None
with open(out/'rendered.log','w') as log_file:
    game=subprocess.Popen([os.environ.get('GODOT','godot'),'--path',str(root),'--audio-driver','Dummy','--','--visual-check','--mobile-ui','--ecology-only','--ecology-output='+str(out)],cwd=root,stdout=log_file,stderr=subprocess.STDOUT)
    try:
        # The engine has connected to X11 and created its window by READY. In
        # restricted hosts the server can take longer to accept its first client.
        deadline=time.monotonic()+60
        while game.poll() is None and 'MOSSLIGHT_READY' not in (out/'rendered.log').read_text():
            if time.monotonic()>deadline:raise RuntimeError('Encounter game did not initialize')
            time.sleep(.1)
        if game.poll() is None and shutil.which('ffmpeg'):
            movie=subprocess.Popen(['ffmpeg','-hide_banner','-loglevel','error','-y','-f','x11grab','-framerate','15','-video_size','2340x1080','-i',os.environ['DISPLAY'],'-c:v','libx264','-preset','ultrafast','-crf','28','-pix_fmt','yuv420p',str(out/'encounters.mp4')],stdin=subprocess.PIPE)
        game.wait(timeout=600)
        log=(out/'rendered.log').read_text()
        if game.returncode or 'REACH ECOLOGY RENDERED CHECKS PASSED' not in log or 'ERROR:' in log:
            raise RuntimeError(log[-5000:])
        print('REACH ECOLOGY RENDERED CHECKS PASSED',flush=True)
    finally:
        if game.poll() is None:game.kill();game.wait()
        if movie:
            try:movie.communicate(b'q',timeout=20)
            except subprocess.TimeoutExpired:movie.kill();movie.wait()
            if movie.returncode:raise RuntimeError('Encounter recording failed')
