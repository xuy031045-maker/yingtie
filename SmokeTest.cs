using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace YingTie
{
    static class SelfTest
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        static void Mouse(PinWindow pin,string method,MouseEventArgs args)
        { typeof(PinWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pin,new object[] { args }); }
        static void PaintCapture(CaptureOverlay overlay,Bitmap target,Rectangle clip)
        {
            using(var g=Graphics.FromImage(target))
            { g.SetClip(clip); typeof(CaptureOverlay).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(overlay,new object[] { new PaintEventArgs(g,clip) }); }
        }
        static void CheckCaptureTrail(CaptureOverlay overlay)
        {
            Rectangle dirty=Rectangle.Empty;
            InvalidateEventHandler changed=delegate(object sender,InvalidateEventArgs e) { dirty=dirty.IsEmpty ? e.InvalidRect : Rectangle.Union(dirty,e.InvalidRect); };
            overlay.Invalidated+=changed;
            try
            {
                using(var incremental=new Bitmap(overlay.Width,overlay.Height))
                using(var full=new Bitmap(overlay.Width,overlay.Height))
                {
                    PaintCapture(overlay,incremental,overlay.ClientRectangle);
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    // Hover movement, selection growing/shrinking across its anchor,
                    // and a tiny cancelled selection must all match a fresh frame.
                    for(int phase=0;phase<3;phase++)
                    foreach(var p in new[] { new Point(260,240),new Point(480,170),new Point(90,410),new Point(939,520),new Point(50,40),new Point(430,270) })
                    {
                        dirty=Rectangle.Empty;
                        if(phase==1 && p==new Point(260,240)) typeof(CaptureOverlay).GetMethod("OnMouseDown",flags).Invoke(overlay,new object[] { new MouseEventArgs(MouseButtons.Left,1,420,260,0) });
                        if(phase==2 && p==new Point(260,240)) typeof(CaptureOverlay).GetMethod("OnMouseUp",flags).Invoke(overlay,new object[] { new MouseEventArgs(MouseButtons.Left,1,421,261,0) });
                        typeof(CaptureOverlay).GetMethod("OnMouseMove",flags).Invoke(overlay,new object[] { new MouseEventArgs(MouseButtons.None,0,p.X,p.Y,0) });
                        Check(!dirty.IsEmpty,"Pointer motion must invalidate its visuals");
                        PaintCapture(overlay,incremental,Rectangle.Intersect(dirty,overlay.ClientRectangle));
                        PaintCapture(overlay,full,overlay.ClientRectangle);
                        var a=incremental.LockBits(overlay.ClientRectangle,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                        var b=full.LockBits(overlay.ClientRectangle,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                        try
                        {
                            var left=new byte[a.Stride*a.Height]; var right=new byte[b.Stride*b.Height];
                            System.Runtime.InteropServices.Marshal.Copy(a.Scan0,left,0,left.Length); System.Runtime.InteropServices.Marshal.Copy(b.Scan0,right,0,right.Length);
                            for(int i=0;i<left.Length;i++) if(left[i]!=right[i]) throw new Exception("Capture repaint left stale pixels near "+(i/4%a.Width)+","+(i/a.Stride)+" after pointer "+p);
                        }
                        finally { incremental.UnlockBits(a); full.UnlockBits(b); }
                    }
                }
            }
            finally { overlay.Invalidated-=changed; }
        }
        public static int Run()
        {
            string output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification-minimal"); Directory.CreateDirectory(output);
            try
            {
                using(var fixture=new Bitmap(480,280))
                {
                    using(var g=Graphics.FromImage(fixture))
                    using(var title=new Font("Microsoft YaHei UI",25,FontStyle.Bold,GraphicsUnit.Pixel))
                    using(var body=Theme.Font(11))
                    {
                        g.Clear(Color.FromArgb(245,248,252)); g.FillRectangle(Brushes.SteelBlue,0,0,12,280);
                        using(var ink=new SolidBrush(Theme.Ink))
                        {
                            g.DrawString("一张临时贴图",title,ink,34,48);
                            g.DrawString("拖动移动，双击关闭。",body,ink,36,112);
                            g.DrawString("需要留存时，再点击保存。",body,ink,36,145);
                        }
                    }
                    fixture.Save(Path.Combine(output,"fixture.png"),ImageFormat.Png);
                    using(var home=new HomeWindow(true))
                    {
                        home.Show(); Check(!home.Visible && home.IsHandleCreated,"Background controller must stay hidden but own a message handle");
                        using(var pin=new PinWindow(home,new Bitmap(fixture),new Point(100,100)))
                        {
                            pin.Show(); Application.DoEvents();
                            Check(pin.FormBorderStyle==FormBorderStyle.None && !pin.ShowInTaskbar,"Pin should have no frame or taskbar tile");
                            Check(pin.TopMost && pin.ClientSize==fixture.Size,"Pin must be topmost without title or padding");
                            using(var shot=new Bitmap(pin.Width,pin.Height))
                            {
                                pin.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));
                                Check(shot.GetPixel(0,0).ToArgb()==fixture.GetPixel(0,0).ToArgb(),"Image corner must not contain chrome");
                                Check(shot.GetPixel(300,200).ToArgb()==fixture.GetPixel(300,200).ToArgb(),"Image pixels must align");
                                shot.Save(Path.Combine(output,"pin.png"),ImageFormat.Png);
                            }
                            pin.ShowActions(); Application.DoEvents(); Check(pin.Actions.Visible,"Hover controls must be showable");
                            Check(pin.Actions.ClientSize.Height<=44 && pin.Actions.ClientSize.Width<=390,"Default toolbar should fit on one compact row");
                            var area=Screen.FromRectangle(pin.Bounds).WorkingArea;
                            Check(area.Contains(pin.Actions.Bounds),"Toolbar must fit within screen working area");
                            if(pin.Bottom+4+pin.Actions.Height<=area.Bottom) Check(pin.Actions.Top>=pin.Bottom,"Toolbar should prefer below the image");
                            using(var shot=new Bitmap(pin.Actions.Width,pin.Actions.Height)) { pin.Actions.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size)); shot.Save(Path.Combine(output,"toolbar.png"),ImageFormat.Png); }
                            pin.HideActions(); Check(!pin.Actions.Visible,"Hover controls must hide without closing image");
                            pin.SetTransparency(65); Check(Math.Abs(pin.Opacity-.65)<.01,"Transparency failed"); pin.SetTransparency(100);
                            Mouse(pin,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,30,30,0));
                            Mouse(pin,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,80,60,0));
                            Mouse(pin,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,80,60,0));
                            Check(pin.Location==new Point(150,130),"Client mouse drag must move image by pointer delta");
                            Check(pin.Visible,"Single drag must not close image");
                            pin.SetTool(MarkTool.Rectangle); pin.MarkColor=Color.Blue;
                            Check(pin.Actions.Height==84,"Annotation tools should expand only while editing");
                            Mouse(pin,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,40,130,0));
                            Mouse(pin,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,160,210,0));
                            Mouse(pin,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,160,210,0));
                            Check(pin.Location==new Point(150,130),"Drawing must not move the pin");
                            pin.SetTool(MarkTool.Ellipse); pin.MarkColor=Color.Green;
                            Mouse(pin,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,220,160,0));
                            Mouse(pin,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,300,240,0));
                            Mouse(pin,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,300,240,0));
                            pin.SetTool(MarkTool.Arrow); pin.MarkColor=Color.Red;
                            Mouse(pin,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,340,180,0));
                            Mouse(pin,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,420,230,0));
                            Mouse(pin,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,420,230,0));
                            Check(pin.MarkCount==3,"Three annotation tools must commit");
                            using(var annotated=pin.RenderImage())
                            {
                                Check(annotated.Size==fixture.Size,"Annotation export must preserve original dimensions");
                                Check(annotated.GetPixel(40,170).B>240 && annotated.GetPixel(40,170).R<10,"Blue rectangle missing from export");
                                Check(annotated.GetPixel(260,160).G>100 && annotated.GetPixel(260,160).R<10,"Green ellipse missing from export");
                                Check(annotated.GetPixel(420,230).R>240 && annotated.GetPixel(420,230).G<10,"Red arrow missing from export");
                                annotated.Save(Path.Combine(output,"annotations.png"),ImageFormat.Png);
                            }
                            pin.ShowActions(); Application.DoEvents();
                            using(var shot=new Bitmap(pin.Actions.Width,pin.Actions.Height)) { pin.Actions.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size)); shot.Save(Path.Combine(output,"annotation-toolbar.png"),ImageFormat.Png); }
                            pin.UndoMark(); Check(pin.MarkCount==2,"Undo must remove last annotation");
                            using(var undone=pin.RenderImage()) Check(undone.GetPixel(420,230).ToArgb()==fixture.GetPixel(420,230).ToArgb(),"Undo must restore pixels in export");
                            pin.SetTool(MarkTool.Move); Check(pin.Actions.Height==42,"Done should collapse annotation row");
                            string[] before=Directory.GetFiles(output); int forms=Application.OpenForms.Count;
                            Mouse(pin,"OnMouseDoubleClick",new MouseEventArgs(MouseButtons.Left,2,40,40,0));
                            Check(pin.IsDisposed && pin.Actions.IsDisposed,"Double-click must close image and toolbar");
                            Check(Application.OpenForms.Count<forms,"Double-click must remove the pin window");
                            Check(Directory.GetFiles(output).Length==before.Length,"Double-click must not write an image");
                        }
                        Check(home.Controls.Count==0,"No dashboard, gallery or history controls should remain");
                    }
                }
                var desktop=SystemInformation.VirtualScreen;
                using(var capture=CaptureOverlay.CaptureDesktop(desktop)) Check(capture.Size==desktop.Size,"Desktop capture dimensions mismatch");
                using(var source=new Bitmap(960,540))
                {
                    using(var g=Graphics.FromImage(source))
                    using(var title=new Font("Microsoft YaHei UI",32,FontStyle.Bold,GraphicsUnit.Pixel))
                    {
                        g.Clear(Color.FromArgb(238,244,251)); g.FillRectangle(Brushes.SteelBlue,64,64,720,360);
                        g.DrawString("映贴 · 框选后直接贴图",title,Brushes.White,100,135);
                    }
                    using(var overlay=new CaptureOverlay(source,new Rectangle(100,100,960,540)))
                    {
                        overlay.Show(); Application.DoEvents();
                        CheckCaptureTrail(overlay);
                        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                        typeof(CaptureOverlay).GetMethod("OnMouseDown",flags).Invoke(overlay,new object[] { new MouseEventArgs(MouseButtons.Left,1,100,80,0) });
                        typeof(CaptureOverlay).GetMethod("OnMouseMove",flags).Invoke(overlay,new object[] { new MouseEventArgs(MouseButtons.Left,0,680,360,0) });
                        using(var preview=new Bitmap(overlay.Width,overlay.Height))
                        {
                            overlay.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));
                            Check(preview.GetPixel(200,300).ToArgb()==source.GetPixel(200,300).ToArgb(),"Selected area must remain undimmed");
                            Check(preview.GetPixel(20,20).ToArgb()!=source.GetPixel(20,20).ToArgb(),"Unselected area must be dimmed");
                            preview.Save(Path.Combine(output,"capture-preview.png"),ImageFormat.Png);
                        }
                        typeof(CaptureOverlay).GetMethod("OnMouseUp",flags).Invoke(overlay,new object[] { new MouseEventArgs(MouseButtons.Left,1,680,360,0) });
                        using(var result=overlay.Result)
                        {
                            Check(result.Size==new Size(580,280),"Crop must preserve selection dimensions");
                            Check(result.GetPixel(20,30).ToArgb()==source.GetPixel(120,110).ToArgb(),"Crop must use original pixels without overlay or text");
                        }
                        Check(overlay.SelectedScreenBounds==new Rectangle(200,180,580,280),"Selection must map to screen coordinates");
                    }
                }
                File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: incremental capture paint matches fresh frames across hover, drag direction changes, edge movement and tiny cancelled selection; frameless pin; compact toolbar; colored annotations; original-size export; undo; transparency; double-click close; original-pixel crop and screen coordinates.\r\nPointer actions were tested through event handlers. Clipboard roundtrip, save dialog, live hotkeys, mixed-DPI monitors and physical hover remain unverified.\r\n"+DateTime.Now.ToString("o"));
                return 0;
            }
            catch(Exception e) { File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL\r\n"+e); return 1; }
        }
    }
}
