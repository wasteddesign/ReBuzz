using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class RenameMachineGroupAction : BuzzAction
    {
        private readonly ReBuzzCore buzz;

        string name;
        string newName;

        public RenameMachineGroupAction(ReBuzzCore buzz, IMachineGroup machineGroup,string newName)
        {
            this.buzz = buzz;
            this.name = machineGroup.Name;
            this.newName = newName;
        }

        protected override void DoAction()
        {
            var song = buzz.SongCore;
            var machine = song.MachineGroups.FirstOrDefault(m => m.Name == this.name) as MachineGroupCore;
            if (machine != null)
            {
                machine.Name = newName;
            }
        }

        protected override void UndoAction()
        {
            var song = buzz.SongCore;
            var machine = song.MachineGroups.FirstOrDefault(m => m.Name == this.newName) as MachineGroupCore;
            if (machine != null)
            {
                machine.Name = name;
            }
        }
    }
}
