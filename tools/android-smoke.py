"""Hosted x86_64 Android runtime smoke; ARM64 phone performance remains separate."""
from pathlib import Path
import os, subprocess, sys, time, re, json
import xml.etree.ElementTree as ET
from PIL import Image
from io import BytesIO
out=Path('build/android-runtime');out.mkdir(parents=True,exist_ok=True)
package='com.cigthepig.mosslight.dev'
recorder=None
recording_start=None
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
    def ensure_foreground(timeout=30):
        deadline=time.monotonic()+timeout
        while time.monotonic()<deadline:
            windows=adb('shell','dumpsys','window','windows')
            focus=next((line for line in windows.splitlines() if 'mCurrentFocus=' in line),'')
            if package in focus: return
            # Hosted Google APIs images can show an unrelated launcher ANR over
            # a healthy game. Inspect the actual native modal, never dismiss a
            # Mosslight ANR or accept scenery behind a blocking system dialog.
            adb('shell','uiautomator','dump','/sdcard/mosslight-window.xml')
            xml=adb('shell','cat','/sdcard/mosslight-window.xml')
            (out/'native-window.xml').write_text(xml)
            if "isn't responding" in xml or 'is not responding' in xml:
                texts=[n.get('text','') for n in ET.fromstring(xml).iter('node')]
                assert any('Pixel Launcher' in text and 'responding' in text for text in texts), 'Game or unexpected application ANR blocks input'
                nodes=ET.fromstring(xml).iter('node')
                close=next((n for n in nodes if n.get('resource-id')=='android:id/aerr_close' or n.get('text')=='Close app'),None)
                assert close is not None, 'Launcher ANR close control missing'
                box=list(map(int,re.findall(r'\d+',close.get('bounds',''))))
                assert len(box)==4
                adb('shell','input','tap',str((box[0]+box[2])//2),str((box[1]+box[3])//2))
                continue
            got_it=next((n for n in ET.fromstring(xml).iter('node') if n.get('text')=='Got it'),None)
            if got_it is not None:
                box=list(map(int,re.findall(r'\d+',got_it.get('bounds',''))))
                if len(box)==4: adb('shell','input','tap',str((box[0]+box[2])//2),str((box[1]+box[3])//2))
            time.sleep(.5)
        raise RuntimeError('Mosslight never acquired foreground input focus')
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
    ensure_foreground()
    capture('start-menu.png',wait=45)
    tap(640,178) # Center of Enter / Resume; avoid its top edge.
    deadline=time.monotonic()+20
    while time.monotonic()<deadline:
        if 'MOSSLIGHT_SESSION_STARTED' in adb('logcat','-d'): break
        ensure_foreground(timeout=10)
        time.sleep(.5)
    else: raise RuntimeError('Resume tap never entered a gameplay session')
    # ADB screenrecord captures the actual Android surface, including frame pacing.
    recording_start=time.time()
    recorder=subprocess.Popen([adb_path,'shell','screenrecord','--bit-rate','4000000','--time-limit','90','/sdcard/mosslight-playtest.mp4'],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    # Exercise actual touch ownership, then open the starting Gloot chest.
    # Read release positions from production diagnostics instead of assuming emulator FPS.
    for attempt in range(12):
        adb('shell','input','swipe',str(int(140*w/1280)),str(int(640*h/800)),str(int(280*w/1280)),str(int(640*h/800)),'600')
        time.sleep(.4)
        ensure_foreground(timeout=10)
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
    interactions=re.findall(r'MOSSLIGHT_INTERACT feedback=([^\r\n]+)',logs)
    assert len(interactions)==1 and interactions[0].startswith('Chest emptied'),f'One physical tap dispatched {len(interactions)} interactions: {interactions}'
    capture('exploration.png')
    tap(580,715) # Pack: second row, first button.
    time.sleep(1)
    capture('inventory.png')
    pids=adb('shell','pidof','screenrecord').split()
    for pid in pids:
        if pid.isdigit(): adb('shell','kill','-2',pid)
    stdout,stderr=recorder.communicate(timeout=15)
    (out/'screenrecord.log').write_bytes(stdout+stderr)
    adb('pull','/sdcard/mosslight-playtest.mp4',str(out/'gameplay.mp4'))
    assert (out/'gameplay.mp4').stat().st_size>10000,'Empty Android recording'
    run('ffmpeg','-v','error','-i',str(out/'gameplay.mp4'),'-vf','fps=2,scale=640:400',str(out/'frame-%03d.png'))
    frames=sorted(out.glob('frame-*.png'));assert len(frames)>5,'Insufficient Android recording frames'
    from PIL import ImageChops, ImageStat, ImageDraw
    changed=sum(sum(ImageStat.Stat(ImageChops.difference(Image.open(a).convert('RGB').crop((100,70,540,280)),Image.open(b).convert('RGB').crop((100,70,540,280)))).mean)>1 for a,b in zip(frames,frames[1:]))
    assert changed>2,'Frozen Android gameplay video'
    sheet=Image.new('RGB',(1280,880));draw=ImageDraw.Draw(sheet)
    for i in range(16):
        f=frames[round(i*(len(frames)-1)/15)];im=Image.open(f);im.thumbnail((320,200));x=i%4*320;y=i//4*220;sheet.paste(im,(x,y));draw.text((x+4,y+200),f'Android t={(int(f.stem.split("-")[-1])-1)/2:.1f}s',fill='white')
    sheet.save(out/'contact-sheet.jpg')
    for f in frames:f.unlink()
    (out/'recording.json').write_text(json.dumps({'captureStartUnix':recording_start,'source':run('git','rev-parse','HEAD',text=True).strip(),'platform':'hosted x86_64 Android emulator','input':'ADB OS touch events','positions':positions,'fps':'screenrecord variable; inspect video PTS','synchronization':'captureStartUnix anchors screenrecord; logcat timestamps and MOSSLIGHT_TOUCH_RELEASE provide coarse input/displacement correlation, not sub-frame latency'},indent=2))
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
    if recorder is not None and recorder.poll() is None:
        recorder.terminate()
        try: recorder.wait(timeout=10)
        except subprocess.TimeoutExpired: recorder.kill()
    try: (out/'logcat.log').write_text(adb('logcat','-d'))
    except Exception: pass
    if emulator.poll() is None:
        emulator.terminate()
        try: emulator.wait(timeout=10)
        except subprocess.TimeoutExpired: emulator.kill()
    log.close()
