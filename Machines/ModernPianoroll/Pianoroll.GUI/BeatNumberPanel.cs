using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pianoroll.GUI
{
    public class BeatNumberPanel : Border
    {
        private class BNVisual : FrameworkElement
        {
            Editor editor;
            const int width = 38;

            static Typeface font = new Typeface("Verdana");
            static CultureInfo cultureInfo = CultureInfo.GetCultureInfo("en-us");

            public BNVisual(Editor editor)
            {
                this.editor = editor;
            }

            protected override void OnRender(DrawingContext dc)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                PatternDimensions pd = editor.PatDim;

                Brush br = (Brush)editor.FindResource("BeatNumberBackgroundBrush");
                Brush lbr = (Brush)editor.FindResource("BeatNumberLineBrush");
                Brush tbr = (Brush)editor.FindResource("BeatNumberTextBrush");
                br.Freeze();
                lbr.Freeze();
                tbr.Freeze();

                dc.DrawRectangle(br, null, new Rect(0, 0, width, pd.Height));

                for (int beat = 0; beat <= pd.BeatCount; beat++)
                {
                    dc.DrawRectangle(lbr, null, new Rect(0, beat * pd.BeatHeight, width, 1));

                }

                for (int beat = 0; beat < pd.BeatCount; beat++)
                {
                    double y = beat * pd.BeatHeight;

                    FormattedText ft = new FormattedText(
                        beat.ToString(),
                        cultureInfo,
                        FlowDirection.LeftToRight,
                        font,
                        10, tbr);

                    ft.TextAlignment = TextAlignment.Center;
                    ft.MaxTextWidth = width;

                    dc.DrawText(ft, new Point(0, y));


                }


                sw.Stop();
                editor.PianorollMachine.WriteDC(string.Format("BeatNumberPanel {0}ms", sw.ElapsedMilliseconds));
            }

            protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
            {
                // dont want focus
                e.Handled = true;
                //                base.OnMouseDown(e);
            }
        }


        Editor ped;
        Canvas canvas;
        public ScrollViewer sv;
        BNVisual bnvisual;

        public double vo { get { return VisualOffset.Y; } }

        public BeatNumberPanel(Editor ped)
        {
            this.ped = ped;
            sv = new ScrollViewer();
            this.Child = sv;
            this.BorderBrush = Brushes.Black;
            this.BorderThickness = new Thickness(1);
            this.Margin = new Thickness(4, 0, 4, 4);
            this.SnapsToDevicePixels = true;



            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;

            canvas = new Canvas();
            canvas.Background = (Brush)ped.FindResource("BeatNumberWindowBackgroundBrush");
            sv.Content = canvas;

            sv.LayoutUpdated += new EventHandler(BeatNumberPanel_LayoutUpdated);
            canvas.MouseDown += new System.Windows.Input.MouseButtonEventHandler(canvas_MouseDown);

            UpdatePatternDimensions();
        }

        void canvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        void BeatNumberPanel_LayoutUpdated(object sender, EventArgs e)
        {
            Canvas.SetTop(canvas.Children[0], Math.Floor(sv.ViewportHeight / 2));
            canvas.Height = ped.PatDim.Height + sv.ViewportHeight;
        }

        public void UpdatePatternDimensions()
        {
            bnvisual = new BNVisual(ped);
            canvas.Children.Clear();
            canvas.Children.Add(bnvisual);
        }

        public void ThemeChanged()
        {
            bnvisual.InvalidateVisual();
        }
    }
}
