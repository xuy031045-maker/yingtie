using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YingTie
{
    sealed class PinWindow : Form
    {
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
        readonly HomeWindow home;
        readonly Bitmap image;
        readonly Timer hoverTimer;
        readonly PinToolbar toolbar;
        float zoom=1;
        int outsideTicks;
        bool holding,moving,disposed,dialogOpen;
        Point dragPointer,dragOrigin;
        public Size ImageSize { get { return image.Size; } }
        public PinToolbar Actions { get { return toolbar; } }
        public PinWindow(HomeWindow owner,Bitmap bitmap,Point? position=null)
        {
            home=owner; image=bitmap; Text="映贴"; Name="YingTiePin";
            AutoScaleMode=AutoScaleMode.None; FormBorderStyle=FormBorderStyle.None; TopMost=true; ShowInTaskbar=false;
            DoubleBuffered=true; KeyPreview=true; BackColor=Color.White; StartPosition=FormStartPosition.Manual;
            var area=Screen.FromPoint(position ?? Cursor.Position).WorkingArea;
            Location=position ?? new Point(area.Left+area.Width/3,area.Top+area.Height/3);
            SetZoom(position.HasValue ? 1 : Math.Min(1,Math.Min(area.Width*0.65f/image.Width,area.Height*0.65f/image.Height)));
            if(!position.HasValue) Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);
            toolbar=new PinToolbar(this);
            hoverTimer=new Timer { Interval=100 };
            hoverTimer.Tick+=delegate {
                if(!Visible || holding || dialogOpen || home.IsCapturing) { HideActions(); return; }
                IntPtr underPointer=GetAncestor(WindowFromPoint(Cursor.Position),2);
                bool hovering=(Bounds.Contains(Cursor.Position) && underPointer==Handle) || (toolbar.Visible && toolbar.Bounds.Contains(Cursor.Position) && underPointer==toolbar.Handle);
                if(hovering) { outsideTicks=0; ShowActions(); }
                else if(++outsideTicks>=5) HideActions();
            };
        }
        protected override void OnShown(EventArgs e) { base.OnShown(e); hoverTimer.Start(); }
        void SetZoom(float value)
        {
            var area=Screen.FromPoint(Location).WorkingArea;
            float max=Math.Min(4,Math.Min(area.Width/(float)image.Width,area.Height/(float)image.Height));
            float min=Math.Min(max,Math.Max(0.02f,8f/Math.Min(image.Width,image.Height)));
            zoom=Math.Max(min,Math.Min(max,value));
            ClientSize=new Size(Math.Max(1,(int)Math.Round(image.Width*zoom)),Math.Max(1,(int)Math.Round(image.Height*zoom))); Invalidate(); PositionActions();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if(ClientSize==image.Size) e.Graphics.DrawImageUnscaled(image,0,0);
            else { e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic; e.Graphics.DrawImage(image,ClientRectangle); }
        }
        public void ShowActions()
        {
            if(disposed || holding || dialogOpen || home.IsCapturing || !Visible) return;
            PositionActions(); toolbar.Sync(); if(!toolbar.Visible) toolbar.Show(this);
        }
        public void HideActions() { if(toolbar!=null && !toolbar.IsDisposed && toolbar.Visible) toolbar.Hide(); }
        void PositionActions()
        {
            if(toolbar==null || toolbar.IsDisposed) return;
            var area=Screen.FromRectangle(Bounds).WorkingArea;
            int x=Math.Max(area.Left,Math.Min(area.Right-toolbar.Width,Left+(Width-toolbar.Width)/2));
            int y=Bottom+4; if(y+toolbar.Height>area.Bottom) y=Top-toolbar.Height-4;
            toolbar.Location=new Point(x,Math.Max(area.Top,Math.Min(area.Bottom-toolbar.Height,y)));
        }
        protected override void OnLocationChanged(EventArgs e) { base.OnLocationChanged(e); PositionActions(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); ShowActions(); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if(!Visible) HideActions(); }
        public void CopyImage() { Theme.Copy(this,image); }
        public void SaveImage()
        {
            dialogOpen=true; HideActions(); hoverTimer.Stop();
            try { Theme.Save(this,image); }
            finally { if(!disposed) { dialogOpen=false; hoverTimer.Start(); } }
        }
        public void SetTransparency(int percent) { Opacity=Math.Max(0.2,Math.Min(1,percent/100.0)); if(toolbar!=null) toolbar.Sync(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if(e.Button!=MouseButtons.Left || e.Clicks>1) return;
            dragPointer=PointToScreen(e.Location); dragOrigin=Location; holding=true; moving=false; Capture=true; HideActions();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); if(!holding) return;
            var now=PointToScreen(e.Location); int dx=now.X-dragPointer.X,dy=now.Y-dragPointer.Y;
            if(!moving && (Math.Abs(dx)>SystemInformation.DragSize.Width/2 || Math.Abs(dy)>SystemInformation.DragSize.Height/2)) moving=true;
            if(moving) Location=new Point(dragOrigin.X+dx,dragOrigin.Y+dy);
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if(e.Button==MouseButtons.Left) { holding=false; moving=false; Capture=false; } }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if(!Capture) { holding=false; moving=false; } }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e); if(e.Button==MouseButtons.Left) Close();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if((ModifierKeys&Keys.Control)==Keys.Control) { SetTransparency((int)Math.Round(Opacity*100)+(e.Delta>0?10:-10)); return; }
            var center=new Point(Left+Width/2,Top+Height/2); SetZoom(zoom*(e.Delta>0?1.12f:1/1.12f)); Location=new Point(center.X-Width/2,center.Y-Height/2);
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keys)
        {
            if(keys==Keys.Escape) { Close(); return true; }
            if(keys==(Keys.Control|Keys.C)) { CopyImage(); return true; }
            if(keys==(Keys.Control|Keys.S)) { SaveImage(); return true; }
            return base.ProcessCmdKey(ref msg,keys);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing && !disposed) { disposed=true; if(hoverTimer!=null) hoverTimer.Dispose(); if(toolbar!=null) toolbar.Dispose(); image.Dispose(); }
            base.Dispose(disposing);
        }
    }
    sealed class PinToolbar : Form
    {
        readonly PinWindow pin;
        readonly TrackBar opacity;
        readonly Label label;
        readonly ToolTip tip;
        bool syncing;
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var cp=base.CreateParams; cp.ExStyle|=0x80; return cp; } }
        public PinToolbar(PinWindow owner)
        {
            pin=owner; Text="映贴操作"; Name="YingTiePinToolbar";
            AutoScaleMode=AutoScaleMode.None; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
            StartPosition=FormStartPosition.Manual; ClientSize=new Size(328,42); BackColor=Color.White;
            var copy=Theme.Button("复制",52,delegate { pin.CopyImage(); }); copy.Location=new Point(4,5); Controls.Add(copy);
            var save=Theme.Button("保存",52,delegate { pin.SaveImage(); }); save.Location=new Point(58,5); Controls.Add(save);
            label=new Label { Text="100%",Font=Theme.Font(8),ForeColor=Theme.Ink,Location=new Point(119,13),Size=new Size(45,20) }; Controls.Add(label);
            opacity=new TrackBar { Location=new Point(162,6),Size=new Size(119,30),Minimum=20,Maximum=100,Value=100,TickStyle=TickStyle.None,AutoSize=false,SmallChange=5,LargeChange=10,AccessibleName="贴图透明度" };
            opacity.ValueChanged+=delegate { if(!syncing) pin.SetTransparency(opacity.Value); }; Controls.Add(opacity);
            var close=Theme.Button("×",32,delegate { pin.Close(); }); close.Location=new Point(289,5); Controls.Add(close);
            tip=new ToolTip(); tip.SetToolTip(copy,"复制图片（Ctrl+C）"); tip.SetToolTip(save,"保存为 PNG（Ctrl+S）"); tip.SetToolTip(opacity,"贴图透明度"); tip.SetToolTip(close,"关闭，不自动保存（也可双击图片）");
        }
        public void Sync()
        {
            if(IsDisposed) return;
            int percent=Math.Max(20,Math.Min(100,(int)Math.Round(pin.Opacity*100)));
            label.Text=percent+"%"; syncing=true; opacity.Value=percent; syncing=false;
        }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using(var pen=new Pen(Theme.Line)) e.Graphics.DrawRectangle(pen,0,0,ClientSize.Width-1,ClientSize.Height-1); }
        protected override void Dispose(bool disposing) { if(disposing && tip!=null) tip.Dispose(); base.Dispose(disposing); }
    }
}
