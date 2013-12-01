using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v2;

namespace WFTFGD.UI.AuthorizationWindow.MVVM
{
    public class ViewModel
    {
        Func<Task<DriveService>> _authorizationHandlerDelegate;
       
        private ICommand _signInCommand;
        private ICommand _exitCommand;
        
        public ViewModel(Func<Task<DriveService>> authorizationHandlerDelegate)
        {
            _authorizationHandlerDelegate = authorizationHandlerDelegate;
            _signInCommand  = new SignInCommandImplementation(this);
            _exitCommand = new ExitCommandImplementation(this);
        }

        public static Action ExitApplicationDelegate;

        public ICommand SignInCommand
        {
            get { return _signInCommand; }
        }

        public ICommand ExitCommand
        {
            get { return _exitCommand; }
        }

        public Window ParentWindow { get; set; }

        //Drive service to be awaited from other classes
        public DriveService DriveService { get; set; }

        private class ExitCommandImplementation : ICommand
        {
            private ViewModel _viewModel;

            public ExitCommandImplementation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;

            public async void Execute(object parameter)
            {
                if (ExitApplicationDelegate != null)
                {
                    ExitApplicationDelegate();
                }
                else
                {
                    MessageBox.Show("Application exit hadler must be implemented");
                }
            }
        }

        private class SignInCommandImplementation : ICommand
        {
            private ViewModel _viewModel;
            
            public SignInCommandImplementation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;

            public async void Execute(object parameter)
            {
                _viewModel.DriveService = await _viewModel._authorizationHandlerDelegate();
                if (_viewModel.DriveService != default(DriveService))
                {
                    _viewModel.ParentWindow.DialogResult = true;
                }
            }
        }
    }
}
