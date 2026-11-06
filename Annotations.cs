using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace YingTie
{
    enum MarkTool { Move, Ellipse, Rectangle, Arrow }
    sealed class Annotation
    {
        public MarkTool Tool;
        public PointF Start,End;
        public Color Color;
        public bool HasSize { get { return Math.Abs(End.X-Start.X)>2 || Math.Abs(End.Y-Start.Y)>2; } }
        public void Draw(Graphics g)
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var pen=new Pen(Color,3) { LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round })
            {
                var r=RectangleF.FromLTRB(Math.Min(Start.X,End.X),Math.Min(Start.Y,End.Y),Math.Max(Start.X,End.X),Math.Max(Start.Y,End.Y));
                if(Tool==MarkTool.Ellipse && r.Width>0 && r.Height>0) g.DrawEllipse(pen,r);
                else if(Tool==MarkTool.Rectangle && r.Width>0 && r.Height>0) g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);
                else if(Tool==MarkTool.Arrow)
                {
                    g.DrawLine(pen,Start,End);
                    double a=Math.Atan2(End.Y-Start.Y,End.X-Start.X);
                    float length=(float)Math.Sqrt(Math.Pow(End.X-Start.X,2)+Math.Pow(End.Y-Start.Y,2));
                    float head=Math.Min(15,length*.4f);
                    g.DrawLine(pen,End,new PointF(End.X-head*(float)Math.Cos(a-.5),End.Y-head*(float)Math.Sin(a-.5)));
                    g.DrawLine(pen,End,new PointF(End.X-head*(float)Math.Cos(a+.5),End.Y-head*(float)Math.Sin(a+.5)));
                }
            }
        }
    }
}
