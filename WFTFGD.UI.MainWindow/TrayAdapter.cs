using System;
using System.Windows.Forms;
using WFTFGD.UI.MainWindow.MVVM;
using WFTFGD.UI.MainWindow.Properties;

namespace WFTFGD.UI.MainWindow
{
    internal class TrayAdapter : IDisposable
    {
        private ViewModel _viewModel;
        private readonly NotifyIcon _trayIcon;

        public TrayAdapter(ViewModel viewModel)
        {
            _viewModel = viewModel;
            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add(0,
                new MenuItem("Show main window",
                    new EventHandler(
                        (Object sender, EventArgs eventArgs) =>
                        {
                            _viewModel.ParentWindow.Show();
                            _viewModel.ParentWindow.Activate();
                        }
                    )
                )
            );
            
            contextMenu.MenuItems.Add(1,
                new MenuItem("Exit",
                    new EventHandler(
                        (Object sender, EventArgs eventArgs) => _viewModel.ExitCommand.Execute(sender)
                    )
                )
            );
            
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Google Drive Files Time Machine";
            _trayIcon.Icon = Resources.file;
            _trayIcon.Visible = true;
            _trayIcon.ContextMenu = contextMenu;
        }

        public void ShowInfoMessage(String header, String message)
        {
            _trayIcon.ShowBalloonTip(3500, header, message, ToolTipIcon.Info);
        }

        public void Dispose()
        {
            _trayIcon.Dispose();
        }
    }
}
