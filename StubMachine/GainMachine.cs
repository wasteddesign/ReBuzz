using System.ComponentModel;
using System.Runtime.CompilerServices;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;

namespace StubMachine;

[MachineDecl(Name = "Modern Pattern Editor", ShortName = "MPE", Author = "WDE", MaxTracks = 0)]
public class GainMachine : IBuzzMachine, INotifyPropertyChanged
{
  IBuzzMachineHost host;

  public GainMachine(IBuzzMachineHost host)
  {
    this.host = host;
    Gain = new Interpolator();
  }

  [ParameterDecl(ResponseTime = 5, MaxValue = 127, DefValue = 80, Transformation = Transformations.Cubic, TransformUnityValue = 80, ValueDescriptor = Descriptors.Decibel)]
  public Interpolator Gain { get; private set; }

  [ParameterDecl(ValueDescriptions = new[] { "no", "yes" })]
  public bool Bypass { get; set; }


  [ParameterDecl(MaxValue = 127, DefValue = 0)]
  public void ATrackParam(int v, int track)
  {
    // track parameter example
  }

  public Sample Work(Sample s)
  {
    return Bypass ? s : s * Gain.Tick();
  }

  // actual machine ends here. the stuff below demonstrates some other features of the api.

  public class State : INotifyPropertyChanged
  {
    public State() { text = "here is state"; }  // NOTE: parameterless constructor is required by the xml serializer

    string text;
    public string Text
    {
      get { return text; }
      set
      {
        text = value;
        if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Text"));
        // NOTE: the INotifyPropertyChanged stuff is only used for data binding in the GUI in this demo. it is not required by the serializer.
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;
  }

  State machineState = new State();
  public State MachineState     // a property called 'MachineState' gets automatically saved in songs and presets
  {
    get { return machineState; }
    set
    {
      machineState = value;
      if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
    }
  }

  int checkedItem = 1;

  public IEnumerable<IMenuItem> Commands
  {
    get
    {
      yield return new MenuItemVM() { Text = "Hello" };
      yield return new MenuItemVM() { IsSeparator = true };
      yield return new MenuItemVM()
      {
        Text = "Submenu",
        Children = new[]
        {
          new MenuItemVM() { Text = "Child 1" },
          new MenuItemVM() { Text = "Child 2" }
        }
      };
      yield return new MenuItemVM() { Text = "Label", IsLabel = true };
      yield return new MenuItemVM()
      {
        Text = "Checkable",
        Children = new[]
        {
          new MenuItemVM() { Text = "Child 1", IsCheckable = true, StaysOpenOnClick = true },
          new MenuItemVM() { Text = "Child 2", IsCheckable = true, StaysOpenOnClick = true },
          new MenuItemVM() { Text = "Child 3", IsCheckable = true, StaysOpenOnClick = true }
        }
      };

      var g = new MenuItemVM.Group();

      yield return new MenuItemVM()
      {
        Text = "CheckGroup",
        Children = Enumerable.Range(1, 5).Select(i => new MenuItemVM()
        {
          Text = "Child " + i,
          IsCheckable = true,
          CheckGroup = g,
          StaysOpenOnClick = true,
          IsChecked = i == checkedItem,
          CommandParameter = i,
          Command = new SimpleCommand()
          {
            CanExecuteDelegate = p => true,
            ExecuteDelegate = p => checkedItem = (int)p
          }
        })
      };

      //bug yield return new MenuItemVM()
      //bug {
      //bug   Text = "About...",
      //bug   Command = new SimpleCommand()
      //bug   {
      //bug     CanExecuteDelegate = p => true,
      //bug     ExecuteDelegate = p => MessageBox.Show("About")
      //bug   }
      //bug };
    }
  }

  public event PropertyChangedEventHandler PropertyChanged;
}

//bug public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new GainGUI(); } }
//bug 
//bug public class GainGUI : IMachineGUI
//bug {
//bug   public IMachine Machine { get; set; }
//bug }
//bug public class GainGUI : UserControl, IMachineGUI
//bug {
//bug   IMachine machine;
//bug   GainMachine gainMachine;
//bug   TextBox tb;
//bug   ListBox lb;
//bug 
//bug   // view model for machine list box items
//bug   public class MachineVM
//bug   {
//bug     public IMachine Machine { get; private set; }
//bug     public MachineVM(IMachine m) { Machine = m; }
//bug     public override string ToString() { return Machine.Name; }
//bug   }
//bug 
//bug   public ObservableCollection<MachineVM> Machines { get; private set; }
//bug 
//bug   public IMachine Machine
//bug   {
//bug     get { return machine; }
//bug     set
//bug     {
//bug       if (machine != null)
//bug       {
//bug         BindingOperations.ClearBinding(tb, TextBox.TextProperty);
//bug         machine.Graph.MachineAdded -= machine_Graph_MachineAdded;
//bug         machine.Graph.MachineRemoved -= machine_Graph_MachineRemoved;
//bug       }
//bug 
//bug       machine = value;
//bug 
//bug       if (machine != null)
//bug       {
//bug         gainMachine = (GainMachine)machine.ManagedMachine;
//bug         tb.SetBinding(TextBox.TextProperty, new Binding("MachineState.Text") { Source = gainMachine, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
//bug 
//bug         machine.Graph.MachineAdded += machine_Graph_MachineAdded;
//bug         machine.Graph.MachineRemoved += machine_Graph_MachineRemoved;
//bug 
//bug         foreach (var m in machine.Graph.Machines)
//bug           machine_Graph_MachineAdded(m);
//bug 
//bug         lb.SetBinding(ListBox.ItemsSourceProperty, new Binding("Machines") { Source = this, Mode = BindingMode.OneWay });
//bug       }
//bug     }
//bug   }
//bug 
//bug   void machine_Graph_MachineAdded(IMachine machine)
//bug   {
//bug     Machines.Add(new MachineVM(machine));
//bug   }
//bug 
//bug   void machine_Graph_MachineRemoved(IMachine machine)
//bug   {
//bug     Machines.Remove(Machines.First(m => m.Machine == machine));
//bug   }
//bug 
//bug   public GainGUI()
//bug   {
//bug     Machines = new ObservableCollection<MachineVM>();
//bug 
//bug     tb = new TextBox() { Margin = new Thickness(0, 0, 0, 4), AllowDrop = true };
//bug     lb = new ListBox() { Height = 100, Margin = new Thickness(0, 0, 0, 4) };
//bug 
//bug     var sp = new StackPanel();
//bug     sp.Children.Add(tb);
//bug     sp.Children.Add(lb);
//bug     this.Content = sp;
//bug 
//bug     // drag and drop example
//bug     tb.PreviewDragEnter += (sender, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
//bug     tb.PreviewDragOver += (sender, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
//bug     tb.Drop += (sender, e) => { tb.Text = "drop"; e.Handled = true; };
//bug   }
//bug 
//bug }