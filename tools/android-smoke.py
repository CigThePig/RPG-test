"""Hosted x86_64 Android runtime smoke; ARM64 phone performance remains separate."""
from pathlib import Path
import os, subprocess, sys, time, re
from PIL import Image
from io import BytesIO
out=Path('build/android-runtime');out.mkdir(parents=True,exist_ok=True)
package='com.cigthepig.mosslight.dev'
def run(*args, **kwargs):
    # Never let a disconnected ADB daemon strand the workflow indefinitely.
    kwargs.setdefault('timeout', 20)
    return subprocess.check_output(args, stderr=subprocess.STDOUT, **kwargs)
def adb(*args): return run(os.path.join(os.environ['ANDROID_HOME'], 'platform-tools', 'adb'), *args, text=True).strip()
log=(out/'emulator.log').open('w')
emulator=subprocess.Popen([os.environ['ANDROID_HOME']+'/emulator/emulator','-avd','mosslight',
    '-no-window','-no-audio','-no-boot-anim','-no-snapshot','-gpu','swangle','-skin','1280x800'],stdout=log,stderr=log)
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
    # Fresh Android emulators display a system immersive-mode confirmation over
    # Godot on first launch. Dismiss it before taking evidence or tapping our menu.
    tap(880,190) # System 'Got it', outside the game UI.
    time.sleep(2)
    # Readiness is CPU-side; shader failure can leave a fully running game blank.
    adb_path = os.path.join(os.environ['ANDROID_HOME'], 'platform-tools', 'adb')
    def capture(name, wait=0):
        deadline=time.monotonic()+wait
        while True:
            data=run(adb_path, 'exec-out', 'screencap', '-p')
            (out/name).write_bytes(data)
            pixels=list(Image.open(BytesIO(data)).convert('RGB').resize((160,100)).getdata())
            colors=len(set(pixels))
            green=sum(g>r*1.04 and g>b*1.04 for r,g,b in pixels)
            # Starting Reach scenery must be present behind the HUD/menu/pack.
            # A Godot splash, solid gray frame or system screen is not gameplay evidence.
            if colors>64 and green>len(pixels)*.01:
                with (out/'image-validation.log').open('a') as f: f.write(f'{name}: colors={colors}, terrain_pixels={green}\n')
                return
            if time.monotonic()>=deadline: raise AssertionError(f'{name}: missing rendered world (colors={colors}, terrain_pixels={green})')
            time.sleep(1)
    capture('start-menu.png',wait=45)
    tap(640,150) # First menu button: Enter / Resume.
    time.sleep(8)
    # Exercise actual touch ownership, then open the starting Gloot chest.
    # Read release positions from production diagnostics instead of assuming emulator FPS.
    for attempt in range(12):
        adb('shell','input','swipe',str(int(140*w/1280)),str(int(640*h/800)),str(int(280*w/1280)),str(int(640*h/800)),'600')
        time.sleep(.4)
        logs=adb('logcat','-d')
        positions=re.findall(r'MOSSLIGHT_TOUCH_RELEASE x=([0-9.]+) y=([0-9.]+)',logs)
        if positions and float(positions[-1][0])>=265: break
    else: raise RuntimeError('Touch movement never approached starting chest')
    assert float(positions[-1][0])>220,'Touch input did not change player coordinates'
    capture('chest.png')
    tap(1145,635)
    time.sleep(1)
    logs=adb('logcat','-d')
    assert 'MOSSLIGHT_GAMEPLAY kind=resource_depleted value=chest' in logs,'Touch interaction did not empty authoritative chest inventory'
    capture('exploration.png')
    tap(580,715) # Pack: second row, first button.
    time.sleep(1)
    capture('inventory.png')
    adb('shell','input','keyevent','3') # Background triggers save and cancels held input.
    time.sleep(2)
    adb('shell','monkey','-p',package,'-c','android.intent.category.LAUNCHER','1')
    time.sleep(3)
    capture('resumed-menu.png',wait=15)
    assert adb('shell','pidof',package),'Application not running after resume'
    logs=adb('logcat','-d');(out/'logcat.log').write_text(logs)
    assert 'MOSSLIGHT_INPUT_CANCELLED' in logs,'Background did not cancel gameplay input'
    assert 'MOSSLIGHT_RUNTIME' in logs,'Gameplay did not progress after touchscreen Resume'
    for error in ['FATAL EXCEPTION','Fatal signal','SCRIPT ERROR:','System.DllNotFoundException','Unhandled exception','E godot   : ERROR:','Program linking failed','unable to bind shader']:
        assert error not in logs, f'Android runtime error: {error}'
    (out/'result.txt').write_text('PASS: x86_64 Android emulator install, C# scene launch, touch Resume, nonblank rendered world/menu/pack, measured player movement, Gloot chest depletion, inventory screenshot, gameplay ticks, background input cancellation/resume. ARM64 device not tested.\n')
    print((out/'result.txt').read_text())
finally:
    try: (out/'logcat.log').write_text(adb('logcat','-d'))
    except Exception: pass
    if emulator.poll() is None:
        emulator.terminate()
        try: emulator.wait(timeout=10)
        except subprocess.TimeoutExpired: emulator.kill()
    log.close()
