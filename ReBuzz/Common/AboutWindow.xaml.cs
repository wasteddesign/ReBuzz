using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ReBuzz.Common
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow(string build)
        {
            DataContext = this;
            InitializeComponent();

            AboutText.SelectionBrush = Brushes.Transparent;

            AboutText.SelectionChanged += (sender, e) =>
            {
                e.Handled = true;
            };

            AboutText.Text = "ReBuzz " + build +
@"
(C) " + Properties.Resources.BuildDate.Substring(0, 4) + @" WDE
         
Thank you for choosing ReBuzz
Digital Audio Workstation!
We are grateful to all our
contributors who have helped
us make this software
what it is today.

We encourage users and developers
alike to continue using and
improving ReBuzz.

Let's make music together!

Thanks to
            
Oskari Tammelin
Creator of the original
Jeskola Buzz DAW

clvn - Buzè
Frank Potulski - Polac VST Loaders
Mark Heath - NAudio
Aleksey Vaneev - r8brain-free-src
Mario Guggenberger - Managed Wrapper for LibSampleRate
Ryan Seghers - Spline code
Olli Parviainen - SoundTouch
Jean-Marc Valin - rnnoise
Jagger - rnnoise-windows
Marc Jacobi - VST.net
            
Joachip, unz, IX, retrofox, snowglobe,
fbev, mantratronic, okp, mute, MarCNeT...
For improvements, testing, support, problem
solving and late night brainstorming

Special Thanks to
Microsoft Bing Chat...

...for AI generated examples,
graphics, and generating big chunk of
this about text.";


            int numLines = AboutText.Text.Split('\n').Length;
            double textEndPos = AboutText.FontSize * AboutText.FontFamily.LineSpacing * numLines;
            var sb = FindResource("StoryboardText") as Storyboard;
            ((ThicknessAnimationUsingKeyFrames)sb.Children[0]).KeyFrames[0].Value = new Thickness(0, this.Height - 50, 0, 0);
            ((ThicknessAnimationUsingKeyFrames)sb.Children[0]).KeyFrames[1].Value = new Thickness(0, -textEndPos, 0, 0);
        }
    }
}
