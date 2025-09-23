using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WDE.AudioBlock
{
    public enum ViewOrientationMode
    {
        Vertical,
        Horizontal
    }

    class WaveView : Grid
    {
        AudioBlock audioBlockMachine;
        StackPanel spWaves;
        bool updateWaveGraph = false;
        DispatcherTimer zoomTimer;
        DispatcherTimer waveViewResizeTimer;
        Slider sliderZoom;
        Slider sliderWindow;
        private double waveDefaultWidthForVerticalWave;
        private double waveDefaultHeigthForVerticalWave;
        private double waveDefaultWidthForHorizontalWave;
        private double waveDefaultHeigthForHorizontalWave;

        private double waveMinWidthForHorizontalWave;

        private ScrollViewer scrollViewer;
        private ViewOrientationMode viewMode = ViewOrientationMode.Vertical;

        private static GridLength TimeLineGridLenghtWidth = new GridLength(56);
        private static GridLength TimeLineGridLenghtHeigth = new GridLength(30);
        double TimeLineCanvasHorizontalHeight = 40;
        double TimeLineCanvasVerticalWidth = 56;

        RowDefinition gridTimeLineRow;
        ColumnDefinition gridScrollViewColTimeLine;
        TimeLineCanvas timeLineCanvasVertical;
        TimeLineCanvas timeLineCanvasHorizontal;
        Grid gridControls;

        public double WaveDefaultWidthForVerticalWave { get => waveDefaultWidthForVerticalWave; set => waveDefaultWidthForVerticalWave = value; }
        public double WaveDefaultHeightForVerticalWave { get => waveDefaultHeigthForVerticalWave; set => waveDefaultHeigthForVerticalWave = value; }
        public double WaveDefaultWidthForHorizontalWave { get => waveDefaultWidthForHorizontalWave; set => waveDefaultWidthForHorizontalWave = value; }
        public double WaveDefaultHeigthForHorizontalWave { get => waveDefaultHeigthForHorizontalWave; set => waveDefaultHeigthForHorizontalWave = value; }
        public bool IsShownBefore { get; internal set; }
        public StackPanel SpWaves { get => spWaves; set => spWaves = value; }
        public double WaveMinWidthForHorizontalWave { get => waveMinWidthForHorizontalWave; set => waveMinWidthForHorizontalWave = value; }
        public Slider SliderZoom { get => sliderZoom; set => sliderZoom = value; }
        public Slider SliderWindow { get => sliderWindow; set => sliderWindow = value; }

        public WaveView(AudioBlock audioBlockMachine, double waveDefaultWidth, double wavedefaultHeight)
        {
            this.audioBlockMachine = audioBlockMachine;

            WaveDefaultWidthForVerticalWave = waveDefaultWidth;
            WaveDefaultHeightForVerticalWave = wavedefaultHeight;
            WaveDefaultWidthForHorizontalWave = wavedefaultHeight;
            WaveDefaultHeigthForHorizontalWave = waveDefaultWidth;

            WaveMinWidthForHorizontalWave = wavedefaultHeight;

            IsShownBefore = false;

            this.HorizontalAlignment = HorizontalAlignment.Stretch;
            this.VerticalAlignment = VerticalAlignment.Stretch;
            this.Margin = new Thickness(8);

            zoomTimer = new DispatcherTimer();
            zoomTimer.Tick += DispatcherZoomTimer_Tick;

            waveViewResizeTimer = new DispatcherTimer();
            waveViewResizeTimer.Tick += DispatcherTimer_Window_Resize_Tick;

            gridControls = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            ColumnDefinition gridCol1 = new ColumnDefinition() { Width = new GridLength(40) };
            ColumnDefinition gridCol2 = new ColumnDefinition() { };
            gridControls.RowDefinitions.Add(new RowDefinition());
            gridControls.RowDefinitions.Add(new RowDefinition());
            gridControls.ColumnDefinitions.Add(gridCol1);
            gridControls.ColumnDefinitions.Add(gridCol2);

            SliderZoom = new Slider() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0), Minimum = 0.5, Maximum = 10, Value = 10 };
            SliderZoom.ValueChanged += SliderZoom_ValueChanged;
            SliderZoom.ToolTipOpening += Slider_ToolTipOpening;
            SliderZoom.ToolTip = "";

            SliderWindow = new Slider { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0), Minimum = 0.0, Maximum = 10, Value = 0 };
            SliderWindow.ValueChanged += SliderWindow_ValueChanged;
            SliderWindow.ToolTipOpening += SliderWindow_ToolTipOpening;
            SliderWindow.ToolTip = "";

            Button btSwitchHorVer = new Button() { Margin = new Thickness(0, 0, 8, -10), Content = new TextBlock() { Text = "↻", FontSize = 24, Margin = new Thickness(0, -6, 0, 0), Padding = new Thickness(0) }, Height = 30 };
            btSwitchHorVer.Click += BtSwitchHorVer_Click;
            btSwitchHorVer.ToolTip += "Switch between horizontal and vertical views.";

            Grid.SetColumn(btSwitchHorVer, 0);
            Grid.SetColumn(SliderZoom, 1);
            gridControls.Children.Add(btSwitchHorVer);
            gridControls.Children.Add(SliderZoom);
            Grid.SetRow(SliderWindow, 1);
            Grid.SetColumn(SliderWindow, 1);
            gridControls.Children.Add(SliderWindow);

            RowDefinition gridControlsRow = new RowDefinition();
            gridControlsRow.Height = new GridLength(55);
            this.RowDefinitions.Add(gridControlsRow);

            gridTimeLineRow = new RowDefinition();
            gridTimeLineRow.Height = new GridLength(0);
            this.RowDefinitions.Add(gridTimeLineRow);

            RowDefinition gridWavesRow = new RowDefinition();
            this.RowDefinitions.Add(gridWavesRow);

            scrollViewer = new ScrollViewer()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                //Height = 500,
                //Width = 630
            };

            SpWaves = new StackPanel() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Orientation = Orientation.Horizontal, Margin = new Thickness(0) };
            scrollViewer.Content = SpWaves;
            scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;

            timeLineCanvasHorizontal = new TimeLineCanvas() { HorizontalAlignment = HorizontalAlignment.Left };
            timeLineCanvasHorizontal.ViewMode = ViewOrientationMode.Horizontal;
            timeLineCanvasHorizontal.Width = scrollViewer.Width;
            timeLineCanvasHorizontal.Height = 0;
            timeLineCanvasHorizontal.TimeScale = SliderZoom.Value;
            timeLineCanvasHorizontal.UpdateTimeLine();

            timeLineCanvasVertical = new TimeLineCanvas() { VerticalAlignment = VerticalAlignment.Top };
            timeLineCanvasVertical.Width = 0;
            timeLineCanvasVertical.Height = scrollViewer.Height;
            timeLineCanvasVertical.TimeScale = SliderZoom.Value;
            timeLineCanvasVertical.UpdateTimeLine();

            // Add another Grid for TimeLine and scrollViewer
            Grid gridScrollView = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            gridScrollViewColTimeLine = new ColumnDefinition() { Width = TimeLineGridLenghtWidth };
            ColumnDefinition gridScrollViewColContent = new ColumnDefinition() { };
            gridScrollView.ColumnDefinitions.Add(gridScrollViewColTimeLine);
            gridScrollView.ColumnDefinitions.Add(gridScrollViewColContent);
            Grid.SetColumn(timeLineCanvasVertical, 0);
            Grid.SetColumn(scrollViewer, 1);
            gridScrollView.Children.Add(timeLineCanvasVertical);
            gridScrollView.Children.Add(scrollViewer);

            Grid.SetRow(gridControls, 0);
            Grid.SetRow(timeLineCanvasHorizontal, 1);
            Grid.SetRow(gridScrollView, 2);
            this.Children.Add(gridControls);
            this.Children.Add(timeLineCanvasHorizontal);
            this.Children.Add(gridScrollView);
            this.SizeChanged += WaveView_SizeChanged;
            scrollViewer.SizeChanged += ScrollViewer_SizeChanged;

            if ((int)this.viewMode != audioBlockMachine.MachineState.WaveViewOrientation)
            {
                SwitchViewMode();
            }

            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
            {
                if (audioBlockMachine.MachineState.AudioBlockInfoTable[i].WavetableIndex != -1 && audioBlockMachine.MachineState.AudioBlockInfoTable[i].Pattern != "")
                {
                    Brush brush = Utils.GetBrushForPattern(audioBlockMachine, i);
                    int index = i;
                    Task.Factory.StartNew(() =>
                    {
                        if (!audioBlockMachine.MachineClosing)
                            this.AddWave(index, brush).UpdateGraph();
                    }, CancellationToken.None, TaskCreationOptions.None, uiScheduler);
                }
            }

            this.Unloaded += (sender, e) =>
            {
                
                waveViewResizeTimer.Stop();
                zoomTimer.Stop();
                audioBlockMachine.UpdateWaveGraph -= AudioBlockMachine_UpdateWaveGraph;
            };
        }

        internal void UpdateWaveHeights(double height)
        {
            foreach (var v in SpWaves.Children)
            {
                WaveCanvas wc = (WaveCanvas)v;
                if (viewMode == ViewOrientationMode.Vertical)
                {
                    wc.Width = height;
                }
                else
                {
                    wc.Height = height;
                }
            }
            ForceUpdateCanvases();
        }

        private void DispatcherTimer_Window_Resize_Tick(object sender, EventArgs e)
        {
            double canvasWidth = this.ActualWidth - 20;
            double canvasHeight = this.ActualHeight - 20;

            if (canvasWidth >= WaveDefaultWidthForHorizontalWave && viewMode == ViewOrientationMode.Horizontal)
            {
                WaveDefaultWidthForHorizontalWave = this.ActualWidth - 20;
                for (int i = 0; i < SpWaves.Children.Count; i++)
                {
                    WaveCanvas wc = (WaveCanvas)SpWaves.Children[i];

                    wc.Width = WaveDefaultWidthForHorizontalWave;
                    wc.UpdateGraph();
                }
                UpdateTimeLines();
            }
            else if (canvasHeight >= WaveDefaultHeightForVerticalWave && viewMode == ViewOrientationMode.Vertical)
            {
                WaveDefaultHeightForVerticalWave = this.ActualHeight - 60;
                for (int i = 0; i < SpWaves.Children.Count; i++)
                {
                    WaveCanvas wc = (WaveCanvas)SpWaves.Children[i];

                    wc.Height = WaveDefaultHeightForVerticalWave;
                    wc.UpdateGraph();
                }
                UpdateTimeLines();
            }
            waveViewResizeTimer.Stop();
        }

        private void SliderWindow_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            SliderWindow.ToolTip = "Wave View Position: " + string.Format("{0:0.0}", SliderWindow.Value) + " Seconds.";
        }

        private void SliderWindow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            timeLineCanvasVertical.SlidingWindowOffsetSeconds = ((Slider)sender).Value;
            timeLineCanvasHorizontal.SlidingWindowOffsetSeconds = ((Slider)sender).Value;
            timeLineCanvasHorizontal.UpdateTimeLine();
            timeLineCanvasVertical.UpdateTimeLine();

            for (int i = 0; i < SpWaves.Children.Count; i++)
            {
                WaveCanvas wc = (WaveCanvas)SpWaves.Children[i];
                wc.SlidingWindowOffsetSeconds = ((Slider)sender).Value;
            }

            UpdateCanvasPlayPos();

            zoomTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            zoomTimer.Start();
        }

        private void SliderZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            timeLineCanvasVertical.TimeScale = SliderZoom.Value;
            timeLineCanvasVertical.UpdateTimeLine();
            timeLineCanvasHorizontal.TimeScale = SliderZoom.Value;
            timeLineCanvasHorizontal.UpdateTimeLine();

            SliderWindow.Maximum = SliderZoom.Maximum - SliderZoom.Value;
            UpdateCanvasPlayPos();

            zoomTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            zoomTimer.Start();
        }

        private void UpdateCanvasPlayPos()
        {
            for (int i = 0; i < SpWaves.Children.Count; i++)
            {
                WaveCanvas wc = (WaveCanvas)SpWaves.Children[i];
                wc.UpdatePlayLine();
            }
        }

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // ToDO Get WaveView Content Width
            UpdateTimeLines();
        }

        public void UpdateTimeLines()
        {
            timeLineCanvasHorizontal.Width = scrollViewer.ActualWidth;
            timeLineCanvasHorizontal.WaveCanvasLengthInPixels = WaveDefaultWidthForHorizontalWave;
            if (scrollViewer.VerticalScrollBarVisibility == ScrollBarVisibility.Visible)
                timeLineCanvasHorizontal.Width -= SystemParameters.VerticalScrollBarWidth;
            timeLineCanvasHorizontal.UpdateTimeLine();

            timeLineCanvasVertical.Height = scrollViewer.ActualHeight;
            if (scrollViewer.ActualHeight > WaveDefaultHeightForVerticalWave)
                timeLineCanvasVertical.Height = WaveDefaultHeightForVerticalWave;
            timeLineCanvasVertical.WaveCanvasLengthInPixels = WaveDefaultHeightForVerticalWave;
            if (scrollViewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Visible)
                timeLineCanvasVertical.Height -= SystemParameters.HorizontalScrollBarHeight;
            timeLineCanvasVertical.UpdateTimeLine();
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            timeLineCanvasVertical.Offset = e.VerticalOffset;
            timeLineCanvasVertical.UpdateTimeLine();

            timeLineCanvasHorizontal.Offset = e.HorizontalOffset;
            timeLineCanvasHorizontal.UpdateTimeLine();
        }

        private void Slider_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            SliderZoom.ToolTip = "Wave View Length: " + string.Format("{0:0.0}", SliderZoom.Value) + " Seconds.";
        }

        private void BtSwitchHorVer_Click(object sender, RoutedEventArgs e)
        {
            SwitchViewMode();
        }

        private void SwitchViewMode()
        {
            if (viewMode == ViewOrientationMode.Vertical)
            {
                viewMode = ViewOrientationMode.Horizontal;
                SpWaves.Orientation = Orientation.Vertical;

                gridTimeLineRow.Height = TimeLineGridLenghtHeigth;
                timeLineCanvasHorizontal.Height = TimeLineCanvasHorizontalHeight;

                gridScrollViewColTimeLine.Width = new GridLength(0);
                timeLineCanvasVertical.Width = 0;

                TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

                for (int i = 0; i < SpWaves.Children.Count; i++)
                {
                    WaveCanvas wc = (WaveCanvas)SpWaves.Children[i];
                    wc.ChangeOrientation(ViewOrientationMode.Horizontal);
                    WaveDefaultWidthForHorizontalWave = WaveDefaultWidthForHorizontalWave < WaveMinWidthForHorizontalWave ? WaveMinWidthForHorizontalWave : WaveDefaultWidthForHorizontalWave;
                    WaveDefaultWidthForHorizontalWave = WaveDefaultWidthForHorizontalWave < this.ActualWidth - 20 ? this.ActualWidth - 20 : WaveDefaultWidthForHorizontalWave;
                    wc.Width = WaveDefaultWidthForHorizontalWave;

                    Task.Factory.StartNew(() =>
                    {
                        wc.UpdateGraph();
                    }, CancellationToken.None, TaskCreationOptions.None, uiScheduler);
                }
            }
            else
            {
                viewMode = ViewOrientationMode.Vertical;
                SpWaves.Orientation = Orientation.Horizontal;

                gridTimeLineRow.Height = new GridLength(0);
                timeLineCanvasHorizontal.Height = 0;

                gridScrollViewColTimeLine.Width = TimeLineGridLenghtWidth;
                timeLineCanvasVertical.Width = TimeLineCanvasVerticalWidth;

                TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

                for (int i = 0; i < SpWaves.Children.Count; i++)
                {
                    WaveCanvas wc = (WaveCanvas)SpWaves.Children[i];
                    wc.ChangeOrientation(ViewOrientationMode.Vertical);
                    wc.Height = WaveDefaultHeightForVerticalWave;
                    Task.Factory.StartNew(() =>
                    {
                        wc.UpdateGraph();
                    }, CancellationToken.None, TaskCreationOptions.None, uiScheduler);
                }
            }

            this.UpdateHeight(audioBlockMachine.AudioBlockGUI.gridMain.ActualHeight);

            audioBlockMachine.MachineState.WaveViewOrientation = (int)viewMode;
        }

        private void WaveView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            scrollViewer.Width = this.Width;

            if ((this.ActualWidth > AudioBlock.AUDIO_BLOCK_GUI_WAVE_VIEW_MIN_WIDTH && viewMode == ViewOrientationMode.Horizontal && e.WidthChanged) ||
                (this.ActualHeight > AudioBlock.AUDIO_BLOCK_GUI_WAVE_VIEW_MIN_WIDTH && viewMode == ViewOrientationMode.Vertical && e.HeightChanged))
            {
                waveViewResizeTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
                waveViewResizeTimer.Start();
            }
        }

        private void DispatcherZoomTimer_Tick(object sender, EventArgs e)
        {
            zoomTimer.Stop();

            for (int i = 0; i < SpWaves.Children.Count; i++)
            {
                WaveCanvas wc = (WaveCanvas)SpWaves.Children[i];
                wc.PatternLengthInSeconds = SliderZoom.Value;
                wc.UpdateGraph();
            }
        }

        /// <summary>
        /// Call this after audioBlockMachine != null;
        /// </summary>
        public void Init()
        {
            audioBlockMachine.UpdateWaveGraph += AudioBlockMachine_UpdateWaveGraph;
        }

        /// <summary>
        /// This event is raised by AudiuoBlock when something is changed in audio block row. Not that WaveCanvas handles all drawing changes.
        /// We are more interested in removing/adding waves. Remove is not expensive so we can do it here.
        /// </summary>
        /// <param name="ab"></param>
        /// <param name="e"></param>
        private void AudioBlockMachine_UpdateWaveGraph(AudioBlock ab, EventArgs e)
        {
            EventArgsWaveUpdate eau = (EventArgsWaveUpdate)e;

            updateWaveGraph = true;

            // Removed? This is fast so let's do it here.
            if (audioBlockMachine.MachineState.AudioBlockInfoTable[eau.AudioBlockIndex].Pattern == "" || audioBlockMachine.MachineState.AudioBlockInfoTable[eau.AudioBlockIndex].WavetableIndex == -1)
            {
                WaveCanvas removeCanvas = null;
                foreach (WaveCanvas wc in SpWaves.Children)
                {
                    if (wc.AudioBlockIndex == eau.AudioBlockIndex)
                    {
                        removeCanvas = wc;
                        break;
                    }
                }

                if (removeCanvas != null)
                {
                    SpWaves.Children.Remove(removeCanvas);
                }
            }

            if (SpWaves.Children.Count == 0)
                this.EnableTimeLine(false);

            UpdateWaveViewContent();
            UpdateScale();
        }

        public void ForceUpdateCanvases()
        {
            for (int i = 0; i < SpWaves.Children.Count; i++)
            {
                ((WaveCanvas)SpWaves.Children[i]).UpdateGraph();
                ((WaveCanvas)SpWaves.Children[i]).UpdateGraphFlag = false;
            }
        }

        public void UpdateWaveViewContent()
        {
            if (audioBlockMachine.MachineClosing)
                return;
            
            for (int i = 0; i < SpWaves.Children.Count; i++)
            {
                if (((WaveCanvas)SpWaves.Children[i]).UpdateGraphFlag)
                {
                    ((WaveCanvas)SpWaves.Children[i]).UpdateGraph();
                    ((WaveCanvas)SpWaves.Children[i]).UpdateGraphFlag = false;
                }
            }
            
            // Check if something was added. Modifications are handled in WaveCanvas.
            if (updateWaveGraph)
            {
                updateWaveGraph = false;

                for (int audioBlockIndex = 0; audioBlockIndex < audioBlockMachine.MachineState.AudioBlockInfoTable.Length; audioBlockIndex++)
                {
                    if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern != "" && audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex >= 0)
                    {
                        // Try to find this canvas
                        bool found = false;
                        foreach (WaveCanvas wc in SpWaves.Children)
                        {
                            if (wc.AudioBlockIndex == audioBlockIndex)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {   
                            WaveCanvas wc = AddWave(audioBlockIndex, Utils.GetBrushForPattern(audioBlockMachine, audioBlockIndex));
                            wc.PatternLengthInSeconds = SliderZoom.Value;
                            wc.SlidingWindowOffsetSeconds = SliderWindow.Value;
                            OrganizeWaves();
                        }
                    }
                }
            }
            UpdateScale();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // Only update visual if visible
            if (updateWaveGraph && this.IsVisible)
            {
                updateWaveGraph = false;
                UpdateWaveViewContent(); // Not needed?
            }
        }

        private void OrganizeWaves()
        {
            // Needs to be same than in MachineState
            WaveCanvas[] wcList = new WaveCanvas[SpWaves.Children.Count];

            for (int i = 0; i < SpWaves.Children.Count; i++)
                wcList[i] = (WaveCanvas)SpWaves.Children[i];

            SpWaves.Children.Clear();

            for (int i = 0; i < audioBlockMachine.MachineState.AudioBlockInfoTable.Length; i++)
            {
                for (int j = 0; j < wcList.Length; j++)
                {
                    WaveCanvas wc = wcList[j];
                    if (wc.AudioBlockIndex == i)
                    {
                        SpWaves.Children.Add(wc);
                    }
                }
            }
        }

        public WaveCanvas AddWave(int audioBlockIndex)
        {   
            WaveCanvas waveCanvas = new WaveCanvas(audioBlockMachine, audioBlockIndex) { Margin = new Thickness(0) };
            
            if (viewMode == ViewOrientationMode.Vertical)
            {
                waveCanvas.Width = AudioBlock.Settings.DefaultWaveWidth; // WaveDefaultWidthForVerticalWave; // aveDefaultWidth < scrollViewer.ActualWidth ? scrollViewer.ActualWidth : WaveDefaultWidth;
                waveCanvas.Height = WaveDefaultHeightForVerticalWave;
                waveCanvas.ViewOrientationMode = ViewOrientationMode.Vertical;
            }
            else
            {
                waveCanvas.Height = AudioBlock.Settings.DefaultWaveWidth; // WaveDefaultHeigthForHorizontalWave;
                waveCanvas.Width = WaveDefaultWidthForHorizontalWave;
                waveCanvas.ViewOrientationMode = ViewOrientationMode.Horizontal;
            }
            waveCanvas.PatternLengthInSeconds = SliderZoom.Value;
            if (this.audioBlockMachine.AudioBlockGUI.Machine.ParameterWindow.Resources != null)
            {
                waveCanvas.ContextMenu.Resources.MergedDictionaries.Add(this.audioBlockMachine.AudioBlockGUI.Machine.ParameterWindow.Resources);
            }
            SpWaves.Children.Add(waveCanvas);
            EnableTimeLine(true);
            UpdateScale();
            return waveCanvas;
        }

        private void EnableTimeLine(bool enable)
        {
            if (enable)
            {
                if (viewMode == ViewOrientationMode.Vertical)
                {
                    gridScrollViewColTimeLine.Width = TimeLineGridLenghtWidth;
                    timeLineCanvasVertical.Width = TimeLineCanvasVerticalWidth;
                }
                else
                {
                    gridTimeLineRow.Height = TimeLineGridLenghtHeigth;
                    timeLineCanvasHorizontal.Height = TimeLineCanvasHorizontalHeight;
                }
            }
            // Hide
            else
            {
                gridTimeLineRow.Height = new GridLength(0);
                timeLineCanvasHorizontal.Height = 0;

                gridScrollViewColTimeLine.Width = new GridLength(0);
                timeLineCanvasVertical.Width = 0;
            }
        }

        public WaveCanvas AddWave(int audioBlockIndex, Brush brush)
        {
            WaveCanvas wc = AddWave(audioBlockIndex);
            wc.WaveBrush = brush;
            wc.PatternEditorWave = false;
            return wc;
        }

        internal void SelectedWave(int audioBlockIndex)
        {
            foreach (WaveCanvas wc in SpWaves.Children)
            {
                if (wc.AudioBlockIndex == audioBlockIndex)
                    wc.SelectedWave = true;
                else
                    wc.SelectedWave = false;
            }
        }

        public void UpdateScale()
        {
            double seconds = audioBlockMachine.GetLongestSampleInSecondsWithOffset();
            SliderZoom.Maximum = seconds;

            SliderWindow.Maximum = SliderZoom.Maximum - SliderZoom.Value;
        }

        internal void UpdateHeight(double height)
        {
            if (height > 0)
            {
                scrollViewer.Height = height - 20;

                scrollViewer.Height -= gridControls.ActualHeight;

                if (viewMode == ViewOrientationMode.Horizontal)
                {
                    scrollViewer.Height -= timeLineCanvasHorizontal.Height;
                }

            }
        }
    }
}
