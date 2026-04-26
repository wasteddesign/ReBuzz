using System.Collections.Generic;
using System.Windows.Controls;

namespace EnvelopeBlock
{
    /// <summary>
    /// Hosts all envelopes for Wave Canvas instance.
    /// </summary>
    class Envelopes : Canvas
    {
        private EnvelopeBlockMachine envelopeBlockMachine;
        private EnvelopeCanvas hostCanvas;
        private double drawLengthInSeconds;
        public Envelopes(EnvelopeBlockMachine ebm, EnvelopeCanvas parentCanvas)
        {
            this.envelopeBlockMachine = ebm;

            this.Width = parentCanvas.Width;
            this.Height = parentCanvas.Height;

            Canvas.SetLeft(this, 0);
            Canvas.SetTop(this, 0);

            HostCanvas = parentCanvas;

            for (int i = 0; i < EnvelopeBlockMachine.MAX_ENVELOPE_BOX_PARAMS; i++)
            {
                EnvelopeLayer el = new EnvelopeLayer(Width, Height);
                el.DrawLengthInSeconds = this.DrawLengthInSeconds;
                el.Init(envelopeBlockMachine, this, i, parentCanvas.LayoutMode);
                this.Children.Add(el);

                parentCanvas.ContextMenu.Items.Insert(i, el.CreateEnvelopeMenu());
            }
            
            Loaded += Envelopes_Loaded;
            Unloaded += Envelopes_Unloaded;

            //Draw();
        }

        private void Envelopes_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            HostCanvas.SizeChanged += ParentCanvas_SizeChanged;
        }

        private void Envelopes_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            HostCanvas.SizeChanged -= ParentCanvas_SizeChanged;
        }

        public void UpdateMenus()
        {
            for (int i = 0; i < EnvelopeBlockMachine.MAX_ENVELOPE_BOX_PARAMS; i++)
            {
                EnvelopeLayer el = (EnvelopeLayer)Children[i];

                HostCanvas.ContextMenu.Items.RemoveAt(i);
                HostCanvas.ContextMenu.Items.Insert(i, el.CreateEnvelopeMenu());
            }
        }

        private void ParentCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            this.Width = ((Canvas)sender).Width;
            this.Height = ((Canvas)sender).Height;
            foreach (EnvelopeLayer el in Children)
            {
                el.Height = Height;
                el.Width = Width;
            }
            Draw();
        }


        public void Draw()
        {
            foreach (EnvelopeLayer el in Children)
            {
                el.Draw();
            }
        }

        public EnvelopeCanvas HostCanvas { get => hostCanvas; set => hostCanvas = value; }
        public double DrawLengthInSeconds
        {
            get => drawLengthInSeconds;
            set
            {
                drawLengthInSeconds = value;

                foreach (EnvelopeLayer el in Children)
                {
                    el.DrawLengthInSeconds = drawLengthInSeconds;
                }
            }
        }

        public EnvelopeLayer SelectedEnvelopeLayer { get; private set; }

        public List<EnvelopeLayer> VisbleEnvelopes()
        {
            List<EnvelopeLayer> visibleEnvelopes = new List<EnvelopeLayer>();
            foreach (EnvelopeLayer el in Children)
            {
                if (el.EnvelopeVisible)
                    visibleEnvelopes.Add(el);
            }
            return visibleEnvelopes;
        }

        internal void SelectEnvelope(EnvelopeLayer envelopeLayer)
        {
            this.SelectedEnvelopeLayer = envelopeLayer;
        }

        internal bool IsSelectedLayer(EnvelopeLayer envelopeLayer)
        {
            bool ret = false;

            List<EnvelopeLayer> visibleEnvelopes = VisbleEnvelopes();
            if (visibleEnvelopes.Count == 1 && visibleEnvelopes[0] == envelopeLayer)
            {
                ret = true;
            }
            else if (envelopeLayer.EnvelopeVisible && SelectedEnvelopeLayer == envelopeLayer)
            {
                ret = true;
            }

            return ret;
        }
    }
}
