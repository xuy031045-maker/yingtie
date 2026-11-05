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
                            Check(pin.Actions.ClientSize.Height<=44 && pin.Actions.ClientSize.Width<=340,"Toolbar should fit on one compact row");
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
                File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: hidden tray controller with message handle; no dashboard/history controls; frameless pin pixel alignment; single-row toolbar; toolbar show/hide/position; transparency; client drag; double-click closes image and toolbar without writing an image; desktop capture dimensions.\r\nDouble-click/drag were tested through form event handlers, not physical mouse input. Clipboard roundtrip, save dialog, live hotkeys, mixed-DPI monitors and physical hover remain unverified.\r\n"+DateTime.Now.ToString("o"));
                return 0;
            }
            catch(Exception e) { File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL\r\n"+e); return 1; }
        }
    }
}
