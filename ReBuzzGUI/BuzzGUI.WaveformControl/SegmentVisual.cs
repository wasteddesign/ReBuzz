using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BuzzGUI.WaveformControl
{
    public class SegmentVisual : DrawingVisual
    {
        public class Resources
        {
            public Brush fill;
            public Pen pen;

            public Resources(WaveformElement e, int h)
            {
                fill = e.FindResource("FillBrush") as Brush;
                if (fill is LinearGradientBrush)
                {
                    LinearGradientBrush lgb = fill.Clone() as LinearGradientBrush;
                    lgb.EndPoint = new Point(0, h);
                    fill = lgb;
                }
                fill.Freeze();


                pen = new Pen(e.FindResource("WaveformOutlineBrush") as Brush, 0.5);
                pen.Freeze();
            }


        }

        public bool Allocated = false;
        public int SampleIndex;
        RenderTargetBitmap rtb;

        public SegmentVisual()
        {
            /*
			this.CacheMode = new BitmapCache()
			{
				RenderAtScale = 1.0,
				SnapsToDevicePixels = true
			};
			 */
        }

        Task task;
        readonly bool bg = false;

        public void Render(WaveformElement wfc, MinMaxCache mmc, int mmcindex, int sampleWidth, int height, Resources r)
        {
            if (height <= 0)
                return;


            if (bg)
            {
                if (task != null)
                    task.Wait();

                task = Task.Factory.StartNew(() =>
                {
                    MinMaxCache.MinMax[] points = null;

                    lock (this)
                    {
                        points = mmc.GetSegmentPlusOne(mmcindex);
                        CreatePiece(wfc, points, sampleWidth, height, r);
                        rtb.Freeze();
                    }

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        lock (this)
                        {
                            var dc = RenderOpen();
                            dc.DrawImage(rtb, new Rect(0, 0, points.Length * sampleWidth, height));
                            dc.Close();
                        }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                });
            }
            else
            {
                MinMaxCache.MinMax[] points = null;
                points = mmc.GetSegmentPlusOne(mmcindex);
                CreatePiece(wfc, points, sampleWidth, height, r);
                var dc = RenderOpen();
                dc.DrawImage(rtb, new Rect(0, 0, points.Length * sampleWidth, height));
                dc.Close();
            }

        }

        void CreatePiece(WaveformElement wfc, MinMaxCache.MinMax[] points, int pw, int h, Resources r)
        {
            double my = h / 2;

            DrawingVisual dv = new DrawingVisual();
            DrawingContext dc = dv.RenderOpen();

            PathFigure pf = new PathFigure();
            PathFigure spf = new PathFigure();
            PathFigure spf2 = new PathFigure();

            pf.StartPoint = new Point(0, my);
            for (int i = 0; i < points.Length; i++)
            {
                double v = my - points[i].MaxNormalized * my;

                pf.Segments.Add(new LineSegment(new Point(pw * i, v), true));

                if (i == 0)
                    spf.StartPoint = new Point(0, v);
                else
                    spf.Segments.Add(new LineSegment(new Point(pw * i, v), true));
            }

            pf.Segments.Add(new LineSegment(new Point(pw * (points.Length - 1), my), true));

            for (int i = points.Length - 1; i >= 0; i--)
            {
                double v = my - points[i].MinNormalized * my;

                pf.Segments.Add(new LineSegment(new Point(pw * i, v), true));

                if (i == points.Length - 1)
                    spf2.StartPoint = new Point(pw * i, v);
                else
                    spf2.Segments.Add(new LineSegment(new Point(pw * i, v), true));
            }

            pf.Segments.Add(new LineSegment(new Point(0, my), true));

            PathGeometry pg = new PathGeometry();
            pg.Figures.Add(pf);

            dc.DrawGeometry(r.fill, null, pg);

            PathGeometry spg = new PathGeometry();
            spg.Figures.Add(spf);
            dc.DrawGeometry(null, r.pen, spg);

            PathGeometry spg2 = new PathGeometry();
            spg2.Figures.Add(spf2);
            dc.DrawGeometry(null, r.pen, spg2);

            dc.Close();

            if (rtb == null || bg)
                rtb = new RenderTargetBitmap(pw * points.Length, h, 96, 96, PixelFormats.Pbgra32);
            else
                rtb.Clear();

            rtb.Render(dv);

            if (bg) rtb.Freeze();
        }

    }
}
