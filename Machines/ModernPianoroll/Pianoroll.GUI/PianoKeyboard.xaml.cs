using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pianoroll.GUI
{
    /// <summary>
    /// Interaction logic for PianoKeyboard.xaml
    /// </summary>
    public partial class PianoKeyboard : UserControl
    {
        public delegate void PianoKeyDelegate(int key);

        private class KeyboardVisual : FrameworkElement
        {
            double height;
            PatternDimensions pd;
            Editor editor;

            public KeyboardVisual(Editor editor)
            {
                this.editor = editor;
                pd = editor.PatDim;

            }


            protected override Size MeasureOverride(Size availableSize)
            {
                return new Size(pd.Width, availableSize.Height == double.PositiveInfinity ? 0 : availableSize.Height);
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                height = finalSize.Height;
                return finalSize;
            }

            protected override void OnRender(DrawingContext dc)
            {
                double by = height * 2.0 / 3.0;

                Brush wkbr = (Brush)editor.FindResource("WhiteKeyBrush");
                Brush bkbr = (Brush)editor.FindResource("BlackKeyBrush");

                dc.DrawRectangle(wkbr, null, new Rect(0, 0, pd.Width, height));

                for (int note = 0; note < pd.NoteCount; note++)
                {
                    int n = note % 12;
                    if (n == 1 || n == 3 || n == 6 || n == 8 || n == 10)
                    {
                        dc.DrawRectangle(bkbr, null, new Rect(note * pd.NoteWidth, 0, pd.NoteWidth, by));
                        //                        dc.DrawRectangle(wkbr, null, new Rect(note * pd.NoteWidth, by, pd.NoteWidth, height - by));
                    }
                    else
                    {
                        //                        dc.DrawRectangle(wkbr, null, new Rect(note * pd.NoteWidth, 0, pd.NoteWidth, height));
                    }

                }

                for (int note = 1; note <= pd.NoteCount; note++)
                {
                    int n = note % 12;

                    double w = 1;

                    //                    double lx = note * pd.NoteWidth;
                    if (n == 1 || n == 3 || n == 6 || n == 8 || n == 10)
                    {
                        dc.DrawRectangle(Brushes.Black, null, new Rect(note * pd.NoteWidth - w / 2, 0, w, by));
                        dc.DrawRectangle(Brushes.Black, null, new Rect((note + 1) * pd.NoteWidth - w / 2, 0, w, by));
                        dc.DrawRectangle(Brushes.Black, null, new Rect(note * pd.NoteWidth - w / 2, by - w / 2, pd.NoteWidth + 1, w));


                        //                      lx += pd.NoteWidth / 2;
                    }
                    else
                    {
                        int wki = 0;
                        switch (n)
                        {
                            case 0: wki = 0; break;
                            case 2: wki = 1; break;
                            case 4: wki = 2; break;
                            case 5: wki = 3; break;
                            case 7: wki = 4; break;
                            case 9: wki = 5; break;
                            case 11: wki = 6; break;
                        }

                        double lx = Math.Floor((note / 12 + wki / 7.0) * pd.NoteWidth * 12.0);

                        if (n == 5 || n == 0)
                            dc.DrawRectangle(Brushes.Black, null, new Rect(lx - w / 2, 0, w, height));
                        else
                            dc.DrawRectangle(Brushes.Black, null, new Rect(lx - w / 2, by, w, height - by));

                    }

                    /*
                    if (n == 5 || n == 0)
                        dc.DrawRectangle(Brushes.Black, null, new Rect(lx - w / 2, 0, w, height));
                    else if (n != 2 && n != 4 && n != 7 && n != 9 && n != 11)
                        dc.DrawRectangle(Brushes.Black, null, new Rect(lx - w / 2, by, w, height - by));
                    */

                }

                /*
                renderCount++;

                dc.DrawText(new FormattedText(
                    renderCount.ToString(),
                    CultureInfo.GetCultureInfo("en-us"),
                    FlowDirection.LeftToRight,
                    new Typeface("Verdana"),
                    8, Brushes.Black),
                    new Point(0, 0));
                */


                int oct = 0;

                for (int note = 0; note < pd.NoteCount; note += 12, oct++)
                {
                    FormattedText ft = new FormattedText(
                        oct.ToString(),
                        CultureInfo.GetCultureInfo("en-us"),
                        FlowDirection.LeftToRight,
                        new Typeface("Verdana"),
                        10, Brushes.Black);

                    ft.TextAlignment = TextAlignment.Center;
                    ft.MaxTextWidth = pd.NoteWidth;

                    dc.DrawText(ft, new Point(note * pd.NoteWidth, 0));
                }

                {
                    FormattedText ft = new FormattedText(
                        "Z",
                        CultureInfo.GetCultureInfo("en-us"),
                        FlowDirection.LeftToRight,
                        new Typeface("Tahoma"),
                        10, Brushes.Black);

                    ft.TextAlignment = TextAlignment.Center;
                    ft.MaxTextWidth = pd.NoteWidth;

                    dc.DrawText(ft, new Point(baseOctave * 12 * pd.NoteWidth, 10));
                }

                if (baseOctave < 9)
                {
                    FormattedText ft = new FormattedText(
                        "Q",
                        CultureInfo.GetCultureInfo("en-us"),
                        FlowDirection.LeftToRight,
                        new Typeface("Tahoma"),
                        10, Brushes.Black);

                    ft.TextAlignment = TextAlignment.Center;
                    ft.MaxTextWidth = pd.NoteWidth;

                    dc.DrawText(ft, new Point((baseOctave + 1) * 12 * pd.NoteWidth, 10));
                }


            }

            new public event PianoKeyDelegate OnKeyDown;
            new public event PianoKeyDelegate OnKeyUp;

            int GetKeyAtPoint(Point p)
            {
                int key = (int)(p.X / pd.NoteWidth);
                double by = height * 2.0 / 3.0;

                if (p.Y >= by)
                {
                    int wki = (int)(p.X / (pd.NoteWidth * 12.0 / 7.0)) % 7;

                    int n = 0;
                    switch (wki)
                    {
                        case 0: n = 0; break;
                        case 1: n = 2; break;
                        case 2: n = 4; break;
                        case 3: n = 5; break;
                        case 4: n = 7; break;
                        case 5: n = 9; break;
                        case 6: n = 11; break;
                    }

                    key = (key / 12) * 12 + n;
                }

                if (key < 0 || key >= pd.NoteCount)
                    return -1;
                else
                    return key;
            }

            bool dragging = false;
            int lastKey = -1;

            protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
            {
                lastKey = GetKeyAtPoint(e.GetPosition(this));
                if (lastKey == -1)
                    return;

                OnKeyDown(lastKey);
                CaptureMouse();

                dragging = true;
                e.Handled = true;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (dragging)
                {
                    int key = GetKeyAtPoint(e.GetPosition(this));
                    if (key != lastKey)
                    {
                        if (lastKey != -1) OnKeyUp(lastKey);

                        lastKey = key;
                        if (key != -1) OnKeyDown(key);
                    }

                    e.Handled = true;
                }
            }

            protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
            {
                if (dragging)
                {
                    if (lastKey != -1) OnKeyUp(lastKey);
                    dragging = false;
                    ReleaseMouseCapture();
                }

            }


            int baseOctave;

            public void SetBaseOctave(int bo)
            {
                if (bo != baseOctave)
                {
                    baseOctave = bo;
                    InvalidateVisual();
                }
            }
        }


        public class ActiveNotePanel : Panel
        {
            public static DependencyProperty NoteProperty =
                DependencyProperty.RegisterAttached("Note",
                typeof(int), typeof(ActiveNotePanel));

            PatternDimensions pd;

            // Default public constructor
            public ActiveNotePanel(PatternDimensions pd)
                : base()
            {
                this.pd = pd;
            }

            // Override the default Measure method of Panel
            protected override Size MeasureOverride(Size availableSize)
            {
                Size childSize = availableSize;
                foreach (UIElement child in InternalChildren)
                    child.Measure(childSize);

                return new Size(
                    availableSize.Width == double.PositiveInfinity ? 0 : availableSize.Width,
                    availableSize.Height == double.PositiveInfinity ? 0 : availableSize.Height);

            }
            protected override Size ArrangeOverride(Size finalSize)
            {
                foreach (UIElement child in InternalChildren)
                {
                    int note = (int)child.GetValue(ActiveNotePanel.NoteProperty);
                    int n = note % 12;

                    double by = finalSize.Height * 2.0 / 3.0;
                    double wh = finalSize.Height - by;

                    double y = 0;
                    double nw = pd.NoteWidth;
                    if (n == 0 || n == 5)
                        nw += pd.NoteWidth / 2;

                    if (n == 4 || n == 11)
                        nw -= pd.NoteWidth / 2;

                    if (n == 0 || n == 2 || n == 4 || n == 5 || n == 7 || n == 9 || n == 11)
                        y = finalSize.Height - wh / 2 - child.DesiredSize.Height / 2;
                    else
                        y = by - wh / 2 - child.DesiredSize.Height / 2;


                    double x = note * pd.NoteWidth + nw / 2 - child.DesiredSize.Width / 2;

                    child.Arrange(new Rect(new Point(x, y), child.DesiredSize));
                }
                return finalSize; // Returns the final Arranged size
            }
        }



        KeyboardVisual kbv;
        public ActiveNotePanel activeNotes;

        Editor ped;

        new public event PianoKeyDelegate KeyDown;
        new public event PianoKeyDelegate KeyUp;

        public PianoKeyboard(Editor ped)
        {
            this.ped = ped;
            InitializeComponent();


            grid.Background = Brushes.DarkGray;
            kbv = new KeyboardVisual(ped);
            kbv.OnKeyDown += new PianoKeyDelegate(kbv_OnKeyDown);
            kbv.OnKeyUp += new PianoKeyDelegate(kbv_OnKeyUp);
            grid.Children.Add(kbv);

            activeNotes = new ActiveNotePanel(ped.PatDim);
            grid.Children.Add(activeNotes);

            RadialGradientBrush rgb = new RadialGradientBrush(Colors.White, Colors.Green);
            rgb.GradientOrigin = new Point(0.33, 0.33);
            rgb.Freeze();

            for (int i = 0; i < ped.PatDim.NoteCount; i++)
            {
                Ellipse e = new Ellipse();
                e.Fill = rgb;
                e.Stroke = Brushes.Black;
                e.StrokeThickness = 0.75;
                e.Width = 13;
                e.Height = 13;
                e.Visibility = Visibility.Collapsed;
                e.IsHitTestVisible = false;
                e.SetValue(ActiveNotePanel.NoteProperty, i);
                activeNotes.Children.Add(e);

            }


        }

        void kbv_OnKeyDown(int key)
        {
            //            Ellipse e = (Ellipse)activeNotes.Children[key];
            //          e.Visibility = Visibility.Visible;
            if (KeyDown != null) KeyDown(key + ped.PatDim.FirstMidiNote);
        }

        void kbv_OnKeyUp(int key)
        {
            //        Ellipse e = (Ellipse)activeNotes.Children[key];
            //      e.Visibility = Visibility.Collapsed;
            if (KeyUp != null) KeyUp(key + ped.PatDim.FirstMidiNote);
        }

        public void PianoKeyDown(int key)
        {
            key -= ped.PatDim.FirstMidiNote;
            if (key < 0 || key >= ped.PatDim.NoteCount)
                return;

            Ellipse e = (Ellipse)activeNotes.Children[key];
            e.Visibility = Visibility.Visible;
        }

        public void PianoKeyUp(int key)
        {
            key -= ped.PatDim.FirstMidiNote;
            if (key < 0 || key >= ped.PatDim.NoteCount)
                return;

            Ellipse e = (Ellipse)activeNotes.Children[key];
            e.Visibility = Visibility.Collapsed;
        }

        public void UpdatePatternDimensions()
        {
            kbv.InvalidateMeasure();
            kbv.InvalidateVisual();
            activeNotes.InvalidateMeasure();
        }

        public bool IsKeyDown(int key)
        {
            key -= ped.PatDim.FirstMidiNote;
            if (key < 0 || key >= ped.PatDim.NoteCount)
                return false;

            Ellipse e = (Ellipse)activeNotes.Children[key];
            return e.Visibility == Visibility.Visible;
        }

        public void SetBaseOctave(int bo)
        {
            kbv.SetBaseOctave(bo);
        }

        public void ThemeChanged()
        {
            kbv.InvalidateVisual();
        }
    }
}
