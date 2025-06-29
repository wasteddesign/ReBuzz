using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class GroupMachinesAction : BuzzAction
    {
        private readonly ReBuzzCore buzz;

        private readonly string name;
        private readonly bool group;

        public GroupMachinesAction(ReBuzzCore buzz, IMachineGroup mg, bool group)
        {
            this.buzz = buzz;
            this.name = mg.Name;
            this.group = group;
        }

        protected override void DoAction()
        {
            var mg = buzz.Song.MachineGroups.FirstOrDefault(g => g.Name == name);
            if (mg != null)
            {
                if (group)
                {
                    mg.IsGrouped = true;
                }
                else
                {
                    mg.IsGrouped = false;
                }
            }
        }

        protected override void UndoAction()
        {
            var mg = buzz.Song.MachineGroups.FirstOrDefault(g => g.Name == name);
            if (mg != null)
            {
                if (group)
                {
                    mg.IsGrouped = false;
                }
                else
                {
                    mg.IsGrouped = true;
                }
            }
        }
    }
}
