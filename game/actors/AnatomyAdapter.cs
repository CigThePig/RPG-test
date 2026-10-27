using PixelCreatures.Runtime;

namespace Mosslight;
/// <summary>Derive gameplay capabilities in the model layer, even for actors never presented.</summary>
public static class AnatomyAdapter
{
    public static void Apply(ActorState actor)
    {
        bool fresh = actor.Appearance.GenomeJson == "";
        var loaded = fresh ? null : CreatureRuntime.Serializer.Deserialize(actor.Appearance.GenomeJson);
        if (loaded != null && !loaded.Ok)
            throw new InvalidDataException(loaded.Describe());
        var genome = loaded?.Genome ?? CreatureRuntime.Sample(actor.Appearance.Family, actor.Appearance.Seed);
        var model = CreatureRuntime.Build(genome);
        actor.Appearance.GenomeJson = CreatureRuntime.Serializer.Serialize(genome);
        actor.Flies = model.Anatomy.Rig.Wings.Count > 0;
        actor.Swims = model.Anatomy.Rig.Fins.Count > 0;
        actor.Grasps = model.Anatomy.Rig.Arms.Count > 0 || model.Anatomy.Rig.Grippers.Count > 0;
        if (fresh)
            actor.Speed = Math.Clamp((float)model.Anatomy.Metrics.WalkSpeed, 40, 100);
        actor.NaturalReach = Math.Clamp((float)model.Anatomy.Metrics.BodyLength * .1f, 0, 12);
        actor.Radius = Math.Clamp((float)model.Anatomy.Metrics.BodyLength * .2f, 6, 11);
    }
}
