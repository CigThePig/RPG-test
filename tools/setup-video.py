"""Bounded CI-only video setup; avoid unrelated distro repository update stalls.
The imageio-ffmpeg wheel carries FFmpeg with H.264 and X11 capture on Linux.
Nothing from this tool is included in the Android application.
"""
from pathlib import Path
import importlib, os, shutil, subprocess, sys

dependencies=Path(os.environ.get('RUNNER_TEMP', '/tmp')) / 'mosslight-video-deps'
dependencies.mkdir(parents=True,exist_ok=True)
subprocess.run([sys.executable, '-m', 'pip', 'install', '--disable-pip-version-check',
                '--upgrade', '--target', str(dependencies),
                '--timeout', '30', '--retries', '1', 'Pillow==11.3.0',
                'imageio-ffmpeg==0.6.0'], check=True, timeout=180)
sys.path.insert(0,str(dependencies))
importlib.invalidate_caches()
import imageio_ffmpeg
binary = Path(imageio_ffmpeg.get_ffmpeg_exe())
for option, required in [('-encoders', 'libx264'), ('-devices', 'x11grab')]:
    result = subprocess.run([str(binary), '-hide_banner', option], check=True,
                            capture_output=True, text=True, timeout=15)
    if required not in result.stdout + result.stderr:
        raise RuntimeError(f'Pinned FFmpeg is missing {required}')
if '--require-display' in sys.argv and shutil.which('xvfb-run') is None:
    raise RuntimeError('The Ubuntu runner must provide xvfb-run (also required by native CI)')
folder = Path(os.environ.get('RUNNER_TEMP', '/tmp')) / 'mosslight-video-bin'
folder.mkdir(parents=True, exist_ok=True)
link = folder / 'ffmpeg'
if link.is_symlink(): link.unlink()
if not link.exists(): link.symlink_to(binary)
if 'GITHUB_ENV' in os.environ:
    inherited=os.environ.get('PYTHONPATH','')
    with open(os.environ['GITHUB_ENV'],'a') as stream:
        stream.write('PYTHONPATH='+str(dependencies)+(os.pathsep+inherited if inherited else '')+'\n')
if 'GITHUB_PATH' in os.environ:
    with open(os.environ['GITHUB_PATH'], 'a') as stream: stream.write(str(folder) + '\n')
version = subprocess.check_output([str(binary), '-version'], text=True, timeout=15).splitlines()[0]
print(f'Video tools ready: {version}; H.264 and X11 capture verified; PATH={folder}')
