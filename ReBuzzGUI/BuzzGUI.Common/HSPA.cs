using System;
using System.Windows.Media;

namespace BuzzGUI.Common
{
    public class HSPA
    {
        const double Pr = 0.299;
        const double Pg = 0.587;
        const double Pb = 0.114;

        public double H { get; set; }
        public double S { get; set; }
        public double P { get; set; }
        public double A { get; set; }

        public HSPA(double h, double s, double p)
        {
            H = h;
            S = s;
            P = p;
            A = 1.0;
        }

        public HSPA(double h, double s, double p, double a)
        {
            H = h;
            S = s;
            P = p;
            A = a;
        }

        public Color ToColor()
        {
            double r, g, b;
            HSPtoRGB(out r, out g, out b);
            return Color.FromArgb((byte)Math.Min(255, A * 255), (byte)Math.Min(255, r * 255), (byte)Math.Min(255, g * 255), (byte)Math.Min(255, b * 255));
        }

        public static HSPA Blend(HSPA x, HSPA y, double alpha)
        {
            return new HSPA(
                x.H + alpha * (y.H - x.H),
                x.S + alpha * (y.S - x.S),
                x.P + alpha * (y.P - x.P),
                x.A + alpha * (y.A - x.A));
        }

        public double MaxRGBValue
        {
            get
            {
                double r, g, b;
                HSPtoRGB(out r, out g, out b);
                return Math.Max(Math.Max(r, g), b);
            }
        }

        void HSPtoRGB(out double R, out double G, out double B)
        {
            var h = H;
            var s = S;
            var p = P;
            double part, minOverMax = 1.0 - s;

            if (minOverMax > 0.0)
            {
                if (h < 1.0 / 6.0)
                {   //  R>G>B
                    h = 6.0 * (h - 0.0 / 6.0); part = 1.0 + h * (1.0 / minOverMax - 1.0);
                    B = p / Math.Sqrt(Pr / minOverMax / minOverMax + Pg * part * part + Pb);
                    R = (B) / minOverMax; G = (B) + h * ((R) - (B));
                }
                else if (h < 2.0 / 6.0)
                {   //  G>R>B
                    h = 6.0 * (-h + 2.0 / 6.0); part = 1.0 + h * (1.0 / minOverMax - 1.0);
                    B = p / Math.Sqrt(Pg / minOverMax / minOverMax + Pr * part * part + Pb);
                    G = (B) / minOverMax; R = (B) + h * ((G) - (B));
                }
                else if (h < 3.0 / 6.0)
                {   //  G>B>R
                    h = 6.0 * (h - 2.0 / 6.0); part = 1.0 + h * (1.0 / minOverMax - 1.0);
                    R = p / Math.Sqrt(Pg / minOverMax / minOverMax + Pb * part * part + Pr);
                    G = (R) / minOverMax; B = (R) + h * ((G) - (R));
                }
                else if (h < 4.0 / 6.0)
                {   //  B>G>R
                    h = 6.0 * (-h + 4.0 / 6.0); part = 1.0 + h * (1.0 / minOverMax - 1.0);
                    R = p / Math.Sqrt(Pb / minOverMax / minOverMax + Pg * part * part + Pr);
                    B = (R) / minOverMax; G = (R) + h * ((B) - (R));
                }
                else if (h < 5.0 / 6.0)
                {   //  B>R>G
                    h = 6.0 * (h - 4.0 / 6.0); part = 1.0 + h * (1.0 / minOverMax - 1.0);
                    G = p / Math.Sqrt(Pb / minOverMax / minOverMax + Pr * part * part + Pg);
                    B = (G) / minOverMax; R = (G) + h * ((B) - (G));
                }
                else
                {   //  R>B>G
                    h = 6.0 * (-h + 6.0 / 6.0); part = 1.0 + h * (1.0 / minOverMax - 1.0);
                    G = p / Math.Sqrt(Pr / minOverMax / minOverMax + Pb * part * part + Pg);
                    R = (G) / minOverMax; B = (G) + h * ((R) - (G));
                }
            }
            else
            {
                if (h < 1.0 / 6.0)
                {   //  R>G>B
                    h = 6.0 * (h - 0.0 / 6.0); R = Math.Sqrt(p * p / (Pr + Pg * h * h)); G = (R) * h; B = 0.0;
                }
                else if (h < 2.0 / 6.0)
                {   //  G>R>B
                    h = 6.0 * (-h + 2.0 / 6.0); G = Math.Sqrt(p * p / (Pg + Pr * h * h)); R = (G) * h; B = 0.0;
                }
                else if (h < 3.0 / 6.0)
                {   //  G>B>R
                    h = 6.0 * (h - 2.0 / 6.0); G = Math.Sqrt(p * p / (Pg + Pb * h * h)); B = (G) * h; R = 0.0;
                }
                else if (h < 4.0 / 6.0)
                {   //  B>G>R
                    h = 6.0 * (-h + 4.0 / 6.0); B = Math.Sqrt(p * p / (Pb + Pg * h * h)); G = (B) * h; R = 0.0;
                }
                else if (h < 5.0 / 6.0)
                {   //  B>R>G
                    h = 6.0 * (h - 4.0 / 6.0); B = Math.Sqrt(p * p / (Pb + Pr * h * h)); R = (B) * h; G = 0.0;
                }
                else
                {   //  R>B>G
                    h = 6.0 * (-h + 6.0 / 6.0); R = Math.Sqrt(p * p / (Pr + Pb * h * h)); B = (R) * h; G = 0.0;
                }
            }
        }

    }
}
