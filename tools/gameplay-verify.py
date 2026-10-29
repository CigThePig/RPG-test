"""Run production Godot touch replays; record actual renderer output, never synthesize frames.
Movie mode proves visual behavior at a fixed clock, NOT real-time frame pacing.
"""
from pathlib import Path
import argparse, json, os, subprocess, statistics, math, hashlib, time
from PIL import Image, ImageChops, ImageStat, ImageDraw
root=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--presets',default='baseline,precise,soft,explore');p.add_argument('--realtime',action='store_true');p.add_argument('--output',default='build/gameplay');a=p.parse_args()
out=root/a.output;out.mkdir(parents=True,exist_ok=True)
def run(command,**kw):return subprocess.run(command,check=True,timeout=360,**kw)
def percentile(items,q):return sorted(items)[int((len(items)-1)*q)]
summary={}
for preset in a.presets.split(','):
    folder=out/preset;folder.mkdir(exist_ok=True)
    command=[os.environ.get('GODOT','godot'),'--path',str(root),'--audio-driver','Dummy','--resolution','1280x800']
    if not a.realtime: command+=['--write-movie',str(folder/'gameplay.avi'),'--fixed-fps','60']
    command+=['--','--gameplay-check','--mobile-ui',f'--preset={preset}',f'--evidence={folder}']
    recorder=None
    capture_utc=time.time()
    if a.realtime:
        recorder=subprocess.Popen(['ffmpeg','-v','error','-y','-f','x11grab','-video_size','1280x800','-framerate','30','-i',os.environ['DISPLAY'],'-c:v','libx264','-preset','ultrafast','-crf','24','-pix_fmt','yuv420p',str(folder/'gameplay.mp4')],stdin=subprocess.PIPE)
    try:
        result=subprocess.run(command,cwd=root,capture_output=True,text=True,timeout=600)
    finally:
        if recorder is not None:
            recorder.communicate(b'q',timeout=30)
            if recorder.returncode:raise RuntimeError('Real-time FFmpeg capture failed')
    logs=result.stdout+result.stderr;(folder/'engine.log').write_text(logs)
    if result.returncode or 'GAMEPLAY REPLAY PASSED' not in logs or 'ERROR:' in logs:raise RuntimeError(logs[-6000:])
    rows=[json.loads(line) for line in (folder/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    samples=[r for r in rows if r['kind']=='sample'];assert samples
    def state_digest(trace):
        # Exclude rendering/time/environment. Keep received analog, authoritative
        # position/state, collision and scenario for each physics iteration.
        state=[(r['scenario'],r['tick'],round(r['x'],4),round(r['y'],4),round(r['analog'],4),r['action'],r['blocked']) for r in trace if r['kind']=='sample']
        return hashlib.sha256(json.dumps(state).encode()).hexdigest()
    digest=state_digest(rows)
    repeat=folder/'repeat';repeat.mkdir(exist_ok=True)
    repeat_command=[os.environ.get('GODOT','godot'),'--resolution','1280x800','--path',str(root),'--audio-driver','Dummy','--fixed-fps','60','--','--gameplay-check','--mobile-ui',f'--preset={preset}',f'--evidence={repeat}']
    # The repeat still renders every frame and can exceed two minutes on a
    # constrained software renderer as the scenario suite grows.
    repeated=subprocess.run(repeat_command,cwd=root,capture_output=True,text=True,timeout=450)
    (repeat/'engine.log').write_text(repeated.stdout+repeated.stderr)
    assert repeated.returncode==0 and 'GAMEPLAY REPLAY PASSED' in repeated.stdout and 'ERROR:' not in repeated.stdout+repeated.stderr, 'Replay repetition failed'
    repeat_rows=[json.loads(line) for line in (repeat/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    assert state_digest(repeat_rows)==digest, 'Non-deterministic physics/input replay; compare trace.jsonl with repeat/trace.jsonl'
    session=rows[0]
    assert any(r['renderCount']>0 for r in samples), 'Creature was never rasterized'
    assert all(not r['renderError'] for r in samples), 'Creature raster errors'
    rendered=[r for r in rows if r['kind']=='render-frame']
    gaps=[rendered[i]['wallMs']-rendered[i-1]['wallMs'] for i in range(1,len(rendered))]
    pacing={f'p{int(q*100)}ms':percentile(gaps,q) for q in [.5,.95,.99]} if gaps else {}
    groups={}
    problems=[]
    for r in samples:
        groups.setdefault(r['scenario'],[]).append(r)
        if r['blocked'] and r['analog']>.2:problems.append({'scenario':r['scenario'],'frame':r['frame'],'movieSeconds':r['frame']/60,'reason':r['blocked']})
    # These hashes identify distinct PPC indexed bitmaps actually uploaded by
    # the renderer. They include all changes in those frames, not a subjective
    # judgment that each silhouette reads well at ordinary playback speed.
    pose_specs={
        'sword':('combat-ahead','playerPoseHash',lambda r:r['action'] in ('Windup','Recovery')),
        'bite':('telegraph-bite','observedPoseHash',lambda r:r['observedActionActive'] and r['observedStyle']=='Bite'),
        'tailSwipe':('telegraph-cone','observedPoseHash',lambda r:r['observedActionActive'] and r['observedStyle']=='TailSwipe'),
        'hitReaction':('telegraph-bite','playerPoseHash',lambda r:r['action']=='Staggered'),
        'dodge':('dodge-idle','playerPoseHash',lambda r:r['action']=='Dodging'),
    }
    pose_evidence={name:len({r[field] for r in groups[scenario] if predicate(r) and r[field]})
                   for name,(scenario,field,predicate) in pose_specs.items()}
    if not a.realtime:
        for name,count in pose_evidence.items():
            assert count>=3, f'{name}: only {count} distinct indexed creature frames during the action'
    metrics={}
    for name,values in groups.items():
        active=[r for r in values if r['analog']>.95 and not r['blocked'] and r['action']=='Moving']
        speed=[math.hypot(r['vx'],r['vy']*.5) for r in active]
        gaps=[values[i]['wallMs']-values[i-1]['wallMs'] for i in range(1,len(values))]
        settled=values[30:-15] or values
        lag=[math.hypot(r['cameraTargetX']-r['cameraX'],r['cameraTargetY']-r['cameraY']) for r in settled]
        metrics[name]={'settledCameraErrorMedian':statistics.median(lag),'projectedSpeed':statistics.median(speed) if speed else 0,'blockedSamples':sum(bool(r['blocked']) for r in values),'samples':len(values),'wallFrameP50ms':percentile(gaps,.5) if gaps else 0,'wallFrameP95ms':percentile(gaps,.95) if gaps else 0,'wallFrameP99ms':percentile(gaps,.99) if gaps else 0,'maxCameraError':max(math.hypot(r['cameraTargetX']-r['cameraX'],r['cameraTargetY']-r['cameraY']) for r in values)}
    # Verify real dispatch, ownership, displacement, release, authoritative clearance.
    assert any(r['kind']=='touch' and r['finger']==7 for r in rows)
    assert any(r['kind']=='touch' and r['finger']==8 for r in rows)
    for name in ['horizontal','vertical','diagonal','tap-stop','reversal']:
        values=groups[name];assert any(math.hypot(r['vx'],r['vy'])>1 for r in values),name
        tail=values[-8:];assert all(math.hypot(r['vx'],r['vy'])<.1 for r in tail),f'{name}: movement after release'
    if preset!='baseline':
        speeds=[metrics[n]['projectedSpeed'] for n in ['horizontal','vertical','diagonal']]
        assert max(speeds)/min(speeds)<1.03,(preset,speeds)
    if not a.realtime:
        run(['ffmpeg','-v','error','-y','-i',str(folder/'gameplay.avi'),'-c:v','libx264','-preset','fast','-crf','24','-pix_fmt','yuv420p','-an',str(folder/'gameplay.mp4')])
    run(['ffmpeg','-v','error','-i',str(folder/'gameplay.mp4'),'-vf','fps=2,scale=640:400',str(folder/'frame-%03d.png')])
    images=sorted(folder.glob('frame-*.png'));assert len(images)>20,'Insufficient video frames'
    changed=0
    for previous,current in zip(images,images[1:]):
        old=Image.open(previous).convert('RGB').crop((120,80,520,280));new=Image.open(current).convert('RGB').crop((120,80,520,280))
        changed+=sum(ImageStat.Stat(ImageChops.difference(old,new)).mean)>1
    assert changed>8,'Frozen gameplay recording'
    for f in images[::5]:
        im=Image.open(f).convert('RGB');assert len(im.getcolors(im.width*im.height))>100,'Blank rendering'
    # Frame/time labels make every thumbnail locatable in the full clip and trace.
    chosen=[images[round(i*(len(images)-1)/19)] for i in range(20)]
    sheet=Image.new('RGB',(4*320,5*220));draw=ImageDraw.Draw(sheet)
    for i,f in enumerate(chosen):
        im=Image.open(f);im.thumbnail((320,200));x=(i%4)*320;y=(i//4)*220;sheet.paste(im,(x,y));draw.text((x+5,y+201),f'{preset} t={(int(f.stem.split("-")[-1])-1)/2:.1f}s',fill='white')
    sheet.save(folder/'contact-sheet.jpg')
    if not a.realtime and 'combat-empty' in groups:
        # Extract actual movie frames at gameplay states, including 50ms samples
        # matching the preferred 20-FPS procedural raster cadence.
        requests=[]
        for name in ['combat-empty','combat-ahead','combat-behind','combat-enter','combat-wall',
                     'dodge-idle','dodge-held','dodge-wall','dodge-evasion']:
            values=groups[name]
            action='Windup' if name.startswith('combat') else 'Dodging'
            start=next(i for i,r in enumerate(values) if r['action']==action)
            for label,offset in [('start',0),('middle',8 if action=='Windup' else 12),('recovery',20)]:
                row=values[min(start+offset,len(values)-1)]
                requests.append((name+' '+label, max(0,int(row['frame'])-1), row))
        values=groups['dodge-idle']; start=next(i for i,r in enumerate(values) if r['action']=='Dodging')
        for i in range(8):
            row=values[min(start+i*3,len(values)-1)]
            requests.append((f'dodge 20fps +{i*50}ms',max(0,int(row['frame'])-1),row))
        values=groups['turn-south-north']
        for i in range(8):
            row=values[min(i*7,len(values)-1)]
            requests.append((f'south to north {i}',max(0,int(row['frame'])-1),row))
        for name,values in groups.items():
            if not name.startswith('tactics-'): continue
            for phase in [24,54,60,66,90,120,150]:
                row=values[min(phase,len(values)-1)]
                requests.append((f'{name} tick {phase}',max(0,int(row['frame'])-1),row))
        warning_requests=[]
        for name in ['telegraph-bite','telegraph-dodge','telegraph-interrupt','telegraph-cone']:
            for phase in ['early','middle','after-impact']:
                marker=next(r for r in rows if r['kind']=='marker' and r['scenario']==name and r['label']=='telegraph/'+phase)
                # Markers are emitted before the physics step; inspect the next rendered frame.
                frame=int(marker['frame'])
                row=min(groups[name],key=lambda r:abs(int(r['frame'])-frame))
                label=name+' '+phase
                requests.append((label,frame,row));warning_requests.append((name,phase,frame))
            if name=='telegraph-interrupt':
                marker=next(r for r in rows if r['kind']=='marker' and r['scenario']==name and r['label']=='telegraph/interrupted')
                frame=int(marker['frame'])+1
                row=min(groups[name],key=lambda r:abs(int(r['frame'])-frame))
                requests.append((name+' interrupted',frame,row));warning_requests.append((name,'interrupted',frame))
        # Complete unzoomed temporal sequences around real world events.
        presentation_requests=[]
        for name,phases in {
            'presentation-fatal':['start','hit','death'],
            'presentation-crowd':['start','hit'],
            'presentation-projectile':['start','release','collision','hit']
        }.items():
            for phase in phases:
                marker=next(r for r in rows if r['kind']=='marker' and r['scenario']==name and r['label']=='presentation/'+phase)
                for offset in [-3,0,3,12]:
                    frame=max(0,int(marker['frame'])+offset)
                    row=min(groups[name],key=lambda r:abs(int(r['frame'])-frame))
                    label=f'{name} {phase} {offset:+d}'
                    requests.append((label,frame,row));presentation_requests.append((name,phase,offset,frame))
        values=groups['combat-ahead']; start=next(i for i,r in enumerate(values) if r['action']=='Windup')
        for i in range(9):
            row=values[min(start+i*3,len(values)-1)]; frame=max(0,int(row['frame'])-1)
            requests.append((f'sword +{i*50}ms',frame,row));presentation_requests.append(('sword','sequence',i,frame))
        indices=sorted(set(frame for _,frame,_ in requests))
        select='+'.join(f'eq(n\\,{frame})' for frame in indices)
        run(['ffmpeg','-v','error','-y','-i',str(folder/'gameplay.mp4'),'-vf',f'select={select}','-vsync','0',str(folder/'combat-%03d.png')])
        selected=sorted(folder.glob('combat-*.png'))
        assert len(selected)==len(indices), 'Missing requested combat movie frames'
        by_frame=dict(zip(indices,selected))
        # Verify copied evidence while the original movie frames are still
        # available; fail rather than publish a truncated or corrupt image.
        def save_frame(frame,path):
            for attempt in range(3):
                with Image.open(by_frame[frame]) as source: source.save(path)
                try:
                    with Image.open(path) as copy: copy.verify()
                    return
                except (OSError, SyntaxError):
                    if attempt==2: raise
        for name,phase,frame in warning_requests:
            save_frame(frame,folder/(name+'-'+phase+'.png'))
        for name,phase,offset,frame in presentation_requests:
            save_frame(frame,folder/(f'{name}-{phase}-{offset:+03d}.png'))
        combat=Image.new('RGB',(1280,math.ceil(len(requests)/4)*220));draw=ImageDraw.Draw(combat)
        close=Image.new('RGB',combat.size); close_draw=ImageDraw.Draw(close)
        for i,(label,frame,row) in enumerate(requests):
            x=(i%4)*320;y=(i//4)*220
            img=Image.open(by_frame[frame]).convert('RGB')
            combat.paste(img.resize((320,200)),(x,y))
            # Center on the authoritative player's projected ground point, keeping
            # headroom for limb and trail inspection at the real 20-FPS cadence.
            cx=float(row['playerScreenX']);cy=float(row['playerScreenY'])
            crop=img.crop((round(cx-120),round(cy-110),round(cx+120),round(cy+40))).resize((320,200),Image.Resampling.NEAREST)
            close.paste(crop,(x,y))
            draw.text((x+4,y+202),f'{label} frame {frame}',fill='white')
            close_draw.text((x+4,y+202),f'{label} frame {frame}',fill='white')
        combat.save(folder/'combat-contact-sheet.jpg')
        close.save(folder/'combat-closeups.jpg')
        for f in selected:f.unlink()
    if (folder/'gameplay.avi').exists(): (folder/'gameplay.avi').unlink()
    for f in images:f.unlink()
    status=subprocess.check_output(['git','status','--porcelain','--untracked-files=all'],cwd=root,text=True).splitlines()
    report={'source':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'dirty':bool(status),'statusEntries':status,'preset':preset,'stateDigest':digest,'deterministicRepeat':True,'movieMode':not a.realtime,'captureStartUnix':capture_utc,'environment':session,'renderWallPacing':pacing,'distinctIndexedCreatureFrames':pose_evidence,'simulationSeconds':(samples[-1]['tick']-samples[0]['tick'])/60,'wallSeconds':(samples[-1]['wallMs']-samples[0]['wallMs'])/1000,'metrics':metrics,'problemPeriods':problems,'visualReview':'Automated sanity/temporal checks only. Human or vision inspection must be reported separately.'}
    (folder/'report.json').write_text(json.dumps(report,indent=2));summary[preset]=metrics
(out/'comparison.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
