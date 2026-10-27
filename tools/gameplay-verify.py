"""Run production Godot touch replays; record actual renderer output, never synthesize frames.
Movie mode proves visual behavior at a fixed clock, NOT real-time frame pacing.
"""
from pathlib import Path
import argparse, json, os, subprocess, statistics, math, hashlib
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
    result=subprocess.run(command,cwd=root,capture_output=True,text=True,timeout=360)
    logs=result.stdout+result.stderr;(folder/'engine.log').write_text(logs)
    if result.returncode or 'GAMEPLAY REPLAY PASSED' not in logs or 'ERROR:' in logs:raise RuntimeError(logs[-6000:])
    rows=[json.loads(line) for line in (folder/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    samples=[r for r in rows if r['kind']=='sample'];assert samples
    session=rows[0]
    groups={}
    problems=[]
    for r in samples:
        groups.setdefault(r['scenario'],[]).append(r)
        if r['blocked'] and r['analog']>.2:problems.append({'scenario':r['scenario'],'frame':r['frame'],'movieSeconds':r['frame']/60,'reason':r['blocked']})
    metrics={}
    for name,values in groups.items():
        active=[r for r in values if r['analog']>.95 and not r['blocked'] and r['action']=='Moving']
        speed=[math.hypot(r['vx'],r['vy']*.5) for r in active]
        gaps=[values[i]['wallMs']-values[i-1]['wallMs'] for i in range(1,len(values))]
        metrics[name]={'projectedSpeed':statistics.median(speed) if speed else 0,'blockedSamples':sum(bool(r['blocked']) for r in values),'samples':len(values),'wallFrameP50ms':percentile(gaps,.5) if gaps else 0,'wallFrameP95ms':percentile(gaps,.95) if gaps else 0,'wallFrameP99ms':percentile(gaps,.99) if gaps else 0,'maxCameraError':max(math.hypot(r['cameraTargetX']-r['cameraX'],r['cameraTargetY']-r['cameraY']) for r in values)}
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
        (folder/'gameplay.avi').unlink()
        for f in images:f.unlink()
    report={'source':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'dirty':bool(subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True)),'preset':preset,'movieMode':not a.realtime,'environment':session,'metrics':metrics,'problemPeriods':problems,'visualReview':'Automated sanity/temporal checks only. Human or vision inspection must be reported separately.'}
    (folder/'report.json').write_text(json.dumps(report,indent=2));summary[preset]=metrics
(out/'comparison.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
