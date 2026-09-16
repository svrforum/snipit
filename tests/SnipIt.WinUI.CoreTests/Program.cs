using System.Drawing;
using SnipIt.Services;
using System.Diagnostics;
static void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
using var original=new Bitmap(100,80);using(var graphics=Graphics.FromImage(original))graphics.Clear(Color.White);original.SetPixel(0,0,Color.Blue);
using var document=new EditorDocument((Bitmap)original.Clone());
using(var result=document.Render())Check(result.GetPixel(0,0).ToArgb()==Color.Blue.ToArgb()&&result.Size==original.Size,"Unedited source pixels preserved");
document.Add(new("Rectangle",new[]{new PointF(10,10),new PointF(60,50)},Color.Red,4));
using(var result=document.Render())Check(result.GetPixel(10,20).R>200&&result.GetPixel(10,20).G<20,"Rectangle coordinates and stroke");
document.Undo();using(var result=document.Render())Check(result.GetPixel(10,20).ToArgb()==Color.White.ToArgb(),"Undo restores original pixels");document.Redo();
document.Add(new("Crop",new[]{new PointF(5,5),new PointF(75,65)},Color.Red,3));
using(var result=document.Render())Check(result.Width==70&&result.Height==60&&result.GetPixel(5,15).R>200,"Crop transforms previous annotations");document.Undo();document.Undo();Check(!document.CanUndo&&document.CanRedo,"Undo across crop and drawing");
foreach(var tool in new[]{"Pen","Arrow","Line","Ellipse","Highlight","Blur","SoftBlur","Text"}){document.Add(new(tool,new[]{new PointF(5,5),new PointF(40,30),new PointF(65,60)},Color.Red,3,"Text"));using var result=document.Render();Check(result.Size==original.Size,tool+" renders at original resolution");document.Undo();}
using var large=new Bitmap(3840,2160);using var stress=new EditorDocument((Bitmap)large.Clone());var allocated=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++)stress.Add(new("Line",new[]{new PointF(1,i),new PointF(2000,i)},Color.Blue,2));Check(GC.GetAllocatedBytesForCurrentThread()-allocated<1024*1024,"100 vector undo entries allocate less than 1 MiB managed memory");var watch=Stopwatch.StartNew();using(var result=stress.Render())Check(result.Width==3840&&result.Height==2160,"100 edits preserve 4K output");Console.WriteLine($"4K 100-edit render: {watch.ElapsedMilliseconds}ms");
using(var revisions=new EditorDocument((Bitmap)original.Clone()))
{
 Check(revisions.Revision==0,"Initial document revision is clean");
 var mark=new EditMark("Line",new[]{new PointF(5,5),new PointF(50,5)},Color.Blue,2);
 revisions.Add(mark);var saved=revisions.Revision;
 revisions.Undo();Check(revisions.Revision==0,"Undo restores initial revision");
 revisions.Redo();Check(revisions.Revision==saved,"Redo restores saved revision");
 revisions.Undo();revisions.Add(mark);Check(revisions.Revision!=saved&&!revisions.CanRedo,"Branched edit does not reuse saved revision");
 for(int i=0;i<200;i++)revisions.Add(mark);
 using var expected=revisions.Render();var checkpointRevision=revisions.Revision;
 revisions.Undo();revisions.Redo();
 using var actual=revisions.Render();
 Check(revisions.Revision==checkpointRevision&&actual.GetPixel(10,5)==expected.GetPixel(10,5),"Undo checkpoint preserves pixels and revision");
 int undoCount=0;while(revisions.CanUndo){revisions.Undo();undoCount++;}
 Check(undoCount==101&&revisions.Revision>0,"Compaction retains bounded undo history and checkpoint revision");
}

Console.WriteLine("All WinUI editor core tests passed.");
using (var paper = new Bitmap(120, 100))
{
 using(var g=Graphics.FromImage(paper))g.Clear(Color.White);
 using var marker = new EditorDocument((Bitmap)paper.Clone());
 marker.Add(new("Highlight",new[]{new PointF(10,30),new PointF(100,30),new PointF(10,30)},Color.Yellow,20));
 using var marked=marker.Render();
 Check(marked.GetPixel(50,30).B is >=120 and <=135,"Highlighter overlap within one stroke keeps uniform opacity");
 Check(marked.GetPixel(50,60).ToArgb()==Color.White.ToArgb(),"Highlighter follows stroke instead of filling a rectangle");
 marker.Undo();using var undone=marker.Render();Check(undone.GetPixel(50,30).ToArgb()==Color.White.ToArgb(),"Highlighter undo restores original");
}
var near=SnipIt.Utils.HighlighterStroke.Align(new[]{new PointF(10,20),new PointF(100,23)},false);
Check(near[1].Y==20,"Highlighter gently snaps nearly horizontal strokes");
var free=SnipIt.Utils.HighlighterStroke.Align(new[]{new PointF(10,20),new PointF(100,60)},false);
Check(free[1].Y==60,"Highlighter preserves deliberate freehand strokes");
Check(SnipIt.Utils.HighlighterStroke.Align(free,true)[1].Y==20,"Shift locks highlighter horizontally");
