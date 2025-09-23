using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;

namespace BuzzGUI.MachineView
{
    public class TemplateListItemVM
    {
        public enum Types { Directory, Item };

        public Types Type { get; private set; }
        public string DisplayName { get; private set; }
        public string Path { get; private set; }

        public ICommand ExploreCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }

        readonly MachineView view;

        public TemplateListItemVM(MachineView view, string fn)
        {
            this.view = view;
            Type = Directory.Exists(fn) ? Types.Directory : Types.Item;
            Path = fn;

            if (Type == Types.Item)
            {
                DisplayName = System.IO.Path.GetFileNameWithoutExtension(fn);

                DeleteCommand = new SimpleCommand
                {
                    CanExecuteDelegate = x => true,
                    ExecuteDelegate = x => { Delete(false); }
                };
            }
            else
            {
                DisplayName = System.IO.Path.GetFileName(fn);
            }

            ExploreCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var psi = new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = Type == Types.Item ? System.IO.Path.GetDirectoryName(Path) : Path,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            };

        }

        public void Delete(bool permanently)
        {
            if (Type != Types.Item) return;

            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Path,
                    Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,
                    permanently ? Microsoft.VisualBasic.FileIO.RecycleOption.DeletePermanently : Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

            }
            catch (Exception) { }

        }

        static readonly Dictionary<string, ImageSource> icons = new Dictionary<string, ImageSource>();

        public ImageSource Icon
        {
            get
            {
                var ext = Type == Types.Item ? System.IO.Path.GetExtension(Path) : "dir";
                if (icons.ContainsKey(ext)) return icons[ext];
                return icons[ext] = Win32.LoadIconAsImageSource(Path);
            }
        }

    }
}
