using Godot;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Normal-health production-world driver. No invulnerability, forced damage, gates or quest flags.</summary>
public sealed class AdventureRun(Foundation app)
{
    World W => app.World;
    Node N => app.Narrative;
    int ticks, potions, attacks, spells, dodges, damage, levers, braziers;
    // Optional fresh ruin seed; null keeps the world-derived ruin. The overworld is unchanged.
    public int? RuinSeed { get; set; }
    readonly List<object> checkpoints=new();
    public Action<string>? Checkpoint;
    public bool UseDialogue { get; set; }
    void Mark(string name)
    {
        checkpoints.Add(new{name,tick=W.Tick,health=W.Player.Health,mana=W.Player.Mana,region=W.Player.Region});
        Checkpoint?.Invoke(name);
    }
    void Tick(V direction, bool combat=true)
    {
        if(++ticks>180000)throw new InvalidOperationException("Adventure driver timeout");
        var p=W.Player;
        if(!p.Alive)throw new InvalidOperationException($"Adventure player died tick={W.Tick} potions={potions} damage={damage}");
        if(p.Health<45&&W.UsePotion(p))potions++;
        if(p.Health<65&&W.Activate(p.Id,"mend",p.Id))spells++;
        W.PlayerMove(direction,1f/60);
        W.Step(1f/60);
    }
    ActorState? Threat() => W.Actors.Values.Where(a=>a.Alive&&a.Region==W.Player.Region&&W.Hostile(W.Player,a)&&V.Distance(a.Position,W.Player.Position)<155&&W.LineOfSight(W.Player,a))
        .OrderBy(a=>V.DistanceSquared(a.Position,W.Player.Position)).ThenBy(a=>a.Id,StringComparer.Ordinal).FirstOrDefault();
    IEnumerable<int> Fight(ActorState enemy)
    {
        Mark("encounter-"+enemy.Id);
        int timeout=0;
        while(enemy.Alive)
        {
            if(++timeout>12000)throw new InvalidOperationException("Adventure fight stuck "+enemy.Id);
            var p=W.Player;V offset=enemy.Position-p.Position;float distance=offset.Length();V direction=distance>1?V.Normalize(offset):V.UnitX;
            if(W.Activate(p.Id,"ember",enemy.Id))spells++;
            if(distance<65&&W.TryStartPlayerSlash(direction))attacks++;
            if(enemy.State==ActionState.Windup && distance<85 && W.TryStartPlayerDodge(new V(-direction.Y,direction.X)))dodges++;
            Tick(distance>38?direction:V.Zero);yield return ticks;
        }
        Mark("defeated-"+enemy.Id);
        // Loot stays with its production corpse, claimed via normal interaction when in range.
        if(V.Distance(W.Player.Position,enemy.Position)<48)W.Interact(W.Player);
    }
    public IEnumerable<int> Travel(int target)
    {
        var r=W.RegionsById[W.Player.Region];int timeout=0;var route=new Queue<int>();
        while(V.Distance(W.Player.Position,r.Center(target))>3)
        {
            if(++timeout>40000)throw new InvalidOperationException("Adventure route stuck "+target+" at "+W.Player.Position);
            var threat=Threat();if(threat!=null){foreach(int step in Fight(threat))yield return step;route.Clear();}
            if(route.Count==0)
            {
                route=new(r.Path(W.Player.Position,r.Center(target),W.Player));
                if(route.Count==0&&r.Index(W.Player.Position)!=target)throw new InvalidOperationException("Adventure route missing "+target);
                if(route.Count==0)route.Enqueue(target);
            }
            var delta=r.Center(route.Peek())-W.Player.Position;
            if(delta.Length()<3){route.Dequeue();continue;}
            Tick(V.Normalize(delta));yield return ticks;
            if(timeout%120==0)route.Clear(); // Knockback/dodge may invalidate the old route.
        }
        Tick(V.Zero);yield return ticks;
    }
    IEnumerable<int> Interact(int target)
    {
        foreach(int step in Travel(target))yield return step;
        for(int i=0;i<60&&W.Player.State is not (ActionState.Idle or ActionState.Moving);i++){Tick(V.Zero);yield return ticks;}
        if(!W.Interact(W.Player))throw new InvalidOperationException("Adventure interact failed: "+W.InteractionFeedback);
    }
    IEnumerable<int> Choose(string text)
    {
        N.Call("talk");
        for(int frame=0;frame<10;frame++)yield return ticks;
        Mark("warden-"+text.Split(' ')[0]);yield return ticks;
        var choices=N.Get("choices").AsGodotObject() as VBoxContainer;
        var choice=choices!.GetChildren().OfType<Button>().FirstOrDefault(b=>b.Text.StartsWith(text,StringComparison.Ordinal));
        if(choice==null||choice.Disabled)throw new InvalidOperationException("Missing dialogue choice: "+text);
        choice.EmitSignal(Button.SignalName.Pressed);
        for(int frame=0;frame<10;frame++)yield return ticks;
        foreach(var button in choices.GetChildren().OfType<Button>().Where(b=>b.Text=="Continue").ToArray())button.EmitSignal(Button.SignalName.Pressed);
        for(int frame=0;frame<10;frame++)yield return ticks;
    }
    // Every reachable lever and unsolved brazier ring, operated through production interaction.
    IEnumerable<int> Mechanisms(string phase)
    {
        var r=W.RegionsById["ruin"];bool progressed=true;
        while(progressed)
        {
            progressed=false;
            foreach(var c in DungeonMechanisms.Controls(r))
            {
                if(c.Gates.Length==0||c.Gates.All(r.OpenDoors.Contains))continue;
                int first=c.Kind=="lever"?c.Controllers[0]:DungeonMechanisms.Solution(r,c.Controllers).FirstOrDefault(-1);
                if(first<0||(r.Path(W.Player.Position,r.Center(first),W.Player).Count==0&&r.Index(W.Player.Position)!=first))continue;
                if(c.Kind=="lever"){foreach(int step in Interact(first))yield return step;levers++;Mark("lever-pulled-"+phase);}
                else
                {
                    Mark("brazier-ring-"+phase);
                    foreach(int brazier in DungeonMechanisms.Solution(r,c.Controllers)){foreach(int step in Interact(brazier))yield return step;braziers++;}
                    Mark("light-seal-broken-"+phase);
                }
                if(!c.Gates.All(r.OpenDoors.Contains))throw new InvalidOperationException("Mechanism did not open its gates: "+c.Kind);
                string save=Persistence.Capture(W,N);Persistence.Restore(W,N,save);r=W.RegionsById["ruin"];
                progressed=true;break; // Controls are rederived from the restored region.
            }
        }
    }
    public void Run() { foreach(int tick in Steps()) { } }
    public IEnumerable<int> Steps()
    {
        app.Reset();app.Ui.PauseForVerification();
        Mark("new-game-settlement");yield return ticks;
        void Observe(WorldEvent e){if(e.Kind=="damage"&&e.Target=="player")damage+=e.Count;}
        W.Events.Published+=Observe;
        try
        {
            if(UseDialogue) { foreach(int step in Choose("I can help"))yield return step; }
            else if(!N.Call("accept_quest",1).AsBool())throw new InvalidOperationException("Intro acceptance failed");
            foreach(var enemy in W.Actors.Values.Where(a=>a.Profile=="beast").Take(2).ToArray())
            {
                var r=W.RegionsById["reach"];foreach(int step in Travel(r.Index(enemy.Position)))yield return step;if(enemy.Alive)foreach(int step in Fight(enemy))yield return step;
            }
            if(!N.Call("completed",1).AsBool())throw new InvalidOperationException("Adventure prerequisite failed");
            if(UseDialogue)
            {
                var giver=W.Actors.Values.First(a=>a.Profession=="merchant");
                foreach(int step in Travel(W.RegionsById["reach"].Index(giver.Position)))yield return step;
                foreach(int step in Choose("Tell me about"))yield return step;
            }
            else if(!N.Call("accept_quest",2).AsBool())throw new InvalidOperationException("Adventure acceptance failed");
            Mark("assignment-accepted");
            if(RuinSeed is int ruinSeed)W.RegionsById["ruin"]=Mosslight.Regions.Dungeon(ruinSeed);
            var reach=W.RegionsById["reach"];foreach(int step in Interact(reach.Features.Single(f=>f.Value=="portal").Key))yield return step;
            var ruin=W.RegionsById["ruin"];Mark("dungeon-entrance");
            if(!DungeonValidator.TrySolve(ruin,out var gates))throw new InvalidOperationException("No mission witness");
            foreach(int gate in gates)
            {
                foreach(int step in Mechanisms("approach"))yield return step;ruin=W.RegionsById["ruin"];
                foreach(int key in ruin.Features.Where(f=>f.Value=="key").Select(f=>f.Key).Order())
                    if(!ruin.Depleted.Contains(key)&&ruin.Path(W.Player.Position,ruin.Center(key),W.Player).Count>0){foreach(int step in Interact(key))yield return step;Mark("key-acquired");}
                var group=ruin.GateAt(gate);int stand=group.SelectMany(ruin.Neighbours).Distinct().Order().First(at=>ruin.CanStand(ruin.Center(at),W.Player)&&ruin.Path(W.Player.Position,ruin.Center(at),W.Player).Count>0);
                foreach(int step in Travel(stand))yield return step;Mark("locked-gate");
                if(!W.Interact(W.Player)||!group.All(ruin.OpenDoors.Contains))throw new InvalidOperationException("Gate did not open");
                Mark("gate-unlocked");
                string save=Persistence.Capture(W,N);Persistence.Restore(W,N,save);
                ruin=W.RegionsById["ruin"]; // Restore is authoritative, keep fresh references.
            }
            foreach(int step in Mechanisms("approach"))yield return step;ruin=W.RegionsById["ruin"];
            // Discover an actually present optional room and collect its supported cache, if reachable.
            foreach(int chest in ruin.Features.Where(f=>f.Value=="chest").Select(f=>f.Key).Order())
                if(!ruin.Depleted.Contains(chest)&&ruin.Path(W.Player.Position,ruin.Center(chest),W.Player).Count>0){foreach(int step in Interact(chest))yield return step;Mark("exploration-supplies");break;}
            foreach(int step in Travel(ruin.Objective))yield return step;Mark("relic-sanctuary");foreach(int step in Interact(ruin.Objective))yield return step;Mark("relic-recovered");
            if(N.Call("completed",2).AsBool())throw new InvalidOperationException("Premature adventure completion");
            foreach(int step in Mechanisms("return"))yield return step; // A lever beyond the relic opens the way home.
            string recovered=Persistence.Capture(W,N);Persistence.Restore(W,N,recovered);ruin=W.RegionsById["ruin"];
            foreach(int step in Interact(ruin.Entrance))yield return step;Mark("returned-to-reach");
            var warden=W.Actors.Values.First(a=>a.Profession=="merchant");
            var r0=W.RegionsById["reach"];foreach(int step in Travel(r0.Index(warden.Position)))yield return step;
            if(UseDialogue){foreach(int step in Choose("I brought back"))yield return step;}
            else if(!N.Call("report_to_warden").AsBool())throw new InvalidOperationException("Warden report failed");
            if(!N.Call("completed",2).AsBool()||N.Call("has_pending").AsBool())throw new InvalidOperationException("Warden reward failed");
            Mark("quest-completed-rewarded");yield return ticks;
            int reward=W.Items.Count("player","potion"),rep=W.Player.Reputation.GetValueOrDefault("wardens");
            string final=Persistence.Capture(W,N);Persistence.Restore(W,N,final);N.Call("claim_pending");
            if(!N.Call("completed",2).AsBool()||reward!=W.Items.Count("player","potion")||rep!=W.Player.Reputation.GetValueOrDefault("wardens"))throw new InvalidOperationException("Completion reload duplicated reward");
            // Optional key gates may stay shut; anything reachable in the final state must have been operated.
            var final_ruin=W.RegionsById["ruin"];var finalReach=DungeonValidator.Reach(final_ruin);
            int expected=DungeonMechanisms.Controls(final_ruin).Count(c=>c.Gates.Length>0);
            if(DungeonMechanisms.Controls(final_ruin).Any(c=>c.Gates.Length>0&&c.Controllers.Any(finalReach.Contains)&&!c.Gates.All(final_ruin.OpenDoors.Contains)))throw new InvalidOperationException("Adventure left a reachable mechanism unopened");
            string file=RuinSeed is int seed?$"build/adventure/normal-health-ruin-{seed}.json":"build/adventure/normal-health.json";
            string setup=RuinSeed is int fixedRuin?$"Fresh production world seed 42 with fresh ruin seed {fixedRuin}; original starter equipment, 100 health, 60 mana, 3 potions; production APIs (not UI input)":"Fresh production seed 42; original starter equipment, 100 health, 60 mana, 3 potions; production APIs (not UI input)";
            Directory.CreateDirectory("build/adventure");File.WriteAllText(file,System.Text.Json.JsonSerializer.Serialize(new{setup,ticks,potions,attacks,spells,dodges,damage,levers,braziers,mechanisms=expected,health=W.Player.Health,checkpoints},Content.Json));
            GD.Print("NORMAL HEALTH ADVENTURE PASSED ticks="+ticks+" health="+W.Player.Health+" levers="+levers+" braziers="+braziers);
        }
        finally{W.Events.Published-=Observe;}
    }
}
