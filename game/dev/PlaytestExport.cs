using Godot;
using System.IO.Compression;
namespace Mosslight;
public static class PlaytestExport
{
    // FileAccess supports Android content:// URIs returned by the native Storage Access Framework.
    // System.IO does not. No broad storage permission or native plugin is required.
    public static void Show(Foundation app)
    {
        app.Trace.Dispose();
        if(!File.Exists(app.Trace.LastPath)){ app.Ui.Notify("Record a playtest first");return; }
        var zip=Path.ChangeExtension(app.Trace.LastPath,"zip");
        try
        {
            using(var stream=File.Create(zip))
            using(var archive=new ZipArchive(stream,ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(app.Trace.LastPath,"trace.jsonl");
                using var text=new StreamWriter(archive.CreateEntry("README.txt").Open());
                text.Write("Mosslight local playtest. Input/tick/frame/wallMs in trace.jsonl. No save, account, network or device identifiers. Pair with Android Screen recorder if video is needed. Source: "+BuildIdentity.Source);
            }
        }
        catch(Exception e){app.Ui.Notify("Could not package playtest: "+e.Message);return;}
        var dialog=new FileDialog {Access=FileDialog.AccessEnum.Filesystem, FileMode=FileDialog.FileModeEnum.SaveFile, UseNativeDialog=true, CurrentFile="Mosslight-playtest.zip"};
        dialog.AddFilter("*.zip","Playtest evidence","application/zip");
        app.AddChild(dialog);
        dialog.FileSelected+=path=>
        {
            try
            {
                using var destination=Godot.FileAccess.Open(path,Godot.FileAccess.ModeFlags.Write);
                if(destination==null)throw new IOException(Godot.FileAccess.GetOpenError().ToString());
                destination.StoreBuffer(File.ReadAllBytes(zip)); destination.Flush();
                if(destination.GetError()!=Error.Ok)throw new IOException(destination.GetError().ToString());
                app.Ui.Notify("Playtest exported — share it from Files / Downloads");
            }
            catch(Exception e){app.Ui.Notify("Export failed: "+e.Message);}
            finally {dialog.QueueFree();}
        };
        dialog.Canceled+=()=>dialog.QueueFree();
        dialog.PopupCentered(new Vector2I(900,600));
    }
}
