using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Google.Apis.Drive.v2;
using WFTFGD.UI.AuthorizationWindow.MVVM;

namespace WFTFGD.UI.AuthorizationWindow
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class GoogleAPIAuthorizationWindow : Window
    {
        private ViewModel _viewModel;

        public GoogleAPIAuthorizationWindow(ViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;

            SourceInitialized += GoogleAPIAuthorizationWindow_SourceInitialized;
        }

        public new DriveService ShowDialog()
        {
            Boolean? dialogResult = base.ShowDialog();
            if (dialogResult == true)
            {
                return _viewModel.DriveService;
            }
            else
            {
                //Exit here
                return null;
            }
        }

        void GoogleAPIAuthorizationWindow_SourceInitialized(object sender, EventArgs e)
        {
            WindowInteropHelper wih = new WindowInteropHelper(this);
            int style = GetWindowLong(wih.Handle, GWL_STYLE);
            SetWindowLong(wih.Handle, GWL_STYLE, style & ~WS_SYSMENU);
        }

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x00080000;

        [DllImport("user32.dll")]
        private extern static int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")]
        private extern static int GetWindowLong(IntPtr hwnd, int index);
    }
}
