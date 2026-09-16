using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using SnipIt.Utils;
namespace SnipIt.Services;
public sealed class CaptureHistoryItem
{
 public string Id {get;set;}=""; public DateTime CapturedAt {get;set;} public int Width {get;set;} public int Height {get;set;}
 public string ImagePath {get;set;}=""; public string ThumbnailPath {get;set;}="";
 public string Label=>$"{CapturedAt:MM/dd HH:mm} · {Width}×{Height}";
}
internal sealed class HistoryStore
{
 public static HistoryStore Instance {get;}=new();
 private readonly SemaphoreSlim gate=new(1,1);
 private readonly string folder=Path.Combine(AppDataPaths.GetFolder(Environment.SpecialFolder.LocalApplicationData),"History");
 private List<CaptureHistoryItem> items=new();
 public event Action? Changed;
 public IReadOnlyList<CaptureHistoryItem> Items {get { lock(items) return items.ToArray(); }}
 private HistoryStore(){Directory.CreateDirectory(folder);try{items=JsonSerializer.Deserialize<List<CaptureHistoryItem>>(File.ReadAllText(Path.Combine(folder,"index.json")))?.Where(x=>File.Exists(x.ImagePath)).ToList()??new();}catch(IOException){}catch(JsonException){}}
 private void Index(){var file=Path.Combine(folder,"index.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(items));File.Move(file+".tmp",file,true);}
 public async Task<CaptureHistoryItem> Add(Bitmap bitmap)
 {
  CaptureHistoryItem added=null!;using var owned=(Bitmap)bitmap.Clone();await gate.WaitAsync();
  try{await Task.Run(()=>{var id=Guid.NewGuid().ToString("N");var item=new CaptureHistoryItem{Id=id,CapturedAt=DateTime.Now,Width=owned.Width,Height=owned.Height,ImagePath=Path.Combine(folder,id+".png"),ThumbnailPath=Path.Combine(folder,id+"_thumb.png")};added=item;owned.Save(item.ImagePath,ImageFormat.Png);using(var thumb=ImageProcessingHelper.CreateThumbnail(owned,480,300))thumb.Save(item.ThumbnailPath,ImageFormat.Png);lock(items){items.Insert(0,item);while(items.Count>100){DeleteFiles(items[^1]);items.RemoveAt(items.Count-1);}Index();}});}finally{gate.Release();}App.Queue.TryEnqueue(()=>Changed?.Invoke());return added;
 }
 public async Task<CaptureHistoryItem> Update(CaptureHistoryItem previous, Bitmap bitmap)
 {
  using var owned=(Bitmap)bitmap.Clone();await gate.WaitAsync();
  CaptureHistoryItem updated=null!;
  try
  {
   await Task.Run(()=>
   {
    var name=Guid.NewGuid().ToString("N");
    updated=new CaptureHistoryItem{Id=previous.Id,CapturedAt=previous.CapturedAt,Width=owned.Width,Height=owned.Height,ImagePath=Path.Combine(folder,name+".png"),ThumbnailPath=Path.Combine(folder,name+"_thumb.png")};
    try
    {
     owned.Save(updated.ImagePath,ImageFormat.Png);
     using(var thumb=ImageProcessingHelper.CreateThumbnail(owned,480,300))thumb.Save(updated.ThumbnailPath,ImageFormat.Png);
     lock(items)
     {
      var index=items.FindIndex(x=>x.Id==previous.Id);
      var old=index>=0?items[index]:null;
      if(index>=0)items[index]=updated;else items.Insert(0,updated);
      try{Index();}catch{if(index>=0)items[index]=old!;else items.Remove(updated);throw;}
     }
    }
    catch{DeleteFiles(updated);throw;}
    try{DeleteFiles(previous);}catch(IOException){}catch(UnauthorizedAccessException){}
   });
  }
  finally{gate.Release();}
  App.Queue.TryEnqueue(()=>Changed?.Invoke());return updated;
 }
 public async Task Delete(CaptureHistoryItem? item)
 {
  await gate.WaitAsync();try{await Task.Run(()=>{lock(items){foreach(var entry in item==null?items.ToArray():new[]{item}){DeleteFiles(entry);items.Remove(entry);}Index();}});}finally{gate.Release();}Changed?.Invoke();
 }
 private static void DeleteFiles(CaptureHistoryItem item){File.Delete(item.ImagePath);File.Delete(item.ThumbnailPath);}
 public static async Task<Bitmap> Load(CaptureHistoryItem item)=>await Task.Run(()=>{using var source=new Bitmap(item.ImagePath);return source.Clone(new Rectangle(0,0,source.Width,source.Height),PixelFormat.Format32bppArgb);});
}

