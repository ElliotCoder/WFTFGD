using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WFTFGD.UI.Entities
{
    /// <summary>
    /// Is a wrapper around file entity, adapted to WPF UI.
    /// </summary>
    public class UIFileRecordEntity
    {
        private ImageSource _fileIcon;
        private String _filePath;

        public UIFileRecordEntity(String path)
        {
            _filePath = path;
        }

        public String FileName
        {
            get { return Path.GetFileName(_filePath); }
        }

        public String ContainingDirectory
        {
            get { return Path.GetDirectoryName(_filePath); }
        }

        public ImageSource FileIcon
        {
            get
            {
                if (_fileIcon == null && File.Exists(_filePath))
                {
                    using (Icon icon = ShellIcon.GetSmallIcon(_filePath))
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
    }
}
