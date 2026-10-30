"""Hosted x86_64 Android runtime smoke; ARM64 phone performance remains separate."""
from pathlib import Path
import os, subprocess, sys, time, re, json
import xml.etree.ElementTree as ET
from android_window import anr_messages, has_game_focus
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
def adb(*args):
    # A hosted emulator can close a logcat dump mid-stream with exit 255 while
    # the game keeps running. Retry this read only; never repeat touch/install mutations.
    attempts=3 if args==('logcat','-d') else 1
    for attempt in range(attempts):
        try: return run(os.path.join(os.environ['ANDROID_HOME'], 'platform-tools', 'adb'), *args, text=True).strip()
        except subprocess.CalledProcessError as error:
            if attempts==1: raise
            (out/f'adb-logcat-read-{time.monotonic_ns()}.log').write_text(str(error)+"\n"+str(error.output))
            if attempt==attempts-1: raise
            time.sleep(.5)
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
    source=run('git','rev-parse','HEAD',text=True).strip()
    assert 'source='+source in logs, 'Exported build is missing its exact source revision'
    # Landscape UI coordinate scaled from the logical 1280x800 base.
    bounds=adb('shell','wm','size').split()[-1].split('x');w,h=sorted(map(int,bounds),reverse=True)
    def tap(x,y): adb('shell','input','tap',str(int(x*w/1280)),str(int(y*h/800)))
    def ensure_foreground(timeout=30):
        deadline=time.monotonic()+timeout
        while time.monotonic()<deadline:
            windows=adb('shell','dumpsys','window')
            (out/'focus-windows.txt').write_text(windows)
            # Hosted Google APIs images can show an unrelated launcher ANR over
            # a healthy game. Inspect the actual native modal, never dismiss a
            # Mosslight ANR or accept scenery behind a blocking system dialog.
            adb('shell','uiautomator','dump','/sdcard/mosslight-window.xml')
            xml=adb('shell','cat','/sdcard/mosslight-window.xml')
            (out/'native-window.xml').write_text(xml)
            messages=anr_messages(xml)
            if messages:
                assert all('Pixel Launcher' in text for text in messages), 'Game or unexpected application ANR blocks input'
                nodes=ET.fromstring(xml).iter('node')
                close=next((n for n in nodes if n.get('resource-id')=='android:id/aerr_close' or n.get('text')=='Close app'),None)
                assert close is not None, 'Launcher ANR close control missing'
                box=list(map(int,re.findall(r'\d+',close.get('bounds',''))))
                assert len(box)==4
                adb('shell','input','tap',str((box[0]+box[2])//2),str((box[1]+box[3])//2))
                continue
            if has_game_focus(xml,package): return
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
    recorder=subprocess.Popen([adb_path,'shell','screenrecord','--bit-rate','4000000','--time-limit','180','/sdcard/mosslight-playtest.mp4'],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    # Set a westward facing and leave the nearby enemy group alone while
    # checking repeated controls. The earlier chest-first ordering let active
    # enemies kill the player mid-test; it did not expose an input failure.
    adb('shell','input','swipe',str(int(140*w/1280)),str(int(640*h/800)),str(int(30*w/1280)),str(int(640*h/800)),'600')
    time.sleep(.4)
    def action_target(action):
        rows=re.findall(fr'MOSSLIGHT_CONTROL version=1 id={action} x=([0-9]+) y=([0-9]+)',adb('logcat','-d'))
        assert rows, f'Missing production layout diagnostic for {action}'
        x,y=map(int,rows[-1])
        assert 0<=x<w and 0<=y<h, f'Invalid control center: {action} {x},{y}'
        return x,y
    def tap_action(action):
        x,y=action_target(action)
        adb('shell','input','tap',str(x),str(y))
    (out/'control-layout.json').write_text(json.dumps({a:action_target(a) for a in ('attack','dodge','ember','mend','potion','interact','inventory','menu')},indent=2))
    tap_action('attack')
    deadline=time.monotonic()+15
    while time.monotonic()<deadline:
        logs=adb('logcat','-d')
        if 'MOSSLIGHT_COMBAT kind=attack_completed' in logs: break
        time.sleep(.5)
    else: raise RuntimeError('Real Android Attack touch did not finish a free swing')
    assert len(re.findall(r'MOSSLIGHT_COMBAT kind=attack_started',logs))==1, 'Attack touch activated more than once'
    tap_action('dodge')
    deadline=time.monotonic()+15
    while time.monotonic()<deadline:
        logs=adb('logcat','-d')
        if 'MOSSLIGHT_COMBAT kind=dodge_completed' in logs: break
        time.sleep(.5)
    else: raise RuntimeError('Real Android Dodge touch did not complete')
    assert len(re.findall(r'MOSSLIGHT_COMBAT kind=dodge_started',logs))==1, 'Dodge touch activated more than once'
    capture('combat-controls.png')
    # Wait for game-tick checkpoints, not assumed emulator wall-clock speed.
    # Count both starts and completions, so duplicate dispatch and stuck
    # actions fail. These swings do not claim confirmed-hit coverage.
    last_started={kind:int(re.findall(fr'MOSSLIGHT_COMBAT kind={kind}_started tick=(\d+)',logs)[-1]) for kind in ('attack','dodge')}
    for kind,total,x,y in [('attack',4,1198,712),('dodge',3,1088,714)]:
        for expected in range(2,total+1):
            ready_tick=last_started[kind]+(41 if kind=='attack' else 44)
            deadline=time.monotonic()+60
            while time.monotonic()<deadline:
                logs=adb('logcat','-d')
                checkpoints=[int(n) for n in re.findall(r'MOSSLIGHT_RUNTIME tick=(\d+)',logs)]
                if checkpoints and checkpoints[-1]>=ready_tick: break
                if not adb('shell','pidof',package): raise RuntimeError(f'Android process exited while waiting for {kind} cooldown')
                time.sleep(.5)
            else: raise RuntimeError(f'Android simulation did not reach {kind} cooldown tick {ready_tick}')
            prior_starts=set(re.findall(fr'MOSSLIGHT_COMBAT kind={kind}_started tick=(\d+)',logs))
            prior_completions=set(re.findall(fr'MOSSLIGHT_COMBAT kind={kind}_completed tick=(\d+)',logs))
            tap_action(kind)
            deadline=time.monotonic()+15
            while time.monotonic()<deadline:
                logs=adb('logcat','-d')
                starts=set(re.findall(fr'MOSSLIGHT_COMBAT kind={kind}_started tick=(\d+)',logs))-prior_starts
                completions=set(re.findall(fr'MOSSLIGHT_COMBAT kind={kind}_completed tick=(\d+)',logs))-prior_completions
                assert len(starts)<=1 and len(completions)<=1, f'{kind}: duplicate touch dispatch'
                if len(starts)==1 and len(completions)==1: break
                if not adb('shell','pidof',package): raise RuntimeError(f'Android process exited during repeated {kind}')
                time.sleep(.5)
            else: raise RuntimeError(f'Android {kind} touch {expected} did not finish')
            last_started[kind]=int(next(iter(starts)))
    # Start a fresh real session before the chest route. The long repeated
    # control phase gives roaming enemies time to approach the original area.
    # Reset through the same menu a player uses, without altering combat rules.
    before_reset=len(re.findall(r'MOSSLIGHT_SESSION_STARTED tick=0',logs))
    tap_action('menu')
    time.sleep(1)
    capture('reset-menu.png')
    tap(640,566) # New game in the initial menu's visible row.
    deadline=time.monotonic()+20
    while time.monotonic()<deadline:
        logs=adb('logcat','-d')
        if len(re.findall(r'MOSSLIGHT_SESSION_STARTED tick=0',logs))>before_reset: break
        time.sleep(.5)
    else: raise RuntimeError('New-game touch did not reset the gameplay session')
    # Exercise actual touch ownership, then open the starting Gloot chest.
    # Read release positions from production diagnostics instead of assuming emulator FPS.
    for attempt in range(16):
        adb('shell','input','swipe',str(int(140*w/1280)),str(int(640*h/800)),str(int(280*w/1280)),str(int(640*h/800)),'600')
        time.sleep(.4)
        ensure_foreground(timeout=10)
        logs=adb('logcat','-d')
        positions=re.findall(r'MOSSLIGHT_TOUCH_RELEASE x=([0-9.]+) y=([0-9.]+)',logs)
        if positions and float(positions[-1][0])>=265: break
    else: raise RuntimeError('Touch movement never approached starting chest')
    assert float(positions[-1][0])>220,'Touch input did not change player coordinates'
    capture('chest.png')
    tap_action('interact')
    time.sleep(1)
    logs=adb('logcat','-d')
    assert 'MOSSLIGHT_GAMEPLAY kind=resource_depleted value=chest' in logs,'Touch interaction did not empty authoritative chest inventory'
    interactions=re.findall(r'MOSSLIGHT_INTERACT feedback=([^\r\n]+)',logs)
    assert len(interactions)==1 and interactions[0].startswith('Chest emptied'),f'One physical tap dispatched {len(interactions)} interactions: {interactions}'
    capture('exploration.png')
    # The initial warden stands at ground x=348 after feature-clearance placement.
    # Move into dialogue range using real touch releases, rather than assuming chest range is enough.
    for approach in range(12):
        logs=adb('logcat','-d')
        positions=re.findall(r'MOSSLIGHT_TOUCH_RELEASE x=([0-9.]+) y=([0-9.]+)',logs)
        current_x=float(positions[-1][0]) if positions else 0
        if 305<=current_x<=330: break
        end_x=280 if current_x<305 else 50
        adb('shell','input','swipe',str(int(140*w/1280)),str(int(640*h/800)),str(int(end_x*w/1280)),str(int(640*h/800)),'180')
        time.sleep(.35)
        ensure_foreground(timeout=10)
    else: raise RuntimeError('OS-touch route never reached the warden dialogue stand')
    capture('warden-approach.png')
    tap_action('interact')
    deadline=time.monotonic()+15
    while time.monotonic()<deadline:
        logs=adb('logcat','-d')
        choices=re.findall(r'MOSSLIGHT_DIALOGUE_CHOICE text=(.*?) x=([0-9.]+) y=([0-9.]+)',logs)
        help_choice=next((row for row in reversed(choices) if row[0].startswith('I can help')),None)
        if help_choice: break
        time.sleep(.25)
    else: raise RuntimeError('OS-touch Interact did not open contextual warden dialogue')
    capture('warden-dialogue.png')
    tap(float(help_choice[1]),float(help_choice[2]))
    deadline=time.monotonic()+10
    while time.monotonic()<deadline:
        logs=adb('logcat','-d')
        if 'MOSSLIGHT_QUEST_ACCEPTED id=1' in logs: break
        time.sleep(.25)
    else: raise RuntimeError('OS-touch dialogue choice did not accept introductory quest')
    choices=re.findall(r'MOSSLIGHT_DIALOGUE_CHOICE text=Continue x=([0-9.]+) y=([0-9.]+)',logs)
    deadline=time.monotonic()+10
    while not choices and time.monotonic()<deadline:
        time.sleep(.25)
        choices=re.findall(r'MOSSLIGHT_DIALOGUE_CHOICE text=Continue x=([0-9.]+) y=([0-9.]+)',adb('logcat','-d'))
    assert choices,'Missing actual Continue control'
    tap(float(choices[-1][0]),float(choices[-1][1]))
    time.sleep(.5)
    # Approach the live enemy group and observe real damage events. This also
    # exercises repeated confirmed-hit effects on the Android renderer.
    previous_hits=set(re.findall(r'MOSSLIGHT_PLAYER_DAMAGE tick=(\d+) health=([0-9.]+)',logs))
    deadline=time.monotonic()+45
    approaches=0
    while time.monotonic()<deadline:
        logs=adb('logcat','-d')
        observed_hits=set(re.findall(r'MOSSLIGHT_PLAYER_DAMAGE tick=(\d+) health=([0-9.]+)',logs))-previous_hits
        if len(observed_hits)>=2: break
        if not adb('shell','pidof',package): raise RuntimeError('Android process exited during real combat damage')
        if approaches<2:
            adb('shell','input','swipe',str(int(140*w/1280)),str(int(640*h/800)),str(int(280*w/1280)),str(int(640*h/800)),'600')
            approaches+=1
        else: time.sleep(.5)
    else: raise RuntimeError('Android combat did not produce two real player damage events')
    tap_action('inventory')
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
    # Repeated background/foreground transitions are intentional: an earlier
    # Part 2 run resumed to a black Android surface despite a live game process,
    # and a single successful retry cannot exclude an intermittent regression.
    # Each cycle checks the actual composed framebuffer, not only CPU markers.
    # The developer fixture uses real profiles, PPC bodies, SPELL and projectile flight.
    # F9 enters the same reversible course exposed in Developer diagnostics.
    adb('shell','input','keyevent','139') # Android KEYCODE_F9
    deadline=time.monotonic()+45
    while time.monotonic()<deadline:
        lab_logs=adb('logcat','-d')
        if 'MOSSLIGHT_TACTICAL_LAB profiles=prowler,brute,spitter' in lab_logs and 'MOSSLIGHT_LAB_PROJECTILE kind=ProjectileReleased ability=spit' in lab_logs: break
        time.sleep(.5)
    else: raise RuntimeError('Android mixed archetype fixture did not enter and release a real spit projectile')
    capture('mixed-archetypes.png',wait=20)
    spell_before=adb('logcat','-d').count('MOSSLIGHT_PLAYER_ABILITY id=ember ')
    tap_action('ember')
    deadline=time.monotonic()+15
    while time.monotonic()<deadline:
        count=adb('logcat','-d').count('MOSSLIGHT_PLAYER_ABILITY id=ember ')-spell_before
        assert count<=1, 'One spell tap double-dispatched'
        if count==1: break
        time.sleep(.25)
    else: raise RuntimeError('Android Ember action did not activate')
    capture('spell-cooldown.png',wait=10)
    lifecycle=[]
    original_pid=adb('shell','pidof',package)
    assert original_pid,'Application exited before Android lifecycle verification'
    for cycle in range(3):
        prior_logs=adb('logcat','-d')
        prior_cancels=prior_logs.count('MOSSLIGHT_INPUT_CANCELLED')
        adb('shell','input','keyevent','3') # Background saves and cancels held input.
        time.sleep(2)
        adb('shell','monkey','-p',package,'-c','android.intent.category.LAUNCHER','1')
        ensure_foreground(timeout=30)
        image_name='resumed-menu.png' if cycle==0 else f'resumed-menu-{cycle+1}.png'
        capture(image_name,wait=20)
        # Resume actual mixed combat between cycles, exercising rebuilt transient views.
        tap(640,178)
        time.sleep(1)
        capture(f'resumed-combat-{cycle+1}.png',wait=20)
        resumed_pid=adb('shell','pidof',package)
        logs=adb('logcat','-d')
        (out/f'resume-{cycle+1}.log').write_text(
            f'initial_pid={original_pid} resumed_pid={resumed_pid} '
            f'cancel_count_before={prior_cancels} cancel_count_after={logs.count("MOSSLIGHT_INPUT_CANCELLED")}\\n')
        assert resumed_pid==original_pid, f'Android process restarted on resume cycle {cycle+1}'
        assert logs.count('MOSSLIGHT_INPUT_CANCELLED')>prior_cancels, (
            f'Background cycle {cycle+1} never canceled gameplay input')
        lifecycle.append({'cycle':cycle+1,'pid':resumed_pid,'screenshot':image_name,
            'inputCancelCount':logs.count('MOSSLIGHT_INPUT_CANCELLED')})
    # Exercise the existing developer region-entry actions on the installed
    # exported application. F10 uses World.Enter, leaving the reversible combat
    # fixture first; it is not a mock scene or a direct edit of region state.
    regions=[]
    for cycle in range(3):
        for region in ('ruin','reach'):
            prior=adb('logcat','-d').count('MOSSLIGHT_ENVIRONMENT_READY')
            adb('shell','input','keyevent','140') # Android KEYCODE_F10
            deadline=time.monotonic()+30
            while time.monotonic()<deadline:
                logs=adb('logcat','-d')
                marks=re.findall(r'MOSSLIGHT_ENVIRONMENT_READY region=(\w+) walls=(\d+) decorations=(\d+) atlases=(\d+)',logs)
                if len(marks)>prior:
                    assert marks[-1][0]==region, 'Unexpected Android region transition'
                    break
                time.sleep(.25)
            else: raise RuntimeError('Installed Android never entered '+region)
            time.sleep(.75)
            capture(f'{region}-entry-{cycle+1}.png',wait=20)
            if region=='ruin':
                landmark_marks=re.findall(r'MOSSLIGHT_LANDMARK_READY region=ruin rooms=(\d+) structures=(\d+) templates=([^\r\n]+)',logs)
                assert landmark_marks and int(landmark_marks[-1][0])>=2 and int(landmark_marks[-1][1])>0, 'Exported dungeon lacks authored landmark structures'
                assert 'ruined-entrance' in landmark_marks[-1][2] and 'relic-sanctuary' in landmark_marks[-1][2], 'Required mission landmarks absent'
                deadline=time.monotonic()+10
                while time.monotonic()<deadline:
                    logs=adb('logcat','-d')
                    if 'MOSSLIGHT_GAMEPLAY kind=landmark_discovered' in logs: break
                    time.sleep(.25)
                else: raise RuntimeError('Installed production room entry did not emit discovery')
                movement_before=logs.count('MOSSLIGHT_LANDMARK_MOVEMENT')
                for move_x in (230,50):
                    adb('shell','input','swipe',str(int(140*w/1280)),str(int(640*h/800)),str(int(move_x*w/1280)),str(int(640*h/800)),'500')
                    time.sleep(.3)
                logs=adb('logcat','-d')
                movement=re.findall(r'MOSSLIGHT_LANDMARK_MOVEMENT clear=(True|False) x=([0-9.]+) y=([0-9.]+)',logs)
                assert len(movement)>=movement_before+2, 'Dungeon joystick input did not complete'
                assert all(m[0]=='True' for m in movement), 'Installed Android player overlaps authored terrain'
                assert len({(m[1],m[2]) for m in movement[-2:]})>1, 'Dungeon joystick produced no displacement'
                for action,marker in (('attack','attack_completed'),('dodge','dodge_completed')):
                    before=adb('logcat','-d').count('MOSSLIGHT_COMBAT kind='+marker)
                    tap_action(action)
                    deadline=time.monotonic()+15
                    while time.monotonic()<deadline:
                        logs=adb('logcat','-d')
                        completed=logs.count('MOSSLIGHT_COMBAT kind='+marker)-before
                        assert completed<=1, 'One dungeon action tap double-dispatched'
                        if completed==1: break
                        time.sleep(.25)
                    else: raise RuntimeError('No '+action+' completion inside exported dungeon')
                capture(f'ruin-combat-{cycle+1}.png',wait=20)
            marks=re.findall(r'MOSSLIGHT_ENVIRONMENT_READY region=(\w+) walls=(\d+) decorations=(\d+) atlases=(\d+)',logs)
            state=marks[-1]
            assert int(state[3])==3, 'Unbounded or missing environment atlas cache'
            assert int(state[2])<=96, 'Unbounded Android decoration count'
            previous=[r for r in regions if r['region']==region]
            if previous: assert previous[-1]['counts']==list(state[1:]), 'Android region views accumulate'
            regions.append({'cycle':cycle+1,'region':region,'counts':list(state[1:])})
    (out/'regions.json').write_text(json.dumps(regions,indent=2))
    (out/'lifecycle.json').write_text(json.dumps(lifecycle,indent=2))
    (out/'logcat.log').write_text(logs)
    assert 'MOSSLIGHT_RUNTIME' in logs,'Gameplay did not progress after touchscreen Resume'
    for error in ['FATAL EXCEPTION','Fatal signal','SCRIPT ERROR:','System.DllNotFoundException','Unhandled exception','E godot   : ERROR:','Program linking failed','unable to bind shader']:
        assert error not in logs, f'Android runtime error: {error}'
    (out/'result.txt').write_text('PASS: x86_64 Android emulator install, C# scene launch, touch Resume, nonblank rendered world/menu/pack, measured player movement, Gloot chest depletion, four free Attack and three Dodge touches, at least two real player damage events, inventory screenshot, gameplay ticks, three archetypes and real spit release, three nonblank background/resume cycles during mixed combat with input cancellation and stable process identity. three generated-dungeon entry/attack/roll/return cycles with stable wall/decoration/atlas counts. Warden dialogue and introductory quest accepted with actual OS touches; production entrance discovery observed. Complete relic/return/reward OS-touch adventure is NOT covered. ARM64 physical device not tested.\n')
    print((out/'result.txt').read_text())
finally:
    # Preserve the real surface recording on failure as well as success. ADB
    # process termination alone can leave screenrecord running with an unclosed MP4.
    if recorder is not None and recorder.poll() is None:
        try:
            for pid in adb('shell','pidof','screenrecord').split():
                if pid.isdigit(): adb('shell','kill','-2',pid)
            recorder.communicate(timeout=15)
            adb('pull','/sdcard/mosslight-playtest.mp4',str(out/'gameplay-failure.mp4'))
        except Exception as error:
            (out/'recording-cleanup.log').write_text(str(error))
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
