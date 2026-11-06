using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace YingTie
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int value);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int msg,IntPtr w,IntPtr l);
        [STAThread] static void Main(string[] args)
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch { try { SetProcessDpiAwareness(2); } catch { } }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
#if TESTS
            if(args.Length>0 && args[0]=="--self-test") { Environment.ExitCode=SelfTest.Run(); return; }
#endif
            bool first;
            using(var gate=new Mutex(true,"Local\\YingTie.Desktop.v1",out first))
            {
                if(!first) { var window=FindWindow(null,"映贴（后台）"); if(window!=IntPtr.Zero) PostMessage(window,0x8001,IntPtr.Zero,IntPtr.Zero); return; }
                Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e) { Theme.Error(null,e.Exception); };
                Application.Run(new HomeWindow());
            }
        }
    }
    static class Theme
    {
        public static readonly Color Ink=Color.FromArgb(29,48,74),Blue=Color.FromArgb(39,100,218),Line=Color.FromArgb(220,228,239);
        public static Font Font(float size) { return new Font("Microsoft YaHei UI",(float)Math.Round(size*1.5f),FontStyle.Regular,GraphicsUnit.Pixel); }
        public static Button Button(string text,int width,EventHandler click)
        {
            var b=new Button { Text=text,Width=width,Height=32,FlatStyle=FlatStyle.Flat,Font=Font(9),BackColor=Color.White,ForeColor=Ink,Cursor=Cursors.Hand,UseVisualStyleBackColor=false };
            b.FlatAppearance.BorderSize=0; b.FlatAppearance.MouseOverBackColor=Color.FromArgb(231,239,252); b.Click+=click; return b;
        }
        public static void Error(IWin32Window owner,Exception e) { MessageBox.Show(owner,e.Message,"操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        public static Bitmap ClipboardImage()
        {
            if(!Clipboard.ContainsImage()) return null;
            using(var image=Clipboard.GetImage()) return image==null ? null : new Bitmap(image);
        }
        public static bool Copy(IWin32Window owner,Bitmap image)
        {
            try { Clipboard.SetImage(image); return true; }
            catch(ExternalException) { MessageBox.Show(owner,"剪贴板被其他程序占用，请稍后再试。","映贴"); return false; }
        }
        public static bool Save(IWin32Window owner,Bitmap image)
        {
            using(var dialog=new SaveFileDialog { Title="保存图片",Filter="PNG 图片|*.png",FileName="映贴-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png",AddExtension=true,DefaultExt="png" })
            {
                if(dialog.ShowDialog(owner)!=DialogResult.OK) return false;
                try { image.Save(dialog.FileName,ImageFormat.Png); return true; }
                catch(Exception e) { Error(owner,e); return false; }
            }
        }
    }
    sealed class HomeWindow : Form
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
        readonly NotifyIcon tray;
        readonly bool testing;
        bool capturing;
        public bool IsCapturing { get { return capturing; } }
        public HomeWindow(bool testMode=false)
        {
            testing=testMode;
            Text="映贴（后台）"; Name="YingTieBackground"; ShowInTaskbar=false;
            FormBorderStyle=FormBorderStyle.FixedToolWindow; ClientSize=new Size(1,1);
            tray=new NotifyIcon { Icon=SystemIcons.Application,Text="映贴 · Ctrl+Alt+A 截图",Visible=!testing };
            var menu=new ContextMenuStrip { Font=Theme.Font(9) };
            menu.Items.Add("截图  Ctrl+Alt+A",null,delegate { Capture(); });
            menu.Items.Add("剪贴板贴图  Ctrl+Alt+V",null,delegate { Paste(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出",null,delegate { Application.Exit(); });
            tray.ContextMenuStrip=menu; tray.DoubleClick+=delegate { Capture(); };
            var backgroundHandle=Handle;
        }
        protected override void SetVisibleCore(bool value) { base.SetVisibleCore(false); }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if(testing) return;
            bool a=RegisterHotKey(Handle,1,0x4003,(uint)Keys.A), v=RegisterHotKey(Handle,2,0x4003,(uint)Keys.V);
            tray.ShowBalloonTip(2500,"映贴已就绪",!a || !v ? "快捷键被占用，可右键通知区域图标截图。" : "Ctrl+Alt+A 截图；双击贴图关闭，不自动保存。",ToolTipIcon.Info);
        }
        protected override void OnHandleDestroyed(EventArgs e) { UnregisterHotKey(Handle,1); UnregisterHotKey(Handle,2); base.OnHandleDestroyed(e); }
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x8001 || (m.Msg==0x0312 && m.WParam.ToInt32()==1)) Capture();
            else if(m.Msg==0x0312 && m.WParam.ToInt32()==2) Paste();
            base.WndProc(ref m);
        }
        public PinWindow Pin(Bitmap image,Point? position=null) { var pin=new PinWindow(this,image,position); pin.Show(); return pin; }
        void Paste()
        {
            try
            {
                var image=Theme.ClipboardImage();
                if(image==null) { tray.ShowBalloonTip(1800,"剪贴板里没有图片","先复制图片，或按 Ctrl+Alt+A 截图。",ToolTipIcon.Info); return; }
                Pin(image);
            }
            catch(Exception e) { Theme.Error(null,e); }
        }
        public new void Capture()
        {
            if(capturing) return;
            capturing=true;
            foreach(Form form in Application.OpenForms) { var toolbar=form as PinToolbar; if(toolbar!=null) toolbar.Hide(); }
            var timer=new System.Windows.Forms.Timer { Interval=180 };
            timer.Tick+=delegate {
                timer.Stop(); timer.Dispose(); Bitmap chosen=null; Point position=Point.Empty;
                try
                {
                    var bounds=SystemInformation.VirtualScreen;
                    using(var screen=CaptureOverlay.CaptureDesktop(bounds))
                    using(var overlay=new CaptureOverlay(screen,bounds))
                        if(overlay.ShowDialog()==DialogResult.OK && overlay.Result!=null) { chosen=overlay.Result; position=overlay.SelectedScreenBounds.Location; }
                }
                catch(Exception e) { Theme.Error(null,e); }
                finally { capturing=false; }
                if(chosen!=null) Pin(chosen,position);
            };
            timer.Start();
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing) { tray.Visible=false; var menu=tray.ContextMenuStrip; tray.Dispose(); if(menu!=null) menu.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
