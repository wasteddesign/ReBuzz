using AtmaFileSystem;
using AtmaFileSystem.IO;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
// ReSharper disable once RedundantUsingDirective
using NUnit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SimpleCommand = AttachedCommandBehavior.SimpleCommand;

namespace ReBuzzTests.Automation.TestMachines
{

    [MachineDecl(Name = "Effect", ShortName = "Effect", Author = "WDE", MaxTracks = 1, InputCount = 1,
        OutputCount = 1)]
    public class Effect(IBuzzMachineHost host) : IBuzzMachine, INotifyPropertyChanged
    {
        private readonly IBuzzMachineHost host = host;

        /// <summary>
        /// Gets the source code of this file for compilation into a dll.
        /// </summary>
        public static string GetSourceCode()
        {
            return AbsoluteFilePath.OfThisFile().ReadAllText();
        }

        public static MachineDecl GetMachineDecl()
        {
            return typeof(Effect).GetCustomAttributes(false)
                .OfType<MachineDecl>().Single();
        }

        [ParameterDecl(ValueDescriptions = ["no", "yes"])]
        public bool Bypass { get; set; }

        public Sample Work(Sample input)
        {
            var nextSample = sampleSource(input.L, input.R);
            Console.WriteLine($"Returning next sample: {nextSample.L}, {nextSample.R} based on {input.L}, {input.R}");
            return new Sample(nextSample.Item1, nextSample.Item2);
        }

        // actual machine ends here. the stuff below demonstrates some other features of the api.

        public class State : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
        }

        private State machineState = new();
        private Func<float, float, (float L, float R)> sampleSource = (l, r) => (l,r);

        public State MachineState // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get => machineState;
            set
            {
                machineState = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MachineState)));
            }
        }

        public IEnumerable<IMenuItem> Commands =>
        [
            new MenuItemVM
            {
                Text = "ConfigureSampleSource",
                Command = new SimpleCommand
                {
                    CanExecuteDelegate = _ => true,
                    ExecuteDelegate = o => sampleSource = (Func<float , float, (float L, float R)>)o //bug make this a delegate
                }
            }
        ];

        public event PropertyChangedEventHandler PropertyChanged;
    }
}