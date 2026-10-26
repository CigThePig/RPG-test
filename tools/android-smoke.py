"""Hosted x86_64 Android runtime smoke; ARM64 phone performance remains separate."""
from pathlib import Path
import os, subprocess, sys, time
out=Path('build/android-runtime');out.mkdir(parents=True,exist_ok=True)
package='com.cigthepig.mosslight.dev'
def run(*args, **kwargs):
    # Never let a disconnected ADB daemon strand the workflow indefinitely.
    kwargs.setdefault('timeout', 20)
    return subprocess.check_output(args, stderr=subprocess.STDOUT, **kwargs)
def adb(*args): return run(os.path.join(os.environ['ANDROID_HOME'], 'platform-tools', 'adb'), *args, text=True).strip()
log=(out/'emulator.log').open('w')
emulator=subprocess.Popen([os.environ['ANDROID_HOME']+'/emulator/emulator','-avd','mosslight',
    '-no-window','-no-audio','-no-boot-anim','-no-snapshot','-gpu','swiftshader_indirect','-skin','1280x800'],stdout=log,stderr=log)
try:
    deadline=time.monotonic()+240
    while time.monotonic()<deadline:
        if emulator.poll() is not None: raise RuntimeError('Emulator exited before boot')
        try:
            if adb('shell','getprop','sys.boot_completed')=='1': break
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
            (out/'boot-probe.log').write_text(str(error))
        time.sleep(2)
    else: raise RuntimeError('Emulator boot timed out')
    adb('shell','input','keyevent','82')
    adb('shell','settings','put','system','accelerometer_rotation','0')
    adb('shell','settings','put','system','user_rotation','1')
    install=adb('install','-r',sys.argv[1]);assert 'Success' in install,install
    (out/'install.log').write_text(install)
    adb('logcat','-c')
    adb('shell','monkey','-p',package,'-c','android.intent.category.LAUNCHER','1')
    deadline=time.monotonic()+90
    while time.monotonic()<deadline:
        logs=adb('logcat','-d')
        if 'MOSSLIGHT_READY' in logs and 'MOSSLIGHT_DIALOGUE_READY' in logs: break
        if 'FATAL EXCEPTION' in logs or 'Fatal signal' in logs: raise RuntimeError('Android crash during launch')
        time.sleep(2)
    else: raise RuntimeError('C# scene or imported dialogue readiness marker never arrived')
    # Landscape UI coordinate scaled from the logical 1280x800 base.
    bounds=adb('shell','wm','size').split()[-1].split('x');w,h=sorted(map(int,bounds),reverse=True)
    def tap(x,y): adb('shell','input','tap',str(int(x*w/1280)),str(int(y*h/800)))
    # Preserve a picture of the actual exported start menu, not just a readiness marker.
    adb_path = os.path.join(os.environ['ANDROID_HOME'], 'platform-tools', 'adb')
    (out/'start-menu.png').write_bytes(run(adb_path, 'exec-out', 'screencap', '-p'))
    tap(640,150) # First menu button: Enter / Resume.
    time.sleep(8)
    adb('shell','input','swipe',str(int(140*w/1280)),str(int(640*h/800)),str(int(220*w/1280)),str(int(640*h/800)),'1500')
    time.sleep(3)
    (out/'exploration.png').write_bytes(run(adb_path, 'exec-out', 'screencap', '-p'))
    adb('shell','input','keyevent','3') # Background triggers save and cancels held input.
    time.sleep(2)
    adb('shell','monkey','-p',package,'-c','android.intent.category.LAUNCHER','1')
    time.sleep(3)
    assert adb('shell','pidof',package),'Application not running after resume'
    logs=adb('logcat','-d');(out/'logcat.log').write_text(logs)
    assert 'MOSSLIGHT_RUNTIME' in logs,'Gameplay did not progress after touchscreen Resume'
    for error in ['FATAL EXCEPTION','Fatal signal','SCRIPT ERROR:','System.DllNotFoundException','Unhandled exception']:
        assert error not in logs, f'Android runtime error: {error}'
    (out/'result.txt').write_text('PASS: x86_64 Android emulator install, C# scene launch, touch Resume, gameplay ticks, screenshot, background/resume. ARM64 device not tested.\n')
    print((out/'result.txt').read_text())
finally:
    try: (out/'logcat.log').write_text(adb('logcat','-d'))
    except Exception: pass
    if emulator.poll() is None:
        emulator.terminate()
        try: emulator.wait(timeout=10)
        except subprocess.TimeoutExpired: emulator.kill()
    log.close()
