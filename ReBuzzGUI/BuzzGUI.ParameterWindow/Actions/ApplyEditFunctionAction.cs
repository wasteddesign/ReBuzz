using BuzzGUI.Common.Presets;
using BuzzGUI.Interfaces;

namespace BuzzGUI.ParameterWindow.Actions
{
    class ApplyEditFunctionAction : IAction
    {
        readonly ParameterWindowVM vm;
        Preset newpreset;
        Preset oldpreset;

        public bool IsUpdateable { get; private set; }

        public ApplyEditFunctionAction(ParameterWindowVM vm, bool updateable)
        {
            this.vm = vm;
            IsUpdateable = updateable;
        }

        public void Do()
        {
            oldpreset = new Preset(vm.Machine, false, true);

            if (newpreset == null)
            {
                Update();
            }
            else
            {
                newpreset.Apply(vm.Machine, true);
            }


        }

        public void Update()
        {
            foreach (ParameterVM pvm in vm.Parameters)
                pvm.ApplyEditFunction();

            newpreset = new Preset(vm.Machine, false, true);
        }

        public void Undo()
        {
            oldpreset.Apply(vm.Machine, true);
        }

    }
}
