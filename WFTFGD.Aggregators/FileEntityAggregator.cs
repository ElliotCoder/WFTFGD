using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WFTFGD.Aggregators
{
    /// <summary>
    /// Is a wrapper around file entity, adapted to WPF UI.
    /// </summary>
    public class FileEntityAggregator : INotifyPropertyChanged
    {
        private ImageSource _fileIcon;
        public String LocalFilePath { get; set; }
        public String GoogleDrivePath { get; set; }
        public String GoogleDriveId { get; set; }
        public String GoogleDriveParentId { get; set; }
        public String MIMEType { get; set; }

        
        public FileEntityAggregator()
        {
        }

        public FileEntityAggregator(
            String localFilePath,
            String googleDriveId,
            String googleDriveParentId,
            String googleDrivePath,
            String mimeType)
        {
            LocalFilePath       = localFilePath;
            GoogleDriveId       = googleDriveId;
            GoogleDriveParentId = googleDriveParentId;
            GoogleDrivePath     = googleDrivePath;
            MIMEType            = mimeType;
        }

        public void RefreshRecord()
        {
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += new DoWorkEventHandler(
                (Object sender, DoWorkEventArgs eventArgs) =>
                {
                    Task<FileEntityAggregator> fileEntityUpdateTask =
                        CloudFileTrackingAggregatorSingleton.
                        Instance.UpdateFileEntityAggregator(this);
                    fileEntityUpdateTask.Wait();
                });
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                (Object sender, RunWorkerCompletedEventArgs eventArgs) =>
                {
                    OnPropertyChanged("LatestDiffStampTakenDateTime");
                    OnPropertyChanged("DiffStampsAmount");
                    OnPropertyChanged("CloudMemory");
                });
            backgroundWorker.RunWorkerAsync();
        }

        public String FileName
        {
            get { return Path.GetFileName(LocalFilePath); }
        }

        public String ContainingDirectory
        {
            get { return Path.GetDirectoryName(LocalFilePath); }
        }
        
        public ImageSource FileIcon
        {
            get
            {
                if (_fileIcon == null && File.Exists(LocalFilePath))
                {
                    using (Icon icon = ShellIconAdapter.GetSmallIcon(LocalFilePath))
                    {

                        _fileIcon = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                }
                return _fileIcon;
            }
        }

        public String CloudMemory { get; set; }

        public DateTime LatestDiffStampTakenDateTime { get; set; }

        public Int32 DiffStampsAmount { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
