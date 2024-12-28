using BuzzGUI.Common;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
//using PropertyChanged;

namespace BuzzGUI.MachineView
{

    //	[DoNotNotify]
    public class TemplateList : INotifyPropertyChanged
    {
        readonly string templateRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Buzz\\Templates");
        string templatePath;

        readonly MachineView view;
        FileSystemWatcher fsw;
        IList<TemplateListItemVM> templateCollection;

        public TemplateList(MachineView view)
        {
            this.view = view;
            templatePath = templateRoot;

            try
            {
                Directory.CreateDirectory(templatePath);
                GetFiles();
                CreateFileSystemWatcher();
            }
            catch (Exception e)
            {
                DebugConsole.WriteLine(e.Message);
            }

        }

        public void Release()
        {
            RemoveFileSystemWatcher();
        }

        void CreateFileSystemWatcher()
        {
            if (fsw != null)
                RemoveFileSystemWatcher();

            fsw = new FileSystemWatcher(templatePath);
            fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            fsw.EnableRaisingEvents = true;

            fsw.Created += FileSystemWatcherEvent;
            fsw.Changed += FileSystemWatcherEvent;
            fsw.Deleted += FileSystemWatcherEvent;
            fsw.Renamed += FileSystemWatcherEvent;

        }

        void RemoveFileSystemWatcher()
        {
            if (fsw != null)
            {
                lock (fsw)
                {
                    fsw.Created -= FileSystemWatcherEvent;
                    fsw.Changed -= FileSystemWatcherEvent;
                    fsw.Deleted -= FileSystemWatcherEvent;
                    fsw.Renamed -= FileSystemWatcherEvent;
                }

                fsw.Dispose();
                fsw = null;
            }

        }

        void FileSystemWatcherEvent(object source, FileSystemEventArgs e)
        {
            lock (fsw)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { GetFiles(); }));
            }
        }



        void GetFiles()
        {
            try
            {
                templateCollection =
                    Directory.EnumerateFiles(templatePath, "*.xml")
                    .Concat(Directory.EnumerateFiles(templatePath, "*.zip"))
                    .Concat(Directory.EnumerateDirectories(templatePath))
                    .Select(fn => new TemplateListItemVM(view, System.IO.Path.Combine(templatePath, fn))).ToList();

                PropertyChanged.Raise(this, "Items");
            }
            catch (Exception e)
            {
                DebugConsole.WriteLine(e.Message);
            }

        }

        public IEnumerable<TemplateListItemVM> Items
        {
            get
            {
                return templateCollection
                        .Where(t => filter.Length == 0 || t.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(t => t.Type)
                        .ThenBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase);
            }
        }

        string filter = "";
        public string Filter
        {
            set
            {
                filter = value;
                PropertyChanged.Raise(this, "Items");
            }
        }

        public bool IsAtRoot { get { return templatePath == templateRoot; } }

        public void SetDirectory(string path)
        {
            if (path == "..")
            {
                if (IsAtRoot) return;
                path = Directory.GetParent(templatePath).FullName;
            }

            RemoveFileSystemWatcher();
            templatePath = path != null ? path : templateRoot;
            GetFiles();
            CreateFileSystemWatcher();
        }


        public void SaveTemplate(string name, Template template, IWavetable wt)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                if (wt != null)
                {
                    template.SaveZip(wt, System.IO.Path.Combine(templatePath, name + ".zip"));
                }
                else
                {
                    var path = System.IO.Path.Combine(templatePath, name + ".xml");

                    using (var fs = File.Create(path))
                    {
                        template.Save(fs);
                    }
                }

            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(string.Format("SaveTemplate failed ({0})", e.Message));
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

        }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
