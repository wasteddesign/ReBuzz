using ATL;
using ATL.AudioData;
using BuzzGUI.Common;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ATL.PictureInfo;


namespace BuzzGUI.MetadataControl
{
    /// <summary>
    /// Interaction logic for MetadataEditor.xaml
    /// </summary>
    public partial class MetadataEditorControl : UserControl, INotifyPropertyChanged
    {
        bool isValidAudioFile = false;
        public bool IsValidAudioFile { get { return isValidAudioFile; } private set { isValidAudioFile = value; PropertyChanged?.Raise(this, "IsValidAudioFile"); } }
        static MetadataEditorControl? metadataEditorControl;

        ObservableCollection<MetadataItemVM> metadataItems = new ObservableCollection<MetadataItemVM>();
        ObservableCollection<MetadataCustomItemVM> metadataCustomItems = new ObservableCollection<MetadataCustomItemVM>();
        ObservableCollection<MetadataImageItemsVM> metadataImageItems = new ObservableCollection<MetadataImageItemsVM>();
        private string filePath;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<MetadataItemVM> MetadataItems { get => metadataItems; }
        public ObservableCollection<MetadataCustomItemVM> MetadataCustomItems { get => metadataCustomItems; }
        public ObservableCollection<MetadataImageItemsVM> MetadataImageItems { get => metadataImageItems; }

        public ICommand LoadRecentImageCommand { get; private set; }
        public ICommand DeleteImageCommand { get; private set; }
        public ICommand AddImageCommand { get; private set; }
        public ICommand AddCustomFieldCommand { get; private set; }
        public ICommand DeleteMetadataCommand { get; private set; }
        public ICommand SaveCustomMetadataCommand { get; private set; }

        public static readonly DependencyProperty SelectedFileProperty =
           DependencyProperty.Register(
               "SelectedFile",
               typeof(string), // whatever your file type is
               typeof(MetadataEditorControl),
               new PropertyMetadata(null, OnSelectedFileChanged));

        private static void OnSelectedFileChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = (MetadataEditorControl)d;

            string oldFile = (string)e.OldValue;
            string newFile = (string)e.NewValue;

            metadataEditorControl?.UpdateView(newFile);
        }

        public string SelectedFile
        {
            get => (string)GetValue(SelectedFileProperty);
            set => SetValue(SelectedFileProperty, value);
        }

        public MetadataEditorControl()
        {
            InitializeComponent();
            metadataEditorControl = this;

            DataContext = this;

            LoadRecentImageCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = p =>
                {
                    if (p is Tuple<string, object> tuple)
                    {
                        string fileName = tuple.Item1;
                        object sender = tuple.Item2;
                        // Load the image from the selected recent file
                        var imageItem = (sender as MenuItem).DataContext as MetadataImageItemsVM;
                        if (imageItem != null)
                        {
                            // Update the ImageSourceData to the selected recent image
                            imageItem.ImageSourceData = new BitmapImage(new Uri(fileName));
                            imageItem.ImageSourceBytes = MetadataEditorControl.GetImageBytes(imageItem.ImageSourceData);
                            imageItem.UpdateRecentFilesList(fileName); // Update the recent files list
                        }
                    }
                }
            };

            DeleteImageCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = p =>
                {
                    var item = p as MetadataImageItemsVM;
                    if (metadataImageItems.Count > 1)
                    {
                        metadataImageItems.Remove(item);
                    }
                    else
                    {
                        item.ImageSourceData = null; // Clear the image source
                        item.ImageSourceBytes = null;
                    }
                }
            };

            AddImageCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = p =>
                {
                    metadataImageItems.Add(new MetadataImageItemsVM());
                }
            };

            AddCustomFieldCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = p =>
                {
                    metadataCustomItems.Add(new MetadataCustomItemVM() { Name = "", Value = "" });
                }
            };

            DeleteMetadataCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = p =>
                {
                    var item = p as MetadataCustomItemVM;
                    metadataCustomItems.Remove(item);
                }
            };

            SaveCustomMetadataCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = p =>
                {
                    SaveMetadata(filePath);
                }
            };
        }

        private void SaveMetadata(string path)
        {
            var formats = ATL.AudioData.AudioDataIOFactory.GetInstance().getFormats();
            var found = formats.Any(f => f.IsValidExtension(Path.GetExtension(path)));

            if (File.Exists(path) && found)
            {
                // Initialize with a file path
                Track theTrack = new Track(path);

                // Standard fields
                foreach (var item in metadataItems)
                {
                    switch (item.Name)
                    {
                        case "Title":
                            theTrack.Title = item.Value;
                            break;
                        case "Artist":
                            theTrack.Artist = item.Value;
                            break;
                        case "Album":
                            theTrack.Album = item.Value;
                            break;
                        case "Year":
                            if (int.TryParse(item.Value, out int year))
                                theTrack.Year = year;
                            break;
                        case "Track Number":
                            if (int.TryParse(item.Value, out int trackNumber))
                                theTrack.TrackNumber = trackNumber;
                            break;
                        case "Disc Number":
                            if (int.TryParse(item.Value, out int discNumber))
                                theTrack.DiscNumber = discNumber;
                            break;
                        case "Genre":
                            theTrack.Genre = item.Value;
                            break;
                        case "Comment":
                            theTrack.Comment = item.Value;
                            break;
                        case "Artists":
                            theTrack.Artist = item.Value;
                            break;
                    }
                }

                foreach (var customItem in metadataCustomItems)
                {
                    theTrack.AdditionalFields[customItem.Name] = customItem.Value;
                }

                theTrack.EmbeddedPictures.Clear();

                foreach (var item in metadataImageItems)
                {
                    if (item.ImageSourceBytes != null)
                    {
                        PictureInfo newPicture = PictureInfo.fromBinaryData(item.ImageSourceBytes, item.ImageType);
                        newPicture.Description = item.Name;
                        theTrack.EmbeddedPictures.Add(newPicture);
                    }
                }

                theTrack.Save();
            }
        }

        private void UpdateView(string path)
        {
            metadataItems.Clear();
            metadataCustomItems.Clear();
            metadataImageItems.Clear();

            var formats = ATL.AudioData.AudioDataIOFactory.GetInstance().getFormats();
            var found = formats.Any(f => f.IsValidExtension(Path.GetExtension(path)));

            if (File.Exists(path) && found)
            {
                filePath = path;
                // Initialize with a file path
                Track theTrack = new Track(path);

                // That's it ! Now try and display standard fields
                metadataItems.Add(new MetadataItemVM() { Name = "Title", Value = theTrack.Title });
                metadataItems.Add(new MetadataItemVM() { Name = "Artist", Value = theTrack.Artist });
                metadataItems.Add(new MetadataItemVM() { Name = "Album", Value = theTrack.Album });
                metadataItems.Add(new MetadataItemVM() { Name = "Year", Value = theTrack.Year.ToString() });
                metadataItems.Add(new MetadataItemVM() { Name = "Track Number", Value = theTrack.TrackNumber.ToString() });
                metadataItems.Add(new MetadataItemVM() { Name = "Disc Number", Value = theTrack.DiscNumber.ToString() });
                metadataItems.Add(new MetadataItemVM() { Name = "Genre", Value = theTrack.Genre });
                metadataItems.Add(new MetadataItemVM() { Name = "Comment", Value = theTrack.Comment });
                var artists = theTrack.Artist;
                metadataItems.Add(new MetadataItemVM() { Name = "Artists", Value = artists });
                metadataItems.Add(new MetadataItemVM() { Name = "Duration (s)", Value = theTrack.Duration.ToString(), IsEnabled = false });
                metadataItems.Add(new MetadataItemVM() { Name = "Bitrate (KBps)", Value = theTrack.Bitrate.ToString(), IsEnabled = false });
                metadataItems.Add(new MetadataItemVM() { Name = "Number of channels", Value = theTrack.ChannelsArrangement.NbChannels.ToString(), IsEnabled = false });
                metadataItems.Add(new MetadataItemVM() { Name = "Channels arrangement", Value = theTrack.ChannelsArrangement.Description, IsEnabled = false });

                metadataItems.Add(new MetadataItemVM() { Name = "Has variable bitrate audio", Value = theTrack.IsVBR ? "yes" : "no", IsEnabled = false });
                metadataItems.Add(new MetadataItemVM() { Name = "Has lossless audio", Value = AudioDataIOFactory.CF_LOSSLESS == theTrack.CodecFamily ? "yes" : "no", IsEnabled = false });

                // Display custom fields (e.g. TXXX values in ID3v2, or any other custom tag)
                foreach (KeyValuePair<string, string> field in theTrack.AdditionalFields)
                {
                    metadataCustomItems.Add(new MetadataCustomItemVM() { Name = field.Key, Value = field.Value });
                }

                // Get picture list
                IList<PictureInfo> embeddedPictures = theTrack.EmbeddedPictures;

                // Transform them into .NET Image, if needed
                foreach (PictureInfo pic in embeddedPictures)
                {
                    metadataImageItems.Add(new MetadataImageItemsVM() { Name = pic.Description, ImageSourceData = LoadImage(pic.PictureData), ImageSourceBytes = pic.PictureData, ImageType = pic.PicType });
                }

                // If no images, create a placeholder image
                if (metadataImageItems.Count == 0)
                {
                    // Create a placeholder image (e.g., a blank image or a default icon)
                    metadataImageItems.Add(new MetadataImageItemsVM() { Name = "" });
                }

                IsValidAudioFile = true;
            }
            else
            {
                IsValidAudioFile = false;
                filePath = null;
            }
        }

        public static ImageSource LoadImage(byte[] imageData)
        {
            using (var ms = new MemoryStream(imageData))
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze(); // Freeze to make it cross-thread accessible
                return bitmap;
            }
        }

        public static byte[] GetImageBytes(ImageSource imageSource)
        {
            if (imageSource is not BitmapSource bitmapSource)
                return null;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            using var ms = new MemoryStream();
            encoder.Save(ms);

            return ms.ToArray();
        }

        public void ImageMenuOpened(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;

            int count = menuItem.Items.Count;
            for (int i = 3; i < count; i++)
            {
                menuItem.Items.RemoveAt(3);
            }
            foreach(var item in RegistryEx.ReadNumberedList<string>("File", "Recent Image File List")
                        .Select(fn => new MenuItem() { Header = Win32.CompactPath(fn, 50), Command = LoadRecentImageCommand, CommandParameter = new Tuple<string,object>(fn, sender) }))
            {
                menuItem.Items.Add(item);
            }
        }
    }

    public class MetadataItemVM : INotifyPropertyChanged
    {
        string name;
        string value;
        private bool isEnabled;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name { get => name; set => name = value; }
        public string Value { get => value; set => this.value = value; }
        public bool IsEnabled { get => isEnabled; internal set { isEnabled = value; PropertyChanged?.Raise(this, "IsEnabled"); } }

        public MetadataItemVM()
        {
            IsEnabled = true;
        }
    }

    public class MetadataCustomItemVM
    {
        string name;
        string value;

        public string Name { get => name; set => name = value; }
        public string Value { get => value; set => this.value = value; }
    }

    public class MetadataImageItemsVM : INotifyPropertyChanged
    {
        string name;
        ImageSource imageSourceData;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name { get => name; set => name = value; }
        public ImageSource ImageSourceData { get => imageSourceData; set { imageSourceData = value; PropertyChanged?.Raise(this, "ImageSourceData"); } }

        public ICommand LoadImageCommand { get; private set; }
        public byte[] ImageSourceBytes { get; internal set; }

        PIC_TYPE imageType;
        public PIC_TYPE ImageType
        {
            get => imageType;
            set
            {
                if (imageType != value)
                {
                    imageType = value;
                    PropertyChanged?.Raise(this, "ImageType");
                }
            }
        }

        public Array AvailableImageTypes => Enum.GetValues(typeof(PIC_TYPE));

        public MetadataImageItemsVM()
        {
            LoadImageCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = p =>
                {
                    // Implement the logic to load the image when the command is executed
                    OpenFileDialog openFileDialog = new OpenFileDialog();
                    openFileDialog.Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*";
                    if (openFileDialog.ShowDialog() == true)
                    {
                        // Load the selected image
                        ImageSourceData = new BitmapImage(new Uri(openFileDialog.FileName));
                        ImageSourceBytes = MetadataEditorControl.GetImageBytes(ImageSourceData);
                        UpdateRecentFilesList(openFileDialog.FileName); // Update the recent files list
                    }
                }
            };
        }



        public void UpdateRecentFilesList(string fileName)
        {
            var files = RegistryEx.ReadNumberedList<string>("File", "Recent Image File List").ToList();
            foreach (var file in files.ToArray())
            {
                if (file == fileName)
                {
                    files.Remove(file);
                }
            }

            // Move to top
            files.Insert(0, fileName);

            string key = Global.RegistryRoot + "\\" + "Recent Image File List";
            try
            {
                using (RegistryKey rkey = Registry.CurrentUser.OpenSubKey(key, true))
                {
                    // Check if the key exists
                    if (rkey != null)
                    {
                        Registry.CurrentUser.DeleteSubKey(key);
                    }
                }
            }
            catch { }

            var regKey = Registry.CurrentUser.CreateSubKey(key);
            int maxFiles = Math.Min(files.Count, 10);
            for (int i = 0; i < maxFiles; i++)
            {
                regKey.SetValue("File" + (i + 1), files[i]);
            }
        }
    }
}
