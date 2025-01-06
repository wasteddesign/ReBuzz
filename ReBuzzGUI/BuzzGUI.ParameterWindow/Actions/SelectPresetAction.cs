using BuzzGUI.Common.Presets;
using BuzzGUI.Interfaces;

namespace BuzzGUI.ParameterWindow.Actions
{
    class SelectPresetAction : IAction
    {
        readonly ParameterWindowVM vm;
        readonly Preset preset;
        Preset oldpreset;

        public SelectPresetAction(ParameterWindowVM vm, Preset p)
        {
            this.vm = vm;
            preset = p;
        }

        public void Do()
        {
            oldpreset = new Preset(vm.Machine, false, true);
            preset.Apply(vm.Machine, true);
        }

        public void Undo()
        {
            oldpreset.Apply(vm.Machine, true);
        }

    }
}
