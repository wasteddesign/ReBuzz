using Box2dNet.Interop;
using System.Collections.Generic;
using System.Numerics;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using BuzzGUI.Common;
using System.Linq;
using ReBuzz.Core;
using BuzzGUI.Interfaces;
using BuzzGUI.Common.DSP;
using System.Windows.Forms;
using System.Windows.Threading;

namespace ReBuzz.Common
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow(ReBuzzCore reBuzz, string build)
        {
            DataContext = this;
            InitializeComponent();

            CreateWorld();

            Loaded += (sender, e) =>
            {
                ps = new ParticleSystem(30, 100, 5, 100, 50, this.particleCanvas, this.partileGrid);
                reBuzz.MasterTap += ReBuzz_MasterTap;
                CreateWorldLimits();
                CreateTextBoxes();

                CompositionTarget.Rendering += CompositionTarget_Rendering;
            };

            MainGrid.PreviewMouseRightButtonDown += (sender, e) =>
            {
                ClearAll();
                CreateWorld();
                CreateWorldLimits();
                CreateTextBoxes();
            };

            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromSeconds(10);
            dt.Tick += (sender, e) =>
            {
                rotateBoxes = !rotateBoxes;
            };
            dt.Start();

            MainGrid.MouseMove += (sender, e) =>
            {
                pMouse = e.GetPosition(this.mainCanvas);
            };

            this.Closed += (sender, e) =>
            {
                dt.Stop();
                reBuzz.MasterTap -= ReBuzz_MasterTap;
                CompositionTarget.Rendering -= CompositionTarget_Rendering;
                ClearAll();
            };

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
fbev, mantratronic, okp, mute, MarCNeT,
Grzegorz Gałęzowski, zeffii...
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

        float maxSample;
        float VUMeterRange = 80f;

        private void ReBuzz_MasterTap(float[] arg1, bool arg2, SongTime arg3)
        {
            if (!arg2) // Mono
            {
                maxSample = Math.Max(maxSample, DSP.AbsMax(arg1) * (1.0f / 32768.0f));
            }
            else
            {
                float[] L = new float[arg1.Length / 2];
                float[] R = new float[arg1.Length / 2];
                for (int i = 0; i < arg1.Length / 2; i++)
                {
                    L[i] = arg1[i * 2];
                    R[i] = arg1[i * 2 + 1];
                }

                maxSample = Math.Max(maxSample, DSP.AbsMax(L) * (1.0f / 32768.0f));
                maxSample = Math.Max(maxSample, DSP.AbsMax(R) * (1.0f / 32768.0f));
            }
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            ps.ParticleRoamUpdate(pMouse);
            ps.AddOrRemoveParticleLine();
            ps.MoveParticleLine();

            UpdateWorld();
        }

        #region Box2d

        public struct BoxInfo
        {
            public b2BodyId box2BodyId;
            public b2ShapeId box2ShapeId;
            public Rectangle rectangle;
        }

        List<BoxInfo> rectangles = new List<BoxInfo>(1000);
        b2WorldId box2WorldId;
        IEnumerable<Color> rectColors = new HSPAColorProvider(80 + 20, 0, 0.916667,0.7, 0.7, 0.6, 0.6).Colors;
        bool rotateBoxes;
        float angleInRadians = 65 * (float)Math.PI / 180f;

        byte[] logobuffer = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, };
        void ClearAll()
        {
            mainCanvas.Children.Clear();
            rectangles.Clear();
            B2Api.b2DestroyWorld(box2WorldId);
        }

        void CreateWorld()
        {
            // create world
            var worldDef = B2Api.b2DefaultWorldDef();
            //worldDef.gravity = new(0, -9.81f);
            worldDef.gravity = new(0, 0);
            box2WorldId = B2Api.b2CreateWorld(worldDef);
        }

        void UpdateWorld()
        {
            B2Api.b2World_Step(box2WorldId, 1 / 60f, 0);

            float forceDircetion = 1;
            float dist = 50 * 50;

            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                forceDircetion = -1;
                dist = 100 * 100;
            }

            var mousePos = Mouse.GetPosition(mainCanvas);

            var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSample), -VUMeterRange), 0.0);
            var VUMeterLevelL = (db + VUMeterRange) / VUMeterRange;
            maxSample = 0;

            for (int i = 0; i < rectangles.Count; i++)
            {
                var r = rectangles[i];
                var position = B2Api.b2Body_GetPosition(r.box2BodyId);
                var rotation = B2Api.b2Body_GetRotation(r.box2BodyId);
                var velocity = B2Api.b2Body_GetLinearVelocity(r.box2BodyId);

                double mouseX = position.X - mousePos.X - r.rectangle.Width / 2;
                double mouseY = -position.Y - mousePos.Y - r.rectangle.Height / 2;

                double mouseDist = mouseX * mouseX + mouseY * mouseY;

                if (mouseDist < dist)
                {
                    double forceX = (1 - mouseDist / dist) * mouseX;
                    double forceY = (1 - mouseDist / dist) * mouseY;
                    B2Api.b2Body_ApplyForceToCenter(r.box2BodyId, new((float)(forceX * forceDircetion * 1000), (float)forceY * forceDircetion * -1000), true);
                }

                r.rectangle.RenderTransform = new RotateTransform(rotation.GetAngle() * 180 / Math.PI, r.rectangle.Width / 2, r.rectangle.Height / 2);

                byte transparency = 127;
                int tDist = 200 * 200;
                if (mouseDist < tDist)
                {
                    // Update transparency
                    double force = (1 - mouseDist / tDist);
                    transparency = (byte)(127 + Math.Min(VUMeterLevelL * 128 * force, 128));
                }

                if (rotateBoxes)
                {
                    Vector2 center = new Vector2((float)mainCanvas.ActualWidth/2f, (float)-mainCanvas.ActualHeight/2f);
                    var centerDirection = center - position;
                    centerDirection = centerDirection / centerDirection.Length() * 10000;
                    centerDirection = Vector2.Transform(centerDirection, Matrix3x2.CreateRotation(angleInRadians));
                    B2Api.b2Body_ApplyForceToCenter(r.box2BodyId, centerDirection, true);
                }

                var brush = r.rectangle.Fill as SolidColorBrush;
                var color = brush.Color;
                brush.Color = Color.FromArgb(transparency, color.R, color.G, color.B);

                Canvas.SetTop(r.rectangle, -position.Y);
                Canvas.SetLeft(r.rectangle, position.X);
            }
        }

        private void CreateWorldLimits()
        {
            // Define the ground body
            var groundBodyDef = B2Api.b2DefaultBodyDef();
            groundBodyDef.position = new Vector2((float)(mainCanvas.ActualWidth / 2), (float)(-mainCanvas.ActualHeight - 10));
            var groundBody = B2Api.b2CreateBody(box2WorldId, groundBodyDef);

            var shapeDef = B2Api.b2DefaultShapeDef();
            var polygon = B2Api.b2MakeBox((float)(mainCanvas.ActualWidth / 2), (float)(20));
            B2Api.b2CreatePolygonShape(groundBody, shapeDef, polygon);
            
            // Define the right body
            var leftBodyDef = B2Api.b2DefaultBodyDef();
            leftBodyDef.position = new Vector2(-20, (float)(-mainCanvas.ActualHeight / 2));
            var leftBody = B2Api.b2CreateBody(box2WorldId, leftBodyDef);

            shapeDef = B2Api.b2DefaultShapeDef();
            polygon = B2Api.b2MakeBox((float)(20), (float)(mainCanvas.ActualHeight / 2));
            B2Api.b2CreatePolygonShape(leftBody, shapeDef, polygon);
            
            // Define the left body
            var rightBodyDef = B2Api.b2DefaultBodyDef();
            rightBodyDef.position = new Vector2((float)(mainCanvas.ActualWidth + 10), (float)(-mainCanvas.ActualHeight / 2));
            var rightBody = B2Api.b2CreateBody(box2WorldId, rightBodyDef);

            shapeDef = B2Api.b2DefaultShapeDef();
            polygon = B2Api.b2MakeBox((float)(20), (float)(mainCanvas.ActualHeight / 2));
            B2Api.b2CreatePolygonShape(rightBody, shapeDef, polygon);
            
            // Define the top body
            var topBodyDef = B2Api.b2DefaultBodyDef();
            topBodyDef.position = new Vector2((float)(mainCanvas.ActualWidth / 2), 20);
            var topBody = B2Api.b2CreateBody(box2WorldId, topBodyDef);

            shapeDef = B2Api.b2DefaultShapeDef();
            polygon = B2Api.b2MakeBox((float)(mainCanvas.ActualWidth / 2), (float)(20));
            B2Api.b2CreatePolygonShape(topBody, shapeDef, polygon);
        }

        readonly float boxWidth = 9;
        readonly float boxHeight = 9;
        readonly float boxRadius = 1;

        void CreateTextBoxes()
        {
            Random random = new Random();
            float scale = 700.0f / 80.0f;

            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 80; x++)
                {
                    if (logobuffer[y * 80 + x] == 1)
                    {
                        // add body ...
                        var bodyDef = B2Api.b2DefaultBodyDef();
                        bodyDef.type = b2BodyType.b2_dynamicBody;
                        bodyDef.position = new((float)(x * scale), (float)(-(y + 8) * scale));
                        bodyDef.enableSleep = false;
                        bodyDef.linearDamping = 1f;
                        var b2BodyId = B2Api.b2CreateBody(box2WorldId, bodyDef);

                        // ... with polygon shape
                        var shapeDef = B2Api.b2DefaultShapeDef();
                        //var polygon = B2Api.b2MakeRoundedBox(boxWidth / 2, boxHeight / 2, boxRadius / 2);
                        var polygon = B2Api.b2MakeBox(boxWidth / 2, boxHeight / 2);
                        var b2ShapeId = B2Api.b2CreatePolygonShape(b2BodyId, in shapeDef, in polygon);

                        var c = rectColors.ElementAt((y + x) % (80+20));
                        Rectangle r = new Rectangle() { Opacity = 0, Width = boxWidth, Height = boxHeight, Fill = new SolidColorBrush(Color.FromArgb(127, c.R, c.G, c.B)), RadiusX = boxRadius, RadiusY = boxRadius };
                        mainCanvas.Children.Add(r);
                        SetInitAnimation(r, (y + x) / 80.0);
                        Canvas.SetTop(r, bodyDef.position.X);
                        Canvas.SetLeft(r, -bodyDef.position.X);

                        rectangles.Add(new BoxInfo { box2BodyId = b2BodyId, box2ShapeId = b2ShapeId, rectangle = r });

                    }
                }
            }
        }

        private void SetInitAnimation(Rectangle rect, double seconds)
        {
            var myDoubleAnimation = new DoubleAnimation();

            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;

            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2));
            myDoubleAnimation.BeginTime = TimeSpan.FromSeconds(seconds);
            myDoubleAnimation.AutoReverse = false;

            Storyboard myStoryboard = new Storyboard();
            myStoryboard.Children.Add(myDoubleAnimation);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Rectangle.OpacityProperty));
            myStoryboard.Begin(rect);
        }
        #endregion

        #region ParticleBackground

        // https://www.programmersought.com/article/13866805821/

        private ParticleSystem ps;
        private Point pMouse = new Point(0, 0);

        public class Particle
        {
            /// <summary>
            /// shape
            /// </summary>
            public Ellipse Shape;
            /// <summary>
            /// coordinates
            /// </summary>
            public Point Position;
            /// <summary>
            /// speed
            /// </summary>
            public System.Windows.Vector Velocity;
            /// <summary>
            /// A collection of particles and line segments
            /// </summary>
            public Dictionary<Particle, Line> ParticleLines { get; set; }
        }

        public class ParticleSystem
        {
            /// <summary>
            /// Number of particles
            /// </summary>
            private int particleCount = 100;

            /// <summary>
            /// Minimum particle size
            /// </summary>
            private static int sizeMin = 5;

            /// <summary>
            /// Maximum particle size
            /// </summary>
            private int sizeMax = 20;

            /// <summary>
            /// Particle movement speed
            /// </summary>
            private int speed = 10;

            /// <summary>
            /// Threshold for marking
            /// </summary>
            private int lineThreshold = 100;

            /// <summary>
            /// Mouse radius
            /// </summary>
            private static int mouseRadius = 50;

            /// <summary>
            /// random number 
            /// </summary>
            private Random random;

            /// <summary>
            /// Particle list
            /// </summary>
            private List<Particle> particles;

            /// <summary>
            /// Particle container
            /// </summary>
            private Canvas containerParticles;

            /// <summary>
            /// Line segment container
            /// </summary>
            private Grid containerLine;


            public ParticleSystem(int _maxRadius, int _particleCount, int _speed, int _lineThreshold, int _mouseRadius, Canvas _containerParticles, Grid _containerLine)
            {
                particleCount = _particleCount;
                speed = _speed;
                sizeMax = _maxRadius;
                lineThreshold = _lineThreshold;
                mouseRadius = _mouseRadius;
                containerLine = _containerLine;
                containerParticles = _containerParticles;
                random = new Random();
                particles = new List<Particle>();
                SpawnParticle();
            }

            /// <summary>
            /// Initialize particles
            /// </summary>
            private void SpawnParticle()
            {
                //Empty the particle queue
                particles.Clear();
                containerLine.Children.Clear();
                containerParticles.Children.Clear();

                //Generate particles
                for (int i = 0; i < particleCount; i++)
                {
                    double size = random.Next(sizeMin, sizeMax + 1);
                    Particle p = new Particle
                    {
                        Shape = new Ellipse
                        {
                            Width = size,
                            Height = size,
                            Stretch = System.Windows.Media.Stretch.Fill,
                            Fill = new SolidColorBrush(Color.FromArgb(125, (byte)random.Next(255), 255, 255)),
                        },
                        Position = new Point(random.Next(0, (int)containerParticles.ActualWidth), random.Next(0, (int)containerParticles.ActualHeight)),
                        Velocity = new System.Windows.Vector(random.Next(-speed, speed) * 0.1, random.Next(-speed, speed) * 0.1),
                        ParticleLines = new Dictionary<Particle, Line>()
                    };
                    particles.Add(p);
                    Canvas.SetLeft(p.Shape, p.Position.X);
                    Canvas.SetTop(p.Shape, p.Position.Y);
                    containerParticles.Children.Add(p.Shape);
                }
            }

            /// <summary>
            /// Particle roaming animation
            /// </summary>
            public void ParticleRoamUpdate(Point mp)
            {
                foreach (Particle p in particles)
                {
                    p.Position.X = p.Position.X + p.Velocity.X;
                    p.Position.Y = p.Position.Y + p.Velocity.Y;

                    if (p.Position.X < p.Shape.Width)
                        p.Position.X = p.Shape.Width;
                    if (p.Position.Y < p.Shape.Height)
                        p.Position.Y = p.Shape.Height;
                    if (p.Position.X > containerParticles.ActualWidth - p.Shape.Width)
                        p.Position.X = containerParticles.ActualWidth - p.Shape.Width;
                    if (p.Position.Y > containerParticles.ActualHeight - p.Shape.Height)
                        p.Position.Y = containerParticles.ActualHeight - p.Shape.Height;

                    //The speed is 0 judgment
                    if (p.Velocity.X == 0) p.Velocity.X = random.Next(-speed, speed) * 0.1;
                    if (p.Velocity.Y == 0) p.Velocity.Y = random.Next(-speed, speed) * 0.1;

                    //Whether it collides with the wall
                    if ((p.Position.X <= p.Shape.Width) || (p.Position.X >= containerParticles.ActualWidth - p.Shape.Width))
                        p.Velocity.X = -p.Velocity.X;
                    if ((p.Position.Y <= p.Shape.Height) || (p.Position.Y >= containerParticles.ActualHeight - p.Shape.Height))
                        p.Velocity.Y = -p.Velocity.Y;

                    //Mouse movement changes particle position
                    //Find the distance from the point to the center of the circle
                    double c = Math.Pow(Math.Pow(mp.X - p.Position.X, 2) + Math.Pow(mp.Y - p.Position.Y, 2), 0.5);
                    if (c < mouseRadius)
                    {
                        p.Position.X = mp.X - ((mp.X - p.Position.X) * mouseRadius / c);
                        p.Position.Y = (p.Position.Y - mp.Y) * mouseRadius / c + mp.Y;
                    }

                    Canvas.SetLeft(p.Shape, p.Position.X);
                    Canvas.SetTop(p.Shape, p.Position.Y);
                }
            }

            /// <summary>
            /// Add or remove lines between particles
            /// </summary>
            public void AddOrRemoveParticleLine()
            {
                for (int i = 0; i < particleCount - 1; i++)
                {
                    for (int j = i + 1; j < particleCount; j++)
                    {
                        Particle p1 = particles[i];
                        double x1 = p1.Position.X + p1.Shape.Width / 2;
                        double y1 = p1.Position.Y + p1.Shape.Height / 2;
                        Particle p2 = particles[j];
                        double x2 = p2.Position.X + p2.Shape.Width / 2;
                        double y2 = p2.Position.Y + p2.Shape.Height / 2;
                        double s = Math.Sqrt((y2 - y1) * (y2 - y1) + (x2 - x1) * (x2 - x1));//The distance between two particles
                        if (s <= lineThreshold)
                        {
                            if (!p1.ParticleLines.ContainsKey(p2))
                            {
                                Line line = new Line()
                                {
                                    X1 = x1,
                                    Y1 = y1,
                                    X2 = x2,
                                    Y2 = y2,
                                    Stroke = new SolidColorBrush(Color.FromArgb(50, (byte)random.Next(255), 255, 255)),
                                };
                                p1.ParticleLines.Add(p2, line);
                                containerLine.Children.Add(line);
                            }
                        }
                        else
                        {
                            if (p1.ParticleLines.ContainsKey(p2))
                            {
                                containerLine.Children.Remove(p1.ParticleLines[p2]);
                                p1.ParticleLines.Remove(p2);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Move the connection between particles
            /// </summary>
            public void MoveParticleLine()
            {
                foreach (Particle p in particles)
                {
                    foreach (var starLine in p.ParticleLines)
                    {
                        Line line = starLine.Value;
                        line.X1 = p.Position.X + p.Shape.Width / 2;
                        line.Y1 = p.Position.Y + p.Shape.Height / 2;
                        line.X2 = starLine.Key.Position.X + starLine.Key.Shape.Width / 2;
                        line.Y2 = starLine.Key.Position.Y + starLine.Key.Shape.Height / 2;
                    }
                }
            }
        }

        #endregion
    }
}
