using System.Diagnostics;
using System.Text.Json;
using PixelCreatures.Core;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Export;
using PixelCreatures.Workshop;
var watch=Stopwatch.StartNew();
var output=Path.GetFullPath(args.Length>0?args[0]:"public/art"); Directory.CreateDirectory(output);
var reg=CreatureRegistry.CreateDefault(); var factory=new CreatureFactory(reg);
var manifest=new Dictionary<string,object>();
foreach(var (id,seed) in new[]{("biped",42UL),("quadruped",17UL),("quadruped",1337UL),("winged",91UL),("arthropod",61UL)}) {
 var family=reg.GetFamily(id); var genome=GenomeFactory.Sample(family,seed);
 var model=factory.Build(genome); var key=$"{id}-{seed}";
 var sheet=SpritesheetExporter.Export(model,reg,new SpritesheetOptions{Clips=new(){ClipKind.Idle,ClipKind.Walk},Fps=6,Quality=1},key);
 for(int i=0;i<sheet.Pages.Count;i++){var p=sheet.Pages[i]; File.WriteAllBytes(Path.Combine(output,sheet.Pages.Count==1?$"{key}.png":$"{key}_{i}.png"),PngEncoder.Encode(p.rgba,p.width,p.height));}
 File.WriteAllText(Path.Combine(output,key+".json"),sheet.MetadataJson);
 // Semantic anatomy bridge, not inference from pixels.
 manifest[key]=new {family=id,seed, wings=model.Anatomy.Rig.Wings.Count, legs=model.Anatomy.Rig.Legs.Count, flies=model.Anatomy.Rig.Wings.Count>0, speed=Math.Clamp(model.Anatomy.Metrics.WalkSpeed,30,85), anatomyKey=model.Anatomy.AnatomyKey.ToString("x16")};
 Console.WriteLine($"{key}: {sheet.TotalFrames} frames");
}
void Save(string key,Godot.Image image,int x=0,int y=0){File.WriteAllBytes(Path.Combine(output,key+".png"),PngEncoder.Encode(image.Data,image.Width,image.Height)); manifest[key]=new{width=image.Width,height=image.Height,x,y};}
foreach(var kind in Enum.GetValues<GroundKind>()) Save(kind.ToString().ToLower(),EnvironmentArt.GroundTile(kind,64,42));
for(ulong s=1;s<=5;s++) {Save($"tree{s}",EnvironmentArt.Tree(s,out var a,1.65),a.X,a.Y);Save($"rock{s}",EnvironmentArt.Rock(s,out var b),b.X,b.Y);Save($"bush{s}",EnvironmentArt.Bush(s,out var c,1.25),c.X,c.Y);}
File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(manifest));
File.WriteAllText(Path.Combine(output,"..","..","src","anatomy.json"),JsonSerializer.Serialize(manifest.Where(e=>e.Key.Contains("-")).ToDictionary(e=>e.Key,e=>e.Value)));
Console.WriteLine($"Generated in {watch.Elapsed.TotalSeconds:F2}s");
