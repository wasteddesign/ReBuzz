using BuzzGUI.Common;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace BuzzGUI.MachineView
{
    /// <summary>
    /// Interaction logic for CreateTemplateWindow.xaml
    /// </summary>
    public partial class CreateTemplateWindow : Window
    {
        public IEnumerable<IMachine> Machines { get; private set; }
        public IEnumerable<IMachineGroup> Groups { get; }
        public IEnumerable<string> ExistingTemplates { get; private set; }
        public string TemplateName { get; private set; }

        public static TemplatePatternMode PatternMode { get; set; }
        public static TemplateWavetableMode WavetableMode { get; set; }

        static CreateTemplateWindow()
        {
            PatternMode = TemplatePatternMode.PatternsAndSequences;
            WavetableMode = TemplateWavetableMode.NoWavetable;
        }

        public CreateTemplateWindow(IEnumerable<IMachine> machines, IEnumerable<IMachineGroup> groups, IEnumerable<string> existing)
        {
            Machines = machines;
            Groups = groups;
            ExistingTemplates = existing;

            InitializeComponent();

            noPatterns.IsChecked = PatternMode == TemplatePatternMode.NoPatterns;
            includePatterns.IsChecked = PatternMode == TemplatePatternMode.PatternsOnly;
            includeSequences.IsChecked = PatternMode == TemplatePatternMode.PatternsAndSequences;

            noWaves.IsChecked = WavetableMode == TemplateWavetableMode.NoWavetable;
            waveRefs.IsChecked = WavetableMode == TemplateWavetableMode.WaveRefsOnly;
            waves.IsChecked = WavetableMode == TemplateWavetableMode.WaveFiles;

            var generators = Machines.Where(m => m.DLL.Info.Type == MachineType.Generator && !m.IsControlMachine);
            var effects = Machines.Where(m => m.DLL.Info.Type == MachineType.Effect && !m.IsControlMachine);

            if (generators.Count() == 1)
                name.Text = Enumerable.Range(1, 10000).Select(n => generators.First().DLL.Info.ShortName + " - " + n.ToString()).Where(x => !ExistingTemplates.Contains(x)).First();
            else if (generators.Count() == 0 && effects.Count() == 1)
                name.Text = Enumerable.Range(1, 10000).Select(n => effects.First().DLL.Info.ShortName + " - " + n.ToString()).Where(x => !ExistingTemplates.Contains(x)).First();
            else
                name.Text = Enumerable.Range(1, 10000).Select(n => "Template" + n.ToString()).Where(x => !ExistingTemplates.Contains(x)).First();

            name.TextChanged += (sender, e) =>
            {
                bool exists = existing.Contains(name.Text);
                okButton.Content = exists ? "Update" : "OK";
                okButton.IsEnabled = name.Text.Length > 0 && name.Text.IsValidFileName();
            };

            okButton.Click += (sender, e) =>
            {
                if ((bool)includePatterns.IsChecked)
                    PatternMode = TemplatePatternMode.PatternsOnly;
                else if ((bool)includeSequences.IsChecked)
                    PatternMode = TemplatePatternMode.PatternsAndSequences;
                else
                    PatternMode = TemplatePatternMode.NoPatterns;

                if ((bool)waveRefs.IsChecked)
                    WavetableMode = TemplateWavetableMode.WaveRefsOnly;
                else if ((bool)waves.IsChecked)
                    WavetableMode = TemplateWavetableMode.WaveFiles;
                else
                    WavetableMode = TemplateWavetableMode.NoWavetable;

                TemplateName = name.Text;
                this.DialogResult = true;
                Close();
            };

            cancelButton.Click += (sender, e) =>
            {
                this.DialogResult = false;
                Close();
            };

            name.SelectAll();
            name.Focus();
        }
    }
}
