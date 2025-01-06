using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace WDE.ModernPatternEditor
{
    /// <summary>
    /// Interaction logic for PatternPropertiesWindow.xaml
    /// </summary>
    public partial class PatternPropertiesWindow : Window
    {
        public PatternPropertiesWindow(PatternEditor editor)
        {
            this.Editor = editor;
            DataContext = this;

            ResourceDictionary rd = GetBuzzThemeResources();
            if (rd != null) this.Resources.MergedDictionaries.Add(rd);

            InitializeComponent();
            okButton.Click += OkButton_Click;
            cancelButton.Click += CancelButton_Click;

            patternNameTb.Text = Editor.SelectedMachine.SelectedPattern.Name;
            for (int i = 1; i <= 256; i++)
                patternLenghtCb.Items.Add(i.ToString());

            // For super long patterns
            PatternLenghtInBeats = Editor.SelectedMachine.SelectedPattern.BeatCount;

            patternLenghtCb.SelectedIndex = Editor.SelectedMachine.SelectedPattern.BeatCount - 1;

            for (int i = 2; i <= 16; i++)
                rowsPerBeatCb.Items.Add(i.ToString());

            int rowsPerBeat = 4;
            if (Editor.SelectedMachine.SelectedPattern != null)
                rowsPerBeat = Editor.MPEPatternsDB.GetMPEPattern(Editor.SelectedMachine.SelectedPattern.Pattern).RowsPerBeat;
            rowsPerBeatCb.SelectedIndex = rowsPerBeat - 2;

            Loaded += (sender, args) =>
            {
                treeView1.ItemsSource = SetTree();
            };
        }

        public List<TreeViewModel> MachineVM { get; set; }

        public PatternEditor Editor { get; }
        public int PatternLenghtInBeats { get; private set; }
        public int RowsPerBeat { get; private set; }
        public string PatternName { get; private set; }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (patternLenghtCb.SelectedIndex >= 0)
            {
                PatternLenghtInBeats = int.Parse((string)patternLenghtCb.SelectedItem);
            }
            RowsPerBeat = int.Parse((string)rowsPerBeatCb.SelectedItem);
            PatternName = patternNameTb.Text;
            DialogResult = true;
        }

        internal ResourceDictionary GetBuzzThemeResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\ModernPatternEditor\\PatternPropertiesWindow.xaml";
                //string skinPath = "..\\..\\..\\Themes\\" + selectedTheme + "\\ModernPatternEditor\\ModernPatternEditor.xaml";

                //skin.Source = new Uri(skinPath, UriKind.Absolute);
                skin = (ResourceDictionary)XamlReaderEx.LoadHack(skinPath);
            }
            catch (Exception)
            {
                string skinPath = Global.BuzzPath + "\\Themes\\Default\\ModernPatternEditor\\PatternPropertiesWindow.xaml";
                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }

            return skin;
        }

        public List<TreeViewModel> SetTree()
        {
            List<TreeViewModel> treeView = new List<TreeViewModel>();
            foreach (var mac in Global.Buzz.Song.Machines)
            {
                //if (mac.DLL.Info.Name == "Master")
                //    continue;

                TreeViewModel tvMachine = new TreeViewModel(mac);
                TreeViewModel tvMPEParameters = new TreeViewModel(mac, "Pattern Editor");
                TreeViewModel tvMachineParameters = new TreeViewModel(mac, "Parameters");

                // Editor Parameters
                foreach (var tPar in PatternEditorUtils.GetInternalParameters(mac))
                {
                    TreeViewModel tvPar = new TreeViewModel(tPar);
                    tvMPEParameters.Children.Add(tvPar);
                    if (Editor.MPEPatternsDB.IsParameterEnabled(tPar))
                        tvPar.IsChecked = true;
                }

                // Machine parameters
                foreach (var tPar in mac.ParameterGroups[1].Parameters)
                {
                    TreeViewModel tvPar = new TreeViewModel(tPar);
                    tvMachineParameters.Children.Add(tvPar);
                    if (Editor.MPEPatternsDB.IsParameterEnabled(tPar))
                        tvPar.IsChecked = true;
                }

                foreach (var tPar in mac.ParameterGroups[2].Parameters)
                {
                    TreeViewModel tvPar = new TreeViewModel(tPar);
                    tvMachineParameters.Children.Add(tvPar);
                    if (Editor.MPEPatternsDB.IsParameterEnabled(tPar))
                        tvPar.IsChecked = true;
                }

                tvMPEParameters.VerifyCheckedState();
                tvMachineParameters.VerifyCheckedState();
                tvMachine.Children.Add(tvMPEParameters);
                tvMachine.Children.Add(tvMachineParameters);

                tvMachine.Initialize();
                tvMachine.VerifyCheckedState();
                treeView.Add(tvMachine);
            }
            return treeView;
        }

        internal IEnumerable<IParameter> GetSelectedParameters()
        {
            List<IParameter> parameters = new List<IParameter>();

            foreach (TreeViewModel macItem in treeView1.Items)
            {
                foreach (TreeViewModel groupItem in macItem.Children)
                {
                    foreach (TreeViewModel parItem in groupItem.Children)
                    {
                        if (parItem.IsChecked == true)
                        {
                            parameters.Add(parItem.Parameter);
                        }
                    }
                }
            }

            return parameters;
        }
    }

    public class TreeViewModel : INotifyPropertyChanged
    {
        public TreeViewModel(IMachine mac, string name)
        {
            Machine = mac;
            Name = name;
            Children = new List<TreeViewModel>();
        }

        public TreeViewModel(IMachine mac)
        {
            Machine = mac;
            Name = mac.Name;
            Children = new List<TreeViewModel>();
        }

        public TreeViewModel(IParameter par)
        {
            Machine = par.Group.Machine;
            Parameter = par;
            Name = par.Name;
            Children = new List<TreeViewModel>();
        }

        public IMachine Machine { get; private set; }
        public IParameter Parameter { get; private set; }

        #region Properties

        public string Name { get; set; }
        public List<TreeViewModel> Children { get; private set; }
        public bool IsInitiallySelected { get; private set; }

        bool? _isChecked = false;
        TreeViewModel _parent;

        #region IsChecked

        public bool? IsChecked
        {
            get { return _isChecked; }
            set { SetIsChecked(value, true, true); }
        }

        void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _isChecked) return;

            _isChecked = value;

            if (updateChildren && _isChecked.HasValue) Children.ForEach(c => c.SetIsChecked(_isChecked, true, false));

            if (updateParent && _parent != null) _parent.VerifyCheckedState();

            NotifyPropertyChanged("IsChecked");
        }

        public void VerifyCheckedState()
        {
            bool? state = false;

            for (int i = 0; i < Children.Count; ++i)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0)
                {
                    state = current;
                }
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }

            SetIsChecked(state, false, true);
        }

        #endregion

        #endregion

        public void Initialize()
        {
            foreach (TreeViewModel child in Children)
            {
                child._parent = this;
                child.Initialize();
            }
        }
        void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

    }
}
