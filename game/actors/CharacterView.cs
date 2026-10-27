using Godot;
using PixelCreatures.Runtime;
using PixelCreatures.Core;

namespace Mosslight;
/// <summary>Presentation consumes actor state; the PPC genome never owns health, quests or inventory.</summary>
public partial class CharacterView : Node2D
{
    public ActorState State { get; private set; } = null !;
    public CreatureActor Creature { get; private set; } = null !;

    public bool HasLoot { get; set; }
    Vector2 previous;
    public void Setup(ActorState actor)
    {
        State = actor;
        Creature = new CreatureActor
        {
            FamilyId = "",
            AutoMove = false,
            UseMeasuredExternalVelocity = true,
            SyncNodePosition = false,
            AnimationFps = 10,
            Quality = 1,
            CullOffscreen = true
        };
        AddChild(Creature);
        var result = actor.Appearance.GenomeJson == "" ? null : CreatureRuntime.Serializer.Deserialize(actor.Appearance.GenomeJson);
        if (result != null && !result.Ok)
            throw new InvalidDataException(result.Describe());
        var genome = result?.Genome ?? CreatureRuntime.Sample(actor.Appearance.Family, actor.Appearance.Seed);
        Creature.SetGenome(genome, async: false);
        float body = (float)Creature.Model!.Anatomy.Metrics.BodyLength;
        Creature.Scale = Vector2.One * Math.Clamp(actor.Radius / Math.Max(1, body * .2f), .25f, 1);
        previous = new(actor.X, actor.Y);
        Sync(1f / 60);
    }

    public void SnapPosition() { previous=new Vector2(State.X,State.Y); Creature.Teleport(previous); Creature.MoveVelocity=Vector2.Zero; }

    public void Equipment(string json)
    {
        if (State.Appearance.Family != "biped")
            return;
        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        var genome = CreatureRuntime.Serializer.Deserialize(State.Appearance.GenomeJson).Genome!;
        genome = genome.WithChoice("gear.weapon", data["weapon"]).WithChoice("gear.outfit", data["outfit"]);
        Creature.SetGenome(genome, async: false);
    }

    public void Sync(float dt)
    {
        var now = new Vector2(State.X, State.Y);
        Creature.MoveVelocity = (now - previous) / Math.Max(dt, .001f);
        Creature.GroundPosition = now;
        previous = now;
        Position = GodotConvert.GroundToScreen(State.X, State.Y);
        Creature.Resting = State.Activity == "rest" || !State.Alive;
        Modulate = State.Alive ? Colors.White : new Color(.45f, .4f, .4f, .6f);
        QueueRedraw();
    }

    public void Cue(string cue)
    {
        if (cue == "attack" || cue == "cast")
            Creature.TriggerAction();
        if (cue == "hit")
            Creature.TriggerHit(Vector2.Left);
    }

    public override void _Draw()
    {
        if (!State.Alive)
        {
            if (HasLoot)
            {
                DrawRect(new Rect2(-7,-15,14,12),new Color("b49364"));
                DrawRect(new Rect2(-4,-18,8,4),new Color("d5bd85"));
                DrawLine(new Vector2(-5,-12),new Vector2(5,-12),new Color("5b4936"),2);
            }
            return;
        }
        if (State.Health < State.MaxHealth)
        {
            DrawRect(new Rect2(-14, -50, 28, 3), new Color(.15f, .1f, .12f));
            DrawRect(new Rect2(-14, -50, 28 * State.Health / State.MaxHealth, 3), new Color(.76f, .29f, .22f));
        }

        if (State.Statuses.Count > 0)
            DrawCircle(new Vector2(0, -54), 2, new Color(1, .6f, .2f));
    }
}
