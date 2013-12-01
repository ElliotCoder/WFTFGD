using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Google.Apis.Drive.v2;
using Microsoft.WindowsAPICodePack.Dialogs;
using WFTFGD.Adapters;
using WFTFGD.Aggregators;
using WFTFGD.CloudStorage.GoogleDriveAPI;
using WFTFGD.UI.GetSpecificVersionWindow;
using WFTFGD.UI.ProgressWindow;
using WFTFGD.UI.TrackedFilesList.Windows;

namespace WFTFGD.UI.TrackedFilesList.MVVM
{
    internal class ViewModel
    {
        private readonly Window _parentWindow;
        private ObservableCollection<FileEntityAggregator> _fileRecordEntities;
        
        private readonly ICommand _addFilesCommand;
        private readonly ICommand _makeSnapshotCommand;
        private readonly ICommand _getSpecificVersionCommand;
        private readonly ICommand _stopTrackingCommand;
        private readonly ICommand _openParentFolderCommand;

        public ICommand OpenParentFolderCommand
        {
            get { return _openParentFolderCommand; }
        }

        public ICommand StopTrackingCommand
        {
            get { return _stopTrackingCommand; }
        }

        public ICommand AddFilesCommand
        {
            get { return _addFilesCommand; }
        }

        public ICommand MakeSnapshotCommand
        {
            get { return _makeSnapshotCommand; }
        }

        public ICommand GetSpecificVersionCommand
        {
            get { return _getSpecificVersionCommand; }
        }

        public ViewModel(Window parentWindow, ObservableCollection<FileEntityAggregator> fileRecordEntities)
        {
            _parentWindow = parentWindow;
            _fileRecordEntities = fileRecordEntities;
            _addFilesCommand = new AddFilesCommandImplementation(this);
            _makeSnapshotCommand = new MakeSnapshotCommandImplementation(this);
            _getSpecificVersionCommand = new GetSpecificVersionCommandImplementation(this);
            _stopTrackingCommand = new StopTrackingCommandImplementation(this);
            _openParentFolderCommand = new OpenParentFolderCommandImplamentation(this);
        }

        FileEntityAggregator _selectedRecordEntity;

        public FileEntityAggregator SelectedRecordEntity
        {
            get { return _selectedRecordEntity; }
            set { _selectedRecordEntity = value; }
        }

        public ObservableCollection<FileEntityAggregator> FileRecordEntities
        {
            get { return _fileRecordEntities; }
        }
        //Process.Start(@"c:\windows\");

        private class OpenParentFolderCommandImplamentation : ICommand
        {
            private ViewModel _viewModel;

            public OpenParentFolderCommandImplamentation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return _viewModel.SelectedRecordEntity != default(FileEntityAggregator);
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }


            public void Execute(object parameter)
            {
                Process.Start(System.IO.Path.GetDirectoryName(_viewModel._selectedRecordEntity.LocalFilePath));
            }
        }

        private class StopTrackingCommandImplementation : ICommand
        {
            private ViewModel _viewModel;

            public StopTrackingCommandImplementation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return _viewModel.SelectedRecordEntity != default(FileEntityAggregator);
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }


            public void Execute(object parameter)
            {
                ContinuousProgressWindow progressWindow =
                    new ContinuousProgressWindow("Removing record info from Google Drive");
                BackgroundWorker backgroundWorker = new BackgroundWorker();
                backgroundWorker.DoWork += new DoWorkEventHandler(
                    (Object sender, DoWorkEventArgs eventArgs) =>
                    {
                        Task<String> recordRemovalTask =
                            CloudFileTrackingAggregatorSingleton.
                            Instance.
                            TryRemoveRecordFromGoogleDrive(_viewModel._selectedRecordEntity);
                        recordRemovalTask.Wait();
                    });
                backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                    (Object sender, RunWorkerCompletedEventArgs eventArgs) =>
                    {
                        _viewModel._fileRecordEntities.Remove(_viewModel._selectedRecordEntity);
                        _viewModel._parentWindow.IsEnabled = true;
                        progressWindow.Close();
                        
                    });
                _viewModel._parentWindow.IsEnabled = false;
                progressWindow.Show();
                backgroundWorker.RunWorkerAsync();
            }
        }

        private class GetSpecificVersionCommandImplementation : ICommand
        {
            private ViewModel _viewModel;

            public GetSpecificVersionCommandImplementation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return _viewModel.SelectedRecordEntity != default(FileEntityAggregator);
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }


            public void Execute(object parameter)
            {
                GetSpecificFileVersionWindow getSpecificFileVersionWindow =
                    new GetSpecificFileVersionWindow(_viewModel._selectedRecordEntity);
                getSpecificFileVersionWindow.ShowDialog();
            }
        }

        private class MakeSnapshotCommandImplementation : ICommand
        {
            private ViewModel _viewModel;

            public MakeSnapshotCommandImplementation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return _viewModel.SelectedRecordEntity != default(FileEntityAggregator);
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            
            public void Execute(object parameter)
            {
                DiffStampDescriptionWindow diffStampDescriptionWindow =
                    new DiffStampDescriptionWindow();
                String diffStampDescription = diffStampDescriptionWindow.ShowDialog();
                if (diffStampDescription == default(String))
                {
                    //Cancelled by user
                }
                else
                {
                    ContinuousProgressWindow progressWindow =
                        new ContinuousProgressWindow("Making snapshot and uploading");
                    Window currentWindow = _viewModel._parentWindow;
                    BackgroundWorker backgroundWorker = new BackgroundWorker();
                    backgroundWorker.DoWork += new DoWorkEventHandler(
                        (Object sender, DoWorkEventArgs eventArgs) =>
                        {
                            FileEntityAggregator seletedRecord = _viewModel.SelectedRecordEntity;
                            Task<FileEntityAggregator> diffStampTask =
                                CloudFileTrackingAggregatorSingleton.
                                Instance.
                                TryMakeAndUploadDiffStamp(seletedRecord, diffStampDescription);
                            diffStampTask.Wait();
                            seletedRecord.RefreshRecord();
                        });
                    backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                        (Object sender, RunWorkerCompletedEventArgs eventArgs) =>
                        {
                            currentWindow.IsEnabled = true;
                            progressWindow.Close();
                        });
                    currentWindow.IsEnabled = false;
                    progressWindow.Show();
                    backgroundWorker.RunWorkerAsync();
                }
            }
        }

        private class AddFilesCommandImplementation : ICommand
        {
            private ViewModel _viewModel;

            public AddFilesCommandImplementation(ViewModel viewModel)
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
                
                    
                    

                Window parentWindow = _viewModel._parentWindow;
                CommonOpenFileDialog commonOpenFileDialog =
                    new CommonOpenFileDialog("Select file(s) for tracking");
                commonOpenFileDialog.Multiselect = true;
                WindowInteropHelper currentWindowHelper =
                    new WindowInteropHelper(parentWindow);
                CommonFileDialogResult dialogResult =
                    commonOpenFileDialog.ShowDialog(currentWindowHelper.Handle);
                if (dialogResult == CommonFileDialogResult.Ok)
                {
                    foreach (String localFilePath in commonOpenFileDialog.FileNames)
                    {
                        Boolean isAlreadyAdded =
                            CloudFileTrackingAggregatorSingleton.
                            IsLocalFileAddedForTracking(
                                _viewModel._fileRecordEntities,
                                localFilePath);
                        if (!isAlreadyAdded)
                        {
                            ContinuousProgressWindow progressWindow =
                                new ContinuousProgressWindow(
                                    String.Format("Uploading {0}", Path.GetFileName(localFilePath)));
                            BackgroundWorker backgroundWorker = new BackgroundWorker();
                            backgroundWorker.DoWork += new DoWorkEventHandler(
                                (Object sender, DoWorkEventArgs eventArgs) =>
                                {
                                    Task<FileEntityAggregator> fileUploadTask =
                                        CloudFileTrackingAggregatorSingleton.
                                        Instance.
                                        TryAddLocalFileForTracking(localFilePath);
                                    fileUploadTask.Wait();
                                    eventArgs.Result = fileUploadTask.Result;
                                });
                            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                                (Object sender, RunWorkerCompletedEventArgs eventArgs) =>
                                {
                                    _viewModel._fileRecordEntities.Add(eventArgs.Result as FileEntityAggregator);
                                    parentWindow.IsEnabled = true;
                                    progressWindow.Close();
                                });
                            parentWindow.IsEnabled = false;
                            progressWindow.Show();
                            backgroundWorker.RunWorkerAsync();
                        }
                    }
                }
            }
        }
    }
}
