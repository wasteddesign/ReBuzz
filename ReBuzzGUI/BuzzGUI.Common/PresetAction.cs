using BuzzGUI.Common.Presets;
using BuzzGUI.Interfaces;
using System;

namespace BuzzGUI.Common
{
    public class PresetAction : SimpleAction<Preset>
    {
        public PresetAction(IMachine m, Action action)
        {
            DoDelegate = a =>
            {
                if (a.OldData == null)
                    a.OldData = new Preset(m, false, true);

                if (a.NewData == null)
                {
                    action();
                    a.NewData = new Preset(m, false, true);
                }
                else
                {
                    a.NewData.Apply(m, true);
                }
            };
            UndoDelegate = a =>
            {
                a.OldData.Apply(m, true);
            };
        }
    }
}
