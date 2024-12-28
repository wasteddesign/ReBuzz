using System;
using System.Windows;

namespace BuzzGUI.MachineView
{
    static class Clipper
    {
        static double sgn(double x)
        {
            if (x < 0)
                return -1;
            else
                return 1;
        }



        // in: p1, p2 = line
        // out: p1, p2 = intersection coordinates
        public static bool LineCircleIntersect(ref Point p1, ref Point p2, Point cp, double r)
        {
            p1.X -= cp.X;
            p1.Y -= cp.Y;
            p2.X -= cp.X;
            p2.Y -= cp.Y;

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dr = Math.Sqrt(dx * dx + dy * dy);
            double D = p1.X * p2.Y - p2.X * p1.Y;

            double disc = r * r * dr * dr - D * D;
            if (disc <= 0)
                return false;

            p1.X = cp.X + (D * dy - sgn(dy) * dx * Math.Sqrt(disc)) / (dr * dr);
            p1.Y = cp.Y + (-D * dx - Math.Abs(dy) * Math.Sqrt(disc)) / (dr * dr);

            p2.X = cp.X + (D * dy + sgn(dy) * dx * Math.Sqrt(disc)) / (dr * dr);
            p2.Y = cp.Y + (-D * dx + Math.Abs(dy) * Math.Sqrt(disc)) / (dr * dr);

            return true;
        }


        public static Point ClipLineByRect(Point p1, Point p2, Rect r)
        {

            if (p2.Y < r.Top)
            {
                double a = (r.Top - p1.Y) / (p2.Y - p1.Y);
                p2.X = p1.X + a * (p2.X - p1.X);
                p2.Y = r.Top;
            }
            else if (p2.Y > r.Bottom)
            {
                double a = (r.Bottom - p1.Y) / (p2.Y - p1.Y);
                p2.X = p1.X + a * (p2.X - p1.X);
                p2.Y = r.Bottom;
            }

            if (p2.X < r.Left)
            {
                double a = (r.Left - p1.X) / (p2.X - p1.X);
                p2.Y = p1.Y + a * (p2.Y - p1.Y);
                p2.X = r.Left;
            }
            else if (p2.X > r.Right)
            {
                double a = (r.Right - p1.X) / (p2.X - p1.X);
                p2.Y = p1.Y + a * (p2.Y - p1.Y);
                p2.X = r.Right;
            }

            return p2;
        }

        public static Point ClipLineByRoundedRect(Point p1, Point p2, Rect r, double rad)
        {
            p2 = ClipLineByRect(p1, p2, r);

            double rcx = r.Left + r.Width / 2;
            double rcy = r.Top + r.Height / 2;

            Point i1 = p1;
            Point i2 = p2;

            if (i2.Y < rcy)
            {
                if (i2.X < rcx)
                {
                    if (LineCircleIntersect(ref i1, ref i2, new Point(r.Left + rad, r.Top + rad), rad))
                    {
                        if (i1.X < r.Left + rad && i1.Y < r.Top + rad)
                            p2 = i1;
                    }
                }
                else if (i2.X > rcx)
                {
                    if (LineCircleIntersect(ref i1, ref i2, new Point(r.Right - rad, r.Top + rad), rad))
                    {
                        if (i1.X > (r.Right - rad) && i1.Y < (r.Top + rad))
                            p2 = i1;
                    }
                }
            }
            else
            {
                if (i2.X < rcx)
                {
                    if (LineCircleIntersect(ref i1, ref i2, new Point(r.Left + rad, r.Bottom - rad), rad))
                    {
                        if (i2.X < r.Left + rad && i2.Y > r.Bottom - rad)
                            p2 = i2;
                    }
                }
                else if (i2.X > rcx)
                {
                    if (LineCircleIntersect(ref i1, ref i2, new Point(r.Right - rad, r.Bottom - rad), rad))
                    {
                        if (i2.X > (r.Right - rad) && i2.Y > (r.Bottom - rad))
                            p2 = i2;
                    }
                }
            }




            return p2;
        }


        public static void ClipLineByTwoRoundedRects(ref Point p1, ref Point p2, Rect r1, Rect r2, double rad)
        {
            p1 = ClipLineByRoundedRect(p1, p2, r1, rad);
            p2 = ClipLineByRoundedRect(p2, p1, r2, rad);


        }

    }
}
