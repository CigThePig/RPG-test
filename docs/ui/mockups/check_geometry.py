#!/usr/bin/env python3
"""HUD v3 geometry check (PART2_HUD.md §1-2, CREATIVE_DIRECTION.md C3).
Verifies >=12*s lp gaps and safe-area containment across window sizes, insets,
handedness and button scale. Port the same table into HudChecks.cs."""
G={'attack':(172,172,136,136),'dodge':(330,150,108,108),'skill1':(300,282,92,92),'skill2':(156,330,92,92),
'potion':(446,214,88,88),'skill3':(260,430,88,88),'interact':(480,384,184,80)}
def inter(a,b,gap=0):
    return not (a[0]+a[2]+gap<=b[0] or b[0]+b[2]+gap<=a[0] or a[1]+a[3]+gap<=b[1] or b[1]+b[3]+gap<=a[1])
bad_any=False;n=0
for W,H in [(1280,800),(1280,720),(1920,864),(1024,768),(1728,800),(1280,640),(1100,600),(1100,640),(900,560),(800,480)]:
  for inset in [(50,24),(0,0),(120,0),(0,40)]:
   for left in [False,True]:
    for bscale in [1.0,1.15]:
     n+=1
     safe=(inset[0],inset[1],W-2*inset[0],H-2*inset[1])
     s=min(1,safe[2]/900,safe[3]/560); ex,ey=safe[0]+safe[2],safe[1]+safe[3]; top=safe[1]; L=safe[0]
     a=s*bscale if safe[3]/(s*bscale)>=640 else s
     rs={k:(ex-r*a,ey-b*a,w*a,h*a) for k,(r,b,w,h) in G.items()}
     rs['stick']=(L+40*s,ey-260*s,200*s,200*s)
     if left: rs={k:(L+(ex-(x+w)),y,w,h) for k,(x,y,w,h) in rs.items()}
     tall = safe[3]/a>=640
     if tall:
        rs['minimap']=(ex-200*s,top+16*s,176*s,176*s)
        rs['inventory']=(ex-292*s,top+20*s,80*s,80*s); rs['menu']=(ex-292*s,top+112*s,80*s,80*s)
        card=safe[3]/a>=720
        rs['quest']=(L+24*s,top+146*s,360*s,112*s) if card else (L+24*s,top+146*s,80*s,80*s)
        rs['vitals']=(L+24*s,top+20*s,432*s,110*s)
     else:
        rs['minimap']=(ex-136*s,top+16*s,112*s,112*s)
        rs['inventory']=(ex-228*s,top+20*s,80*s,80*s); rs['menu']=(ex-320*s,top+20*s,80*s,80*s)
        rs['quest']=(ex-412*s,top+20*s,80*s,80*s)
        rs['vitals']=(L+24*s,top+20*s,432*s,96*s)
     bad=[(p,q) for p in rs for q in rs if p<q and inter(rs[p],rs[q],12*s-0.01)]
     out=[k for k,v in rs.items() if v[0]<safe[0]-.01 or v[1]<safe[1]-.01 or v[0]+v[2]>ex+.01 or v[1]+v[3]>ey+.01]
     if bad or out: bad_any=True; print(W,H,inset,'LH' if left else 'RH',bscale,'s=%.2f'%s,'tall' if tall else 'short',bad,out)
print(n,'layouts;','ALL OK' if not bad_any else 'PROBLEMS')
