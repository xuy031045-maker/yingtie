using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YingTie
{
    sealed class CaptureOverlay : Form
    {
        readonly Bitmap screen;
        Point start, current;
        bool dragging;
        public Bitmap Result;
        public Rectangle SelectedScreenBounds { get; private set; }
        public static Bitmap CaptureDesktop(Rectangle bounds)
        {
            if (bounds.Width<=0 || bounds.Height<=0 || (long)bounds.Width*bounds.Height>120000000)
                throw new InvalidOperationException("当前屏幕尺寸无法截图。");
            var bitmap=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb);
            try { using(var g=Graphics.FromImage(bitmap)) g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy); return bitmap; }
            catch { bitmap.Dispose(); throw; }
        }
        public CaptureOverlay(Bitmap frozen, Rectangle bounds)
        {
            screen=frozen; AutoScaleMode=AutoScaleMode.None; FormBorderStyle=FormBorderStyle.None;
            StartPosition=FormStartPosition.Manual; Bounds=bounds; TopMost=true; ShowInTaskbar=false;
            DoubleBuffered=true; Cursor=Cursors.Cross; KeyPreview=true; Text="映贴 · 选择截图区域";
        }
        protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); Focus(); }
        Rectangle Selection { get { return Rectangle.Intersect(Rectangle.FromLTRB(Math.Min(start.X,current.X),Math.Min(start.Y,current.Y),Math.Max(start.X,current.X),Math.Max(start.Y,current.Y)),ClientRectangle); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(screen,0,0);
            using(var shade=new SolidBrush(Color.FromArgb(112,12,23,40))) e.Graphics.FillRectangle(shade,ClientRectangle);
            var rect=Selection;
            if(dragging && rect.Width>0 && rect.Height>0)
            {
                e.Graphics.DrawImage(screen,rect,rect,GraphicsUnit.Pixel);
                using(var pen=new Pen(Color.FromArgb(86,167,255),2)) e.Graphics.DrawRectangle(pen,rect);
                string dimensions=rect.Width+" × "+rect.Height;
                DrawBadge(e.Graphics, dimensions, new Point(Math.Max(4,rect.Left),Math.Max(4,rect.Top-30)));
            }
            Point hint=PointToClient(Cursor.Position); hint.Offset(20,22);
            DrawBadge(e.Graphics, "拖动选择区域   ·   松开直接贴图   ·   Esc 取消", hint);
        }
        void DrawBadge(Graphics g,string text,Point at)
        {
            using(var font=Theme.Font(10))
            {
                Size size=TextRenderer.MeasureText(text,font); size.Width+=20; size.Height+=12;
                at.X=Math.Max(0,Math.Min(ClientSize.Width-size.Width,at.X)); at.Y=Math.Max(0,Math.Min(ClientSize.Height-size.Height,at.Y));
                using(var brush=new SolidBrush(Theme.Ink)) g.FillRectangle(brush,new Rectangle(at,size));
                TextRenderer.DrawText(g,text,font,new Point(at.X+10,at.Y+6),Color.White);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if(e.Button==MouseButtons.Right) { DialogResult=DialogResult.Cancel; Close(); return; }
            if(e.Button!=MouseButtons.Left) return;
            start=e.Location; current=start; dragging=true; Capture=true; Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e) { current=e.Location; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if(!dragging || e.Button!=MouseButtons.Left) return;
            current=e.Location; var selected=Selection; dragging=false; Capture=false;
            if(selected.Width<3 || selected.Height<3) { Invalidate(); return; }
            Result=screen.Clone(selected,PixelFormat.Format32bppArgb); SelectedScreenBounds=new Rectangle(PointToScreen(selected.Location),selected.Size); DialogResult=DialogResult.OK; Close();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if(keyData==Keys.Escape) { DialogResult=DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref msg,keyData);
        }
    }

}
