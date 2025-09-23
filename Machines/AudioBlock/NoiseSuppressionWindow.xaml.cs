using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WDE.AudioBlock
{
    /// <summary>
    /// Interaction logic for NoiseSuppressionWindow.xaml
    /// </summary>
    public partial class NoiseSuppressionWindow : Window, INotifyPropertyChanged
    {
        public class ModelVM
        {
            public string Name { get; }
            public string FilePath { get; }

            public string Description { get; }

            public ModelVM(string name, string filePath, string description)
            {
                Name = name;
                FilePath = filePath;
                Description = description;
            }
        }

        private List<ModelVM> models = new List<ModelVM> {
            new ModelVM("Delault", null, "Default model."),
            new ModelVM("Model1", Global.BuzzPath + "\\Gear\\Generators\\AudioBlockRNNModels\\bd.rnnn", "Voice in a reasonable recording environment. Fans, AC, computers, etc."),
            new ModelVM("Model2", Global.BuzzPath + "\\Gear\\Generators\\AudioBlockRNNModels\\cb.rnnn", "General use in a reasonable recording environment. Fans, AC, computers, etc."),
            new ModelVM("Model3", Global.BuzzPath + "\\Gear\\Generators\\AudioBlockRNNModels\\lq.rnnn", "Voice in a noisy recording environment."),
            new ModelVM("Model4", Global.BuzzPath + "\\Gear\\Generators\\AudioBlockRNNModels\\mp.rnnn", "General use in a noisy recording environment."),
            new ModelVM("Model5", Global.BuzzPath + "\\Gear\\Generators\\AudioBlockRNNModels\\sh.rnnn", "Speech in a reasonable recording environment. Fans, AC, computers, etc.\r\n\r\n Notethat \"speech\" means speech, not other human sounds; laughter, coughing, etc are not included.")
        };

        public event PropertyChangedEventHandler PropertyChanged;

        public List<ModelVM> Models { get => models; }

        private ModelVM selectedModel;
        public ModelVM SelectedModel { get => selectedModel; set { selectedModel = value; PropertyChanged.Raise(this, "SelectedModel"); } }
        public NoiseSuppressionWindow()
        {
            DataContext = this;
            InitializeComponent();

            new WindowInteropHelper(this).Owner = Global.MachineViewHwndSource.Handle;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            this.Loaded += (sender, e) =>
            {
                SelectedModel = models[0];
                PropertyChanged.Raise(this, "Models");
                PropertyChanged.Raise(this, "SelectedModel");
            };

            btRun.Click += BtRun_Click;
        }

        private void BtRun_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
