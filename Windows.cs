using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YingTie
{
    sealed class CaptureOverlay : Form
    {
        readonly Bitmap screen;
        readonly Bitmap dimmed;
        readonly Font badgeFont=new Font("Microsoft YaHei UI",16,FontStyle.Regular,GraphicsUnit.Pixel);
        readonly StringFormat badgeFormat=new StringFormat(StringFormat.GenericTypographic) { FormatFlags=StringFormatFlags.NoWrap };
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
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Opaque,true);
            dimmed=new Bitmap(frozen.Width,frozen.Height,PixelFormat.Format32bppRgb);
            using(var g=Graphics.FromImage(dimmed))
            using(var shade=new SolidBrush(Color.FromArgb(88,15,25,40))) { g.DrawImageUnscaled(screen,0,0); g.FillRectangle(shade,0,0,dimmed.Width,dimmed.Height); }
        }
        protected override void OnShown(EventArgs e) { base.OnShown(e); current=PointToClient(Cursor.Position); Activate(); Focus(); }
        Rectangle Selection { get { return Rectangle.Intersect(Rectangle.FromLTRB(Math.Min(start.X,current.X),Math.Min(start.Y,current.Y),Math.Max(start.X,current.X),Math.Max(start.Y,current.Y)),ClientRectangle); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.CompositingMode=CompositingMode.SourceCopy;
            e.Graphics.DrawImageUnscaled(dimmed,0,0);
            var rect=Selection;
            if(dragging && rect.Width>0 && rect.Height>0)
            {
                var repaint=Rectangle.Intersect(rect,e.ClipRectangle);
                if(repaint.Width>0 && repaint.Height>0) e.Graphics.DrawImage(screen,repaint,repaint,GraphicsUnit.Pixel);
                using(var pen=new Pen(Color.FromArgb(107,184,255),1)) e.Graphics.DrawRectangle(pen,rect);
            }
            e.Graphics.CompositingMode=CompositingMode.SourceOver;
            DrawBadge(e.Graphics,BadgeBounds);
        }
        string BadgeText { get { return dragging ? Selection.Width+" × "+Selection.Height+"    松开贴图" : "拖动截图    Esc 取消"; } }
        Rectangle BadgeBounds
        {
            get
            {
                int width=dragging ? 254 : 224;
                int x=Math.Max(4,Math.Min(ClientSize.Width-width-4,current.X+20));
                int y=current.Y+26;
                if(y+42>ClientSize.Height) y=current.Y-54;
                return new Rectangle(x,Math.Max(4,y),width,42);
            }
        }
        void DrawBadge(Graphics g,Rectangle bounds)
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=new GraphicsPath())
            using(var background=new SolidBrush(Color.FromArgb(30,42,59)))
            {
                const int radius=12;
                path.AddArc(bounds.Left,bounds.Top,radius,radius,180,90);
                path.AddArc(bounds.Right-radius,bounds.Top,radius,radius,270,90);
                path.AddArc(bounds.Right-radius,bounds.Bottom-radius,radius,radius,0,90);
                path.AddArc(bounds.Left,bounds.Bottom-radius,radius,radius,90,90); path.CloseFigure();
                g.FillPath(background,path);
            }
            g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
            g.DrawString(BadgeText,badgeFont,Brushes.White,new PointF(bounds.Left+15,bounds.Top+10),badgeFormat);
        }
        void InvalidateChange(Rectangle oldSelection,Rectangle oldBadge)
        {
            // Repaint a complete bounded area, including antialiased pixels outside
            // badge/selection edges. A fragmented XOR region left rounded edges
            // and overlapping translucent pixels dependent on the previous frame.
            var dirty=Rectangle.Union(oldBadge,BadgeBounds);
            if(dragging) dirty=Rectangle.Union(dirty,Rectangle.Union(oldSelection,Selection));
            dirty.Inflate(3,3);
            Invalidate(Rectangle.Intersect(dirty,ClientRectangle));
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if(e.Button==MouseButtons.Right) { DialogResult=DialogResult.Cancel; Close(); return; }
            if(e.Button!=MouseButtons.Left) return;
            var oldSelection=Selection; var oldBadge=BadgeBounds;
            start=e.Location; current=start; dragging=true; Capture=true; InvalidateChange(oldSelection,oldBadge);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if(current==e.Location) return;
            var oldSelection=Selection; var oldBadge=BadgeBounds; current=e.Location; InvalidateChange(oldSelection,oldBadge);
        }
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
        protected override void Dispose(bool disposing)
        {
            if(disposing) { dimmed.Dispose(); badgeFont.Dispose(); badgeFormat.Dispose(); }
            base.Dispose(disposing);
        }
    }

}
