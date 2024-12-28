using BuzzGUI.Common;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace BuzzGUI.FileBrowser
{
    public class FSItemVM : INotifyPropertyChanged
    {
        public enum Types { Drive, Directory, File };

        readonly string path;
        readonly string name;
        readonly FileInfo fileInfo;
        readonly DirectoryInfo directoryInfo;
        readonly DriveInfo driveInfo;
        ImageSource icon;
        bool triedToLoadIcon;
        bool triedToGetVolumeLabel;

        public Types Type
        {
            get
            {
                if (driveInfo != null) return FSItemVM.Types.Drive;
                else if (directoryInfo != null) return FSItemVM.Types.Directory;
                else return FSItemVM.Types.File;
            }
        }

        public FSItemVM(string path)
        {
            this.path = path;
            this.name = path;
            directoryInfo = new DirectoryInfo(path);
            driveInfo = new DriveInfo(path);
        }

        public FSItemVM(FileInfo fi)
        {
            path = fi.FullName;
            name = fi.Name;
            fileInfo = fi;
        }

        public FSItemVM(DirectoryInfo di)
        {
            path = di.FullName;
            name = di.Name;
            directoryInfo = di;
        }

        public FSItemVM(DirectoryInfo di, bool fullpathname)
        {
            path = di.FullName;
            name = fullpathname ? di.FullName : di.Name;
            directoryInfo = di;
        }

        public string FullPath { get { return path; } }
        public long Size { get { return fileInfo != null ? fileInfo.Length : 0; } }
        public bool IsFile { get { return fileInfo != null; } }
        public bool IsDirectory { get { return directoryInfo != null; } }

        public string SizeString { get { return IsDirectory ? "" : (Size / 1024).ToString() + " KB"; } }
        public Tuple<Types, string> TypeAndName { get { return Tuple.Create(Type, name); } }
        public Tuple<Types, long> TypeAndSize { get { return Tuple.Create(Type, Size); } }
        public Tuple<Types, DateTime> TypeAndDateModified { get { return Tuple.Create(Type, DateModified); } }

        DateTime? dateModified;
        public DateTime DateModified
        {
            get
            {
                if (dateModified != null)
                    return (DateTime)dateModified;

                var ui = TaskScheduler.FromCurrentSynchronizationContext();
                DateTime dm = new DateTime();

                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        if (driveInfo == null || (driveInfo.DriveType != DriveType.Removable && driveInfo.DriveType != DriveType.CDRom && driveInfo.DriveType != DriveType.Network))
                            dm = directoryInfo != null ? directoryInfo.LastWriteTime : fileInfo.LastWriteTime;
                    }
                    catch { }
                }).ContinueWith(_ =>
                {
                    dateModified = dm;
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("DateModified"));
                        PropertyChanged(this, new PropertyChangedEventArgs("DateModifiedString"));
                    }

                }, ui);

                return new DateTime();
            }
        }

        string volumeLabel;
        public string Name
        {
            get
            {
                if (driveInfo == null)
                    return name;

                if (!triedToGetVolumeLabel)
                {
                    triedToGetVolumeLabel = true;

                    TaskScheduler ui = null;

                    try
                    {
                        ui = TaskScheduler.FromCurrentSynchronizationContext();
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message);
                    }

                    string label = null;

                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            label = driveInfo.VolumeLabel;
                            if (label.Length == 0)
                            {
                                if (driveInfo.DriveType == DriveType.Fixed)
                                    label = "Local Drive";
                            }
                        }
                        catch (Exception) { }
                    }).ContinueWith(_ =>
                    {
                        volumeLabel = label;

                        if (PropertyChanged != null)
                            PropertyChanged(this, new PropertyChangedEventArgs("Name"));

                    }, ui);

                    return name;
                }

                if (volumeLabel != null)
                    return name + " (" + volumeLabel + ")";
                else
                    return name;
            }
        }


        public string DateModifiedString
        {
            get
            {
                var dm = DateModified;
                if (dm == new DateTime())
                    return null;
                else
                    return dm.ToString();

            }
        }


        public ImageSource Icon
        {
            get
            {
                if (icon == null && !triedToLoadIcon)
                {
                    triedToLoadIcon = true;

                    var ui = TaskScheduler.FromCurrentSynchronizationContext();
                    IntPtr hIcon = IntPtr.Zero;

                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            if (driveInfo == null || (driveInfo.DriveType != DriveType.Removable && driveInfo.DriveType != DriveType.CDRom && driveInfo.DriveType != DriveType.Network))
                                hIcon = Win32.LoadIcon(path);
                        }
                        catch (Exception) { }
                    }).ContinueWith(_ =>
                    {
                        if (hIcon != IntPtr.Zero)
                        {
                            try
                            {
                                icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, null);
                                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Icon"));
                            }
                            finally
                            {
                                Win32.DestroyIcon(hIcon);
                            }

                        }


                    }, ui);

                    return null;
                }

                return icon;
            }
        }

        public void LoadIcon()
        {
            icon = Win32.LoadIconAsImageSource(path);
            triedToLoadIcon = true;
        }

        public ObservableCollection<FSItemVM> GetSubItems(IEnumerable<string> extfilter)
        {
            if (!IsDirectory) return null;
            return GetItemsFromPath(directoryInfo, extfilter, true);
        }

        static void GetMountedFolders(ObservableCollection<FSItemVM> l)
        {
            var key = Registry.CurrentUser.OpenSubKey(Global.RegistryRoot + "Settings");
            if (key == null) return;

            int index = 0;

            while (true)
            {
                var v = key.GetValue("WaveDir" + index.ToString());
                if (v == null) break;
                if (v is string)
                {
                    l.Add(new FSItemVM(new DirectoryInfo(v as string), true));
                }
                index++;
            }

        }


        public static ObservableCollection<FSItemVM> GetItemsFromPath(DirectoryInfo di, IEnumerable<string> extfilter, bool includefiles)
        {
            var l = new ObservableCollection<FSItemVM>();

            try
            {
                if (di == null)
                {
                    try
                    {
                        foreach (var x in DriveInfo.GetDrives())
                            l.Add(new FSItemVM(x.Name));

                        l.Add(new FSItemVM(new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Desktop))));
                        l.Add(new FSItemVM(new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))));

                        GetMountedFolders(l);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.ToString(), System.Reflection.Assembly.GetExecutingAssembly().FullName);
                    }
                }
                else
                {

                    if (includefiles)
                    {
                        foreach (var x in di.EnumerateFiles())
                        {
                            if ((x.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                            if ((x.Attributes & FileAttributes.System) == FileAttributes.System) continue;

                            if (extfilter != null)
                            {
                                bool found = false;

                                foreach (string f in extfilter)
                                {
                                    if (f.Equals(x.Extension, StringComparison.OrdinalIgnoreCase))
                                    {
                                        found = true;
                                        break;
                                    }
                                }

                                if (!found) continue;
                            }

                            l.Add(new FSItemVM(x));
                        }
                    }

                    foreach (var x in di.EnumerateDirectories())
                    {
                        if ((x.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                        l.Add(new FSItemVM(x));
                    }
                }
            }
            catch (Exception) { }

            return l;
        }

        static Task searchTask;
        static CancellationTokenSource searchCancel = new CancellationTokenSource();

        void Search(string s, IEnumerable<string> extfilter, Action<FSItemVM> callback, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                ct.ThrowIfCancellationRequested();

            if (Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                callback(this);

            if (IsDirectory)
            {
                if (driveInfo == null || driveInfo.DriveType != DriveType.Removable)
                {
                    foreach (var x in GetItemsFromPath(directoryInfo, extfilter, true))
                        x.Search(s, extfilter, callback, ct);
                }
            }
        }

        public static void Search(IEnumerable<FSItemVM> items, string s, IEnumerable<string> extfilter, Action<FSItemVM> callback)
        {
            CancelSearch();

            searchCancel = new CancellationTokenSource();
            var token = searchCancel.Token;

            searchTask = Task.Factory.StartNew(() => { foreach (var x in items) x.Search(s, extfilter, callback, token); }, token);
        }

        public static void CancelSearch()
        {
            if (searchTask != null)
            {
                searchCancel.Cancel();
                try
                {
                    searchTask.Wait();
                }
                catch (AggregateException) { }

                searchTask = null;
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
