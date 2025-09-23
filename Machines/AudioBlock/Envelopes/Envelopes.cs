using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using static WDE.AudioBlock.AudioBlock;

namespace WDE.AudioBlock
{
    /// <summary>
    /// Hosts all envelopes for Wave Canvas instance.
    /// </summary>
    class Envelopes : Canvas
    {
        private ViewOrientationMode viewOrientationMode = ViewOrientationMode.Vertical;
        private EnvelopeLayerVolume layerVolume;
        private EnvelopeLayerPan layerPan;
        private AudioBlock audioBlock;
        private WaveCanvas hostCanvas;
        private double drawLengthInSeconds;
        public Envelopes(AudioBlock ab, int audioBlockIndex, WaveCanvas parentCanvas)
        {
            this.audioBlock = ab;

            this.Width = parentCanvas.Width;
            this.Height = parentCanvas.Height;

            Canvas.SetLeft(this, 0);
            Canvas.SetTop(this, 0);

            HostCanvas = parentCanvas;
            
            layerVolume = new EnvelopeLayerVolume();
            layerVolume.Init(audioBlock, audioBlockIndex, this);
            layerVolume.LoadData();
            this.Children.Add(layerVolume);

            layerPan = new EnvelopeLayerPan();
            layerPan.Init(audioBlock, audioBlockIndex, this);
            layerPan.LoadData();
            this.Children.Add(layerPan);

            HostCanvas.ContextMenu.Items.Insert(0, layerVolume.CreateEnvelopeMenu());
            HostCanvas.ContextMenu.Items.Insert(1, layerPan.CreateEnvelopeMenu());

            this.Loaded += (s, e) =>
            {
                HostCanvas.SizeChanged += ParentCanvas_SizeChanged;
                InvalidateMeasure();
            };
            
            this.Unloaded += Envelopes_Unloaded;
        }

        private void Envelopes_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ContextMenu != null)
            {
                HostCanvas.ContextMenu.Items.RemoveAt(0);
                HostCanvas.ContextMenu.Items.RemoveAt(0);
            }
            HostCanvas.SizeChanged -= ParentCanvas_SizeChanged;
            Children.Clear();
            layerVolume.Release();
            layerPan.Release(); 
            //audioBlock = null;
        }

        internal void UpdateMenus()
        {
            layerVolume.RemoveMenuCheckedEvents();
            layerPan.RemoveMenuCheckedEvents();
            HostCanvas.ContextMenu.Items.RemoveAt(1);
            HostCanvas.ContextMenu.Items.RemoveAt(0);
            HostCanvas.ContextMenu.Items.Insert(0, layerVolume.CreateEnvelopeMenu());
            HostCanvas.ContextMenu.Items.Insert(1, layerPan.CreateEnvelopeMenu());
        }

        private void ParentCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            this.Width = e.NewSize.Width;
            this.Height = e.NewSize.Height;
            layerVolume.Draw();
            layerPan.Draw();
        }

        internal ViewOrientationMode ViewOrientationMode
        {
            get
            {
                return viewOrientationMode;
            }
            set
            {
                viewOrientationMode = value;

                layerVolume.SetOrientation(viewOrientationMode);
                layerPan.SetOrientation(viewOrientationMode);
            }
        }

        public void Draw()
        {
            layerVolume.Draw();
            layerVolume.UpdatePolyLinePath();
            layerPan.Draw();
            layerPan.UpdatePolyLinePath();
        }

        public WaveCanvas HostCanvas { get => hostCanvas; set => hostCanvas = value; }
        public double DrawLengthInSeconds
        {
            get => drawLengthInSeconds;
            set
            {
                drawLengthInSeconds = value;
                layerVolume.DrawLengthInSeconds = drawLengthInSeconds;
                layerPan.DrawLengthInSeconds = drawLengthInSeconds;
            }
        }

        public List<EEnvelopeType> VisbleEnvelopes()
        {
            List<EEnvelopeType> visibleEnvelopes = new List<EEnvelopeType>();
            if (layerVolume.EnvelopeVisible)
            {
                EEnvelopeType vol = EEnvelopeType.Volume;
                visibleEnvelopes.Add(vol);
            }
            if (layerPan.EnvelopeVisible)
            {
                EEnvelopeType pan = EEnvelopeType.Pan;
                visibleEnvelopes.Add(pan);
            }
            return visibleEnvelopes;
        }

        public EEnvelopeType SelectedEnvelopeLayer { get; private set; }

        internal void SelectPanEnvelope()
        {
            this.SelectedEnvelopeLayer = EEnvelopeType.Pan;
        }

        internal void SelectVolEnvelope()
        {
            this.SelectedEnvelopeLayer = EEnvelopeType.Volume;
        }

        internal bool IsSelectedLayer(EEnvelopeType type)
        {
            bool ret = false;

            List<EEnvelopeType> visibleEnvelopes = VisbleEnvelopes();
            if (visibleEnvelopes.Count == 1 && visibleEnvelopes[0] == type)
            {
                ret = true;
            }
            else if (type == EEnvelopeType.Volume && layerVolume.EnvelopeVisible && SelectedEnvelopeLayer == EEnvelopeType.Volume)
            {
                ret = true;
            }
            else if (type == EEnvelopeType.Pan && layerPan.EnvelopeVisible && SelectedEnvelopeLayer == EEnvelopeType.Pan)
            {
                ret = true;
            }

            return ret;
        }
    }
}
