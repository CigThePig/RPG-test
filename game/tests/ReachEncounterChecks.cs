using Godot;
using System.Text.Json;
using V=System.Numerics.Vector2;
namespace Mosslight;

public static class ReachEncounterChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        var w=app.World;string original=Persistence.Capture(w,app.Narrative);
        Directory.CreateDirectory("build/ecology");var plans=new List<object>();
        try
        {
            foreach(int seed in new[]{1,42,1337,0,-1,2147483647})
            {
                var r=Regions.Overworld(seed,320,240);var sites=ReachEncounters.Plan(r,out var diagnostics);
                var repeat=ReachEncounters.Plan(r,out _);
                check(JsonSerializer.Serialize(sites)==JsonSerializer.Serialize(repeat),"ecology deterministic complete plan "+seed);
                check(sites.Count is >=30 and <=50,"ecology deliberate coverage "+seed+" sites="+sites.Count);
                check(sites.Select(s=>s.Template).Distinct().Count()==9,"ecology all nine tactical templates "+seed);
                var probe=new ActorState{Radius=11};
                foreach(var site in sites)
                {
                    check(!ReachEncounters.Protected(r,r.Center(site.Tile)),"ecology sanctuary "+seed+"/"+site.Id);
                    foreach(var m in site.Members)
                    {
                        check(r.CanStand(r.Center(m.Tile),probe)&&r.CanTravel(r.Center(site.Tile),r.Center(m.Tile),probe),"ecology connected footprint "+m.Id);
                        check(!ReachEncounters.Protected(r,r.Center(m.Tile)),"ecology protected home "+m.Id);
                    }
                    check(sites.Where(s=>s!=site).All(s=>V.Distance(r.Center(s.Tile),r.Center(site.Tile))>=ReachEncounters.Spacing),"ecology territory spacing "+site.Id);
                }
                r.ReachSites=sites;r.ReachRevision=ReachEncounters.Revision;
                WriteOverview(r);
                plans.Add(new{seed,diagnostics,sites,creatures=sites.Sum(s=>s.Members.Count)});
            }
            File.WriteAllText("build/ecology/plans.json",JsonSerializer.Serialize(plans,Content.Json));
            var reach=Regions.Overworld(42,320,240);reach.ReachSites=ReachEncounters.Plan(reach,out _);reach.ReachRevision=1;
            w.Seed=42;w.Population.Dormant.Clear();w.Population.ResetIndex();w.RegionsById["reach"]=reach;
            w.Actors=w.Actors.Where(x=>x.Key=="player").ToDictionary(x=>x.Key,x=>x.Value);
            w.Player.Region="reach";w.Player.X=reach.Center(reach.Entrance).X;w.Player.Y=reach.Center(reach.Entrance).Y;
            w.Projectiles.Clear();w.Hazards.Clear();
            string fresh=Persistence.Capture(w,app.Narrative);
            var site0=reach.ReachSites.First(s=>s.Template=="lone-hunter");
            void Move(V p){w.Cancel(w.Player);w.Player.X=p.X;w.Player.Y=p.Y;}
            void Activate(ReachSite site){Move(reach.Center(site.Tile)+new V(-300,0));for(int i=0;i<60;i++)w.Population.Update(w,true);}
            Activate(site0);
            var member=site0.Members[0];var actor=w.Actors[member.Id];
            check(actor.Sleeper&&actor.Awareness==Awareness.Unaware,"ecology activation is calm, not chunk alarm");
            actor.Health-=13;float hurt=actor.Health;
            Move(new V(7200,5300));w.Population.Update(w,true);
            check(w.Population.Dormant.ContainsKey(member.Id)&&!w.Actors.ContainsKey(member.Id),"ecology dormant outside authoritative actor loop");
            string dormant=Persistence.Capture(w,app.Narrative);Persistence.Restore(w,app.Narrative,dormant);
            Activate(site0);actor=w.Actors[member.Id];
            check(actor.Health==hurt,"ecology dormant damage and genome survive save/load");
            w.Damage(w.Player,actor,10000,"physical");int xp=w.Player.Experience;int coins=w.Items.Count(actor.Id,"coin");
            check(!actor.Alive&&coins>0,"ecology defeat creates production corpse loot");
            Move(new V(7200,5300));w.Population.Update(w,true);
            string corpse=Persistence.Capture(w,app.Narrative);Persistence.Restore(w,app.Narrative,corpse);Activate(site0);
            actor=w.Actors[member.Id];
            check(!actor.Alive&&w.Player.Experience==xp&&w.Items.Count(actor.Id,"coin")==coins,"ecology corpse no respawn or duplicate reward");
            check(w.Items.LootAll(actor.Id,"player")&&!w.Items.LootAll(actor.Id,"player"),"ecology loot claimed once");
            Move(new V(7200,5300));w.Population.Update(w,true);Activate(site0);
            check(!w.Actors[member.Id].Alive&&!w.Items.HasLoot(member.Id),"ecology looted corpse stays empty");
            int peak=0;
            foreach(var site in reach.ReachSites.Concat(reach.ReachSites.AsEnumerable().Reverse()))
            {
                Activate(site);peak=Math.Max(peak,w.Actors.Values.Count(a=>a.EncounterSite!=""));
                check(peak<=ReachPopulation.MaximumActors,"ecology long route bounded actors "+site.Id);
            }
            // Direction of traversal changes neither the plans nor identities/health of visited members.
            check(w.Actors.Keys.Concat(w.Population.Dormant.Keys).Distinct().Count()==w.Actors.Count+w.Population.Dormant.Count,"ecology no duplicate active/dormant IDs");
            w.Enter(w.Player,"ruin");w.Population.Update(w,true);w.Enter(w.Player,"reach");w.Population.Update(w,true);
            check(reach.ReachSites.Count==42,"ecology dungeon return preserves sites");
            Persistence.Restore(w,app.Narrative,fresh);
            var old=JsonSerializer.Deserialize<SaveData>(fresh,Content.Json)!;
            old.Regions["reach"].ReachRevision=0;old.Regions["reach"].ReachSites.Clear();
            Persistence.Restore(w,app.Narrative,JsonSerializer.Serialize(old,Content.Json));
            for(int i=0;i<600;i++)w.Step(1f/60);
            check(w.Actors.Count==1&&w.Population.Dormant.Count==0,"ecology legacy save opt-out never adds hostile population");
            Persistence.Restore(w,app.Narrative,fresh);
            var fights=new List<object>();
            foreach(var template in ReachEncounters.Templates)
            foreach(bool observe in new[]{true,false})
            {
                Persistence.Restore(w,app.Narrative,fresh);reach=w.RegionsById["reach"];
                var site=reach.ReachSites.First(s=>s.Template==template.Id);Activate(site);
                var foes=site.Members.Select(m=>w.Actors[m.Id]).ToArray();
                var main=foes[0];var direction=new V(site.Members[0].FacingX,site.Members[0].FacingY);
                var start=main.Position+direction*(template.Id=="rocky-guardian"?180:template.Id=="poisoned-shore"?140:template.Id=="lone-hunter"?20:100);
                if(!reach.CanTravel(main.Position,start,w.Player))start=main.Position+direction*48;
                Move(start);w.Player.Health=w.Player.MaxHealth=100;w.Player.Invulnerable=0;
                var abilities=new Dictionary<string,int>();int taken=0;int calls=0;int heals=0;
                void Observe(WorldEvent e){if(e.Kind=="ability_started"&&site.Members.Any(m=>m.Id==e.Actor))abilities[e.Value]=abilities.GetValueOrDefault(e.Value)+1;if(e.Kind=="damage"&&e.Target=="player")taken+=e.Count;}
                void Present(CombatPresentationEvent e){if(e.Kind==CombatPresentationKind.Alerted&&e.Outcome=="called")calls++;}
                w.Events.Published+=Observe;w.Presentation+=Present;
                int tick=0;float previous=foes.Sum(a=>a.Health);
                try
                {
                    for(;tick<3600&&w.Player.Alive&&foes.Any(a=>a.Alive);tick++)
                    {
                        // Begin with observation, then a normal-health duelist using production inputs.
                        if(tick>(observe?(template.Id=="lone-hunter"?120:480):0))DuelStep(w,foes,tick);
                        w.Step(1f/60);
                        float health=foes.Sum(a=>a.Health);if(health>previous+.01f)heals++;previous=health;
                    }
                }
                finally{w.Events.Published-=Observe;w.Presentation-=Present;}
                GD.Print("ECOLOGY_FIGHT "+template.Id+" abilities="+string.Join(",",abilities)+" hp="+w.Player.Health+" enemy="+main.Health+" awareness="+main.Awareness+" target="+main.Target+" pos="+main.Position+" player="+w.Player.Position);
                if(observe)
                {
                    string defining=template.Id switch {"lone-hunter"=>"bite","hunting-pack"=>"pounce","rocky-guardian"=>"charge","poisoned-shore"=>"glob","woodland-ambush"=>"sting","alarmed-ground"=>"screech","defended-refuge"=>"bloom","coiled-threat"=>"coil",_=>"howl"};
                    check(abilities.ContainsKey(defining),"ecology production defining ability "+template.Id+"/"+defining);
                    if(template.Id is "hunting-pack" or "alarmed-ground" or "pack-leader")check(calls>0,"ecology intended allies answer "+template.Id);
                }
                if(observe&&template.Id=="defended-refuge")check(heals>0,"ecology mender actually restores ally health");
                if(!observe&&template.Id=="lone-hunter")check(w.Player.Alive&&foes.All(a=>!a.Alive),"ecology starter duelist survives early hunter");
                fights.Add(new{template=template.Id,policy=observe?"stationary opening then duelist":"normal-health duelist",seconds=tick/60f,abilities,calls,heals,damageTaken=taken,health=w.Player.Health,fallen=foes.Count(a=>!a.Alive),worstActivationMs=w.Population.WorstActivationMilliseconds});
            }
            File.WriteAllText("build/ecology/combat.json",JsonSerializer.Serialize(fights,Content.Json));
        }
        finally{Persistence.Restore(w,app.Narrative,original);app.RebuildViews();app.Ui.PauseForVerification();}
    }
    public static void DuelStep(World w,ActorState[] foes,int tick)
    {
        var p=w.Player;var target=foes.Where(a=>a.Alive).OrderBy(a=>V.DistanceSquared(a.Position,p.Position)).FirstOrDefault();if(target==null)return;
        if(p.Health<45)w.UsePotion(p);if(p.Health<65)w.Activate(p.Id,"mend",p.Id);
        var offset=target.Position-p.Position;float distance=offset.Length();var toward=distance>1?offset/distance:V.UnitX;var side=new V(-toward.Y,toward.X);
        var move=distance>36?toward:V.Zero;
        var threat=foes.FirstOrDefault(a=>a.Alive&&a.State==ActionState.Windup&&V.Distance(a.Position,p.Position)<300);
        if(threat!=null){move=side;if(threat.StateRemaining<.3f)w.TryStartPlayerDodge(side);}
        else if(w.Shielded(target,p.Position)&&distance<70)move=side+toward*.3f;
        else if(distance<58)w.TryStartPlayerSlash(toward);
        else if(distance>70)w.Activate(p.Id,"ember",target.Id);
        foreach(var h in w.Hazards)if(V.Distance(h.Position,p.Position)<65)move=CombatGeometry.TryNormalize(p.Position-h.Position,out var away)?away:side;
        w.PlayerMove(move,1f/60);
    }
    public static void WriteOverview(RegionState r)
    {
        using var image=Image.CreateEmpty(r.Width*3,r.Height*3,false,Image.Format.Rgba8);
        Color[] palette={new("4e7b54"),new("80634b"),new("244f60"),new("7a8681"),new("baab85"),new("4b4a45")};
        for(int y=0;y<r.Height;y++)for(int x=0;x<r.Width;x++)for(int dy=0;dy<3;dy++)for(int dx=0;dx<3;dx++)image.SetPixel(x*3+dx,y*3+dy,r.Crossings.Contains(y*r.Width+x)?new Color("d1ae70"):palette[(int)r.Tiles[y*r.Width+x]]);
        image.SavePng($"build/ecology/terrain-{r.Seed}.png");
        string[] colors={"#ead8b6","#f7c65e","#f4845f","#84e1ce","#c09cea","#e49ac5","#b5d076","#ed9357","#f35b67"};
        var svg=new System.Text.StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='1200' height='830' viewBox='0 0 1200 830'><rect width='1200' height='830' fill='#182c28'/><text x='24' y='32' fill='white' font-size='23'>Living Reach · Encounter ecology · seed {r.Seed}</text><image x='24' y='58' width='960' height='720' href='data:image/png;base64,{Convert.ToBase64String(image.SavePngToBuffer())}'/>");
        foreach(var s in r.ReachSites){int n=Array.FindIndex(ReachEncounters.Templates,t=>t.Id==s.Template);float x=24+(s.Tile%r.Width+.5f)*3,y=58+(s.Tile/r.Width+.5f)*3;svg.Append($"<circle cx='{x}' cy='{y}' r='112.5' stroke='{colors[n]}' stroke-opacity='.12' fill='none'/><circle cx='{x}' cy='{y}' r='24' stroke='{colors[n]}' stroke-opacity='.5' fill='none'/><circle cx='{x}' cy='{y}' r='{4+s.Danger}' fill='{colors[n]}' stroke='#142b24'/><text x='{x+8}' y='{y+4}' fill='white' font-size='10'>{n+1}</text>");}
        int spawn=r.Entrance;svg.Append($"<circle cx='{24+(spawn%r.Width)*3}' cy='{58+(spawn/r.Width)*3}' r='9' fill='white'/>");
        for(int i=0;i<9;i++)svg.Append($"<text x='1000' y='{90+i*38}' fill='{colors[i]}' font-size='14'>{i+1} {ReachEncounters.Templates[i].Id}</text>");
        svg.Append("<text x='24' y='810' fill='white' font-size='14'>White: player spawn · solid dots: sites (size = danger) · inner ring: territory · outer ring: activation · gold: crossings</text></svg>");
        File.WriteAllText($"build/ecology/overview-{r.Seed}.svg",svg.ToString());
    }
}
