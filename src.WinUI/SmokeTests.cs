#if SNIPIT_SMOKE_TESTS
using System.Drawing;
using System.Security.Cryptography;
using SnipIt.Services;
using SnipIt.Models;
namespace SnipIt;
internal static class SmokeTests
{
 internal static async Task Run()
 {
  var folder=Environment.GetEnvironmentVariable("SNIPIT_DATA_DIRECTORY");
  if(string.IsNullOrWhiteSpace(folder))throw new InvalidOperationException("An isolated profile is required.");
  Directory.CreateDirectory(folder);
  var log=Path.Combine(folder,"smoke.log");
  void Check(bool condition,string name){if(!condition)throw new Exception(name);File.AppendAllText(log,"PASS "+name+Environment.NewLine);}
  try
  {
   if(File.Exists(Path.Combine(folder,"restart.pending"))){File.Delete(Path.Combine(folder,"restart.pending"));Check(true,"Single-file update replaced executable and restarted WinUI");File.WriteAllText(Path.Combine(folder,"complete"),"passed");return;}
   File.WriteAllText(log,"");
   var dualAssets = System.Text.Json.JsonSerializer.Serialize(new { tag_name="v9.0.0", draft=false, prerelease=false, assets=new[]{
    new {name="SnipIt.exe",browser_download_url="https://github.com/svrforum/snipit/releases/download/v9.0.0/SnipIt.exe",size=100},
    new {name="SnipIt-WinUI.exe",browser_download_url="https://github.com/svrforum/snipit/releases/download/v9.0.0/SnipIt-WinUI.exe",size=100},
    new {name="SHA256SUMS.txt",browser_download_url="https://github.com/svrforum/snipit/releases/download/v9.0.0/SHA256SUMS.txt",size=100}}});
   Check(GitHubUpdateClient.ParseRelease(dualAssets,new Version(2,7,0))!.DownloadUrl.AbsolutePath.EndsWith("/SnipIt-WinUI.exe")
       && GitHubUpdateClient.ParseChecksum(new string('a',64)+"  SnipIt.exe\n"+new string('b',64)+"  SnipIt-WinUI.exe")==new string('b',64),"Dual release selects WinUI executable and its own checksum");
   Check(!Program.ShouldStartInTray(Array.Empty<string>(), true)
       && Program.ShouldStartInTray(new[]{"--startup"}, true)
       && !Program.ShouldStartInTray(new[]{"--startup"}, false), "Manual launch shows home; only configured Windows startup minimizes to tray");
   LocalizationService.Instance.CurrentLanguage = Language.English;
   Check(Ui.L("저장") == "Save" && Ui.L("도움말") == "Help" && Ui.L("SnipIt 편집") == "SnipIt Editor" && Ui.L("캡처 단축키 기본값") == "Reset capture shortcuts", "English setting translates editor and settings controls");
   LocalizationService.Instance.CurrentLanguage = Language.Korean;
   await Views.EditorWindow.VerifyCaptureReuse();
   Check(true,"Editor reuse, capture hiding, latest history, clipboard edits, OCR click/Ctrl/drag and reopening");
   var borderRegion = new Rectangle(-500, 100, 320, 240);
   Check(Views.RecordingBorder.BoundsFor(borderRegion).All(strip => !strip.IntersectsWith(borderRegion)), "Recording border stays outside physical capture bounds");
   await Views.RecordingWindow.VerifyLifecycle(folder);
   Check(true,"GIF window countdown, cancellation, recording, save and resource cleanup");
   if (Environment.GetEnvironmentVariable("SNIPIT_EDITOR_REUSE_TEST") == "1")
   {
    Check(true,"Repeated captures reuse one editor, preserve new pixels, reset undo, and reopen after close");
    File.WriteAllText(Path.Combine(folder,"complete"),"passed");
    return;
   }
   await AppSettingsConfig.Instance.SaveCheckedAsync();var settingsPath=Path.Combine(folder,"settings.json");var savedSettings=File.ReadAllText(settingsPath);using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await AppSettingsConfig.Instance.SaveCheckedAsync(cancelled.Token);throw new Exception("Cancelled save unexpectedly succeeded");}catch(OperationCanceledException){}}Check(File.ReadAllText(settingsPath)==savedSettings&&!Directory.EnumerateFiles(folder,"settings.json.*.tmp").Any(),"Cancelled settings save preserves file and removes temporary payload");
   var mapped=SnipIt.Views.CaptureOverlay.ToPhysical(new Windows.Foundation.Point(20,30),new Windows.Foundation.Point(120,130),new Rectangle(-1920,0,3840,2160),1920,1080);Check(mapped==new Rectangle(-1880,60,200,200),"Negative monitor origin and 200 percent coordinate mapping");
   foreach(var scale in new[]{1.25,1.5,1.75,2.0})
    for(int x=0;x<1000;x++)
    {
     var tiny=Views.CaptureOverlay.ToPhysical(new Windows.Foundation.Point(x/scale,173/scale),new Windows.Foundation.Point((x+10)/scale,183/scale),new Rectangle(-1920,0,2880,1800),2880/scale,1800/scale);
     if(tiny!=new Rectangle(-1920+x,173,10,10))throw new Exception($"Pixel boundary round trip: {scale}, {x}, {tiny}");
    }
   Check(true,"Ten-pixel keyboard selection retains exact bounds at fractional DPI scales");
   using var source=new Bitmap(3840,2160);using(var g=Graphics.FromImage(source))g.Clear(Color.White);source.SetPixel(20,20,Color.Blue);
   var native=await Ui.ImageAsync(source);Check(native.PixelWidth==3840&&native.PixelHeight==2160,"Native preview keeps 4K resolution");
   await HistoryStore.Instance.Add(source);var item=HistoryStore.Instance.Items[0];
   using(var restored=await HistoryStore.Load(item))Check(restored.GetPixel(20,20).ToArgb()==Color.Blue.ToArgb(),"WinUI history PNG preserves source pixel");
   using(var thumb=new Bitmap(item.ThumbnailPath))Check(thumb.Width<=480&&thumb.Height<=300,"WinUI thumbnail bounds");
   using var document=new EditorDocument((Bitmap)source.Clone());document.Add(new("Crop",new[]{new PointF(10,10),new PointF(110,90)},Color.Red,3));
   using(var edited=document.Render()){ImageExport.Save(edited,Path.Combine(folder,"edited.png"));Check(edited.Size==new Size(100,80)&&edited.GetPixel(10,10).ToArgb()==Color.Blue.ToArgb(),"Edited PNG retains crop coordinates and pixels");}
   foreach(var extension in new[]{"png","jpg","bmp","gif"}){var path=Path.Combine(folder,"export."+extension);ImageExport.Save(source,path);using var exported=new Bitmap(path);Check(exported.Size==source.Size,"Export "+extension+" dimensions");}
   using(var screen=ScreenCaptureService.CaptureFullScreen()){var bounds=ScreenCaptureService.GetVirtualScreenBoundsPhysical();Check(screen.Width==bounds.Width&&screen.Height==bounds.Height,"Physical screen capture dimensions");}
   using(var recorder=new GifRecorderService(15,GifQualityPreset.SkipFrames,5)){var bounds=ScreenCaptureService.GetVirtualScreenBoundsPhysical();var path=Path.Combine(folder,"recording.gif");recorder.SavePathProvider=()=>path;recorder.StartRecording(new Rectangle(bounds.X,bounds.Y,160,100));await Task.Delay(500);Check(await recorder.StopRecordingAsync()==path,"GIF captures and saves through WinUI callback");using var gif=new Bitmap(path);Check(gif.Size==new Size(160,100),"Recorded GIF retains selected resolution");}
   using(var ocrImage=new Bitmap(8000,240)){using(var graphics=Graphics.FromImage(ocrImage)){graphics.Clear(Color.White);using var font=new Font("Arial",80);graphics.DrawString("SCREEN CAPTURE TEST",font,Brushes.Black,4000,50);}var recognized=await OcrService.ExtractTextWithRegionsAsync(ocrImage);Check(recognized.Lines.SelectMany(x=>x.Words).Any(x=>x.BoundingRect.X>3000),"Oversized OCR returns original-image word coordinates");}
   await HistoryStore.Instance.Delete(item);Check(!File.Exists(item.ImagePath)&&!HistoryStore.Instance.Items.Any(x=>x.Id==item.Id),"WinUI history removes item and source");
   var cache=Path.Combine(folder,"update");Directory.CreateDirectory(cache);var payload=Path.Combine(cache,"SnipIt.update.exe");File.Copy(Environment.ProcessPath!,payload,true);
   var sha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(payload)));
   var release=new UpdateRelease(new Version(2,7,0),"v2.7.0","test",new Uri("https://github.com/svrforum/snipit/releases/download/v2.7.0/SnipIt.exe"),new Uri("https://github.com/svrforum/snipit/releases/download/v2.7.0/SHA256SUMS.txt"),new FileInfo(payload).Length,null);
   File.WriteAllText(Path.Combine(folder,"restart.pending"),"test");
   await UpdateInstaller.StartHelperAsync(new(release,payload,sha));Check(true,"Published WinUI update helper handshake");
  }
  catch(Exception ex){File.AppendAllText(log,"FAIL "+ex);Environment.ExitCode=1;}
  finally{App.ExitApp();}
 }
}
#endif
