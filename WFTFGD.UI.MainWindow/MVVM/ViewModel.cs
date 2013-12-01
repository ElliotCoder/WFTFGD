using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using IO = System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Google.Apis.Drive.v2;
using Google.Apis.Drive.v2.Data;
using WFTFGD.Adapters;
using WFTFGD.Aggregators;
using WFTFGD.Aggregators.BridgingEntities;
using WFTFGD.UI.ProgressWindow;
using WFTFGD.UI.TrackedFilesList;
using WFTFGD.CloudStorage.GoogleDriveAPI;
using WFTFGD.UI.AuthorizationWindow;

namespace WFTFGD.UI.MainWindow.MVVM
{
    internal class ViewModel : INotifyPropertyChanged, IDisposable
    {
        private Visibility _dragEnterNotifierVisibility = Visibility.Collapsed;
        #region Private: Commands
        private readonly ICommand _exitCommand;
        private readonly ICommand _openFilesTrackingListCommand;
        private readonly ICommand _logOutCommand;
        #endregion Private: Commands
        private readonly TrayAdapter _trayAdapter;

        private readonly Window _parentWindow;

        //List of file record entities, which is loaded on startup
        private readonly ObservableCollection<FileEntityAggregator> _fileEntities;

        public ViewModel(Window parentWindow)
        {
            _parentWindow = parentWindow;
            //Commands initialization
            _exitCommand = new ExitCommandImplementation(this);
            _openFilesTrackingListCommand = new OpenFilesTrackingListCommandImplementation(this);
            _logOutCommand = new LogOutCommandImplementation(this);
            //Tray icon with menu
            _trayAdapter = new TrayAdapter(this);
            //Files list
            _fileEntities = new ObservableCollection<FileEntityAggregator>();
            AuthorizationWindow.MVVM.ViewModel.ExitApplicationDelegate = 
                new Action(() => ExitCommand.Execute(null));
            ResolveApplicationInitialization();
        }

        public Window ParentWindow
        {
            get { return _parentWindow; }
        }

        private void ResolveApplicationInitialization()
        {
            //Background operations must coordinate the UI thread to invoke
            CloudFileTrackingAggregatorSingleton.Instance.CurentUIThreadInvoker = _parentWindow;
            ContinuousProgressWindow progressWindow = new ContinuousProgressWindow("Loading file records");
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += new DoWorkEventHandler(
                (Object sender, DoWorkEventArgs eventArgs) =>
                {
                    Task <List<FileEntityAggregator>> entitiesListTask =
                        CloudFileTrackingAggregatorSingleton.
                        Instance.
                        TryInitializeGoogleDriveFileRecords();
                    entitiesListTask.Wait();
                    IList<FileEntityAggregator> fileEntitiesList = entitiesListTask.Result;
                    foreach (FileEntityAggregator fileEntity in fileEntitiesList)
                    {
                        _fileEntities.Add(fileEntity);
                    }
                });
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                (Object sender, RunWorkerCompletedEventArgs eventArgs) =>
                {
                    _trayAdapter.ShowInfoMessage(
                        "Files Time Machine For Google Drive",
                        "The application has loaded");
                    _parentWindow.IsEnabled = true;
                    progressWindow.Close();
                });
            _parentWindow.IsEnabled = false;
            progressWindow.Show();
            backgroundWorker.RunWorkerAsync();
        }

        
        public Visibility DragEnterNotifierVisibility
        {
            get { return _dragEnterNotifierVisibility; }
            set
            {
                _dragEnterNotifierVisibility = value;
                OnPropertyChanged();
            }
        }

        public void Closing(object sender, CancelEventArgs eventArguments)
        {
            eventArguments.Cancel = true; //Cancel window closing
            Window window = sender as Window;
            window.Hide();
        }

        #region Command accessors

        public ICommand LogOutCommand
        {
            get { return _logOutCommand; }
        }

        public ICommand ExitCommand
        {
            get { return _exitCommand; }
        }

        public ICommand OpenFilesTrackingListCommand
        {
            get { return _openFilesTrackingListCommand; }
        }

        #endregion Command accessors

        #region Commands Implementations

        private class LogOutCommandImplementation : ICommand
        {
            private ViewModel _viewModel;

            public LogOutCommandImplementation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;

            public void Execute(object parameter)
            {
                BackgroundWorker backgroundWorker = new BackgroundWorker();
                backgroundWorker.DoWork += new DoWorkEventHandler(
                    (Object sender, DoWorkEventArgs eventArgs) =>
                    {
                        Task<DriveService> authorizerTask =
                            CloudFileTrackingAggregatorSingleton.
                            Instance.
                            TryGetAuthorizer();
                        authorizerTask.Wait();
                        GoogleAPIAuthorization.
                        Instance.
                        Deauthorize(authorizerTask.Result).Wait();
                    });
                backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                    (Object sender, RunWorkerCompletedEventArgs eventArgs) =>
                    {
                        _viewModel._parentWindow.IsEnabled = true;
                    });
                _viewModel._parentWindow.IsEnabled = false;
                backgroundWorker.RunWorkerAsync();
            }
        }
        
        private class OpenFilesTrackingListCommandImplementation : ICommand
        {
            private ViewModel _viewModel;

            public OpenFilesTrackingListCommandImplementation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;

            public void Execute(object parameter)
            {
                TrackedFilesListWindow trackedFilesListWindow = 
                    new TrackedFilesListWindow(_viewModel._fileEntities);
                trackedFilesListWindow.ShowDialog();
            }
        }

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

            public void Execute(object parameter)
            {
                _viewModel.Dispose();
                Application.Current.Shutdown();
            }
        }

        #endregion Commands Implementations

        #region File drag and drop events handlers

        public void DragEnter(object sender, DragEventArgs eventArguments)
        {
            IDataObject draggedObject = eventArguments.Data;
            Object draggedItems = draggedObject.GetData(DataFormats.FileDrop, false);
            String[] pathsToItems = draggedItems as String[];
            foreach (String filePath in pathsToItems)
            {
                IO.FileAttributes fileAttributes = IO.File.GetAttributes(filePath);
                if ((fileAttributes & IO.FileAttributes.Directory) == IO.FileAttributes.Directory)
                {
                    return;
                }
            }
            DragEnterNotifierVisibility = Visibility.Visible;
        }

        public void DragLeave(object sender, DragEventArgs eventArguments)
        {
            DragEnterNotifierVisibility = Visibility.Collapsed;
        }

        public void Drop(Object sender, DragEventArgs eventArguments)
        {
            IDataObject draggedObject = eventArguments.Data;
            Object draggedItems = draggedObject.GetData(DataFormats.FileDrop, false);
            String[] pathsToItems = draggedItems as String[];
            DragEnterNotifierVisibility = Visibility.Collapsed;
            foreach (String localFilePath in pathsToItems)
            {
                Boolean isAlreadyAdded =
                            CloudFileTrackingAggregatorSingleton.
                            IsLocalFileAddedForTracking(
                                _fileEntities,
                                localFilePath);
                if (!isAlreadyAdded)
                {
                    ContinuousProgressWindow progressWindow =
                        new ContinuousProgressWindow(
                            String.Format("Uploading {0}", IO.Path.GetFileName(localFilePath)));
                    BackgroundWorker backgroundWorker = new BackgroundWorker();
                    backgroundWorker.DoWork += new DoWorkEventHandler(
                        (Object bwsender, DoWorkEventArgs eventArgs) =>
                        {
                            Task<FileEntityAggregator> fileUploadTask =
                                CloudFileTrackingAggregatorSingleton.
                                Instance.
                                TryAddLocalFileForTracking(localFilePath);
                            fileUploadTask.Wait();
                            eventArgs.Result = fileUploadTask.Result;
                        });
                    backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                        (Object bwsender, RunWorkerCompletedEventArgs eventArgs) =>
                        {
                            _fileEntities.Add(eventArgs.Result as FileEntityAggregator);
                            _parentWindow.IsEnabled = true;
                            progressWindow.Close();
                        });
                    _parentWindow.IsEnabled = false;
                    progressWindow.Show();
                    backgroundWorker.RunWorkerAsync();
                }
            }
        }

        #endregion File drag and drop events handlers

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler eventHandler = this.PropertyChanged;
            if (eventHandler != null)
            {
                eventHandler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion INotifyPropertyChanged

        public void Dispose()
        {
            _trayAdapter.Dispose();
        }
    }
}
