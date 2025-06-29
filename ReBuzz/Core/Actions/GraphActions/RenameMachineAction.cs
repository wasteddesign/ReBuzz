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
    internal class RenameMachineAction : BuzzAction
    {
        private readonly ReBuzzCore buzz;

        string name;
        string newName;

        public RenameMachineAction(ReBuzzCore buzz, IMachine machine,string newName)
        {
            this.buzz = buzz;
            this.name = machine.Name;
            this.newName = newName;
        }

        protected override void DoAction()
        {
            var song = buzz.SongCore;
            var machine = song.Machines.FirstOrDefault(m => m.Name == this.name) as MachineCore;
            if (machine != null)
            {
                buzz.RenameMachine(machine, newName);
            }
        }

        protected override void UndoAction()
        {
            var song = buzz.SongCore;
            var machine = song.Machines.FirstOrDefault(m => m.Name == this.newName) as MachineCore;
            if (machine != null)
            {
                buzz.RenameMachine(machine, name);
            }
        }
    }
}
