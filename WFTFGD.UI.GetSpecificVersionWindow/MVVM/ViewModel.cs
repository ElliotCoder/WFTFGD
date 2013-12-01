using System;
using System.Collections.Generic;
using System.ComponentModel;
using IO = System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Google.Apis.Drive.v2.Data;
using Microsoft.Win32;
using WFTFGD.Adapters;
using WFTFGD.Aggregators;
using WFTFGD.UI.ProgressWindow;

namespace WFTFGD.UI.GetSpecificVersionWindow.MVVM
{
    public class ViewModel : INotifyPropertyChanged
    {
        private readonly Window _parentWindow;
        private readonly List<DiffStampAdapter> _diffStampsList;
        private readonly FileEntityAggregator _fileEntityAggregator;
        private ICommand _saveSelectedVersionCommand;
        private ICommand _cancelCommand;
        private DiffStampAdapter _selectedDiffStamp;

        public ViewModel(Window parentWindow, FileEntityAggregator fileEntityAggregator)
        {
            _parentWindow = parentWindow;
            _fileEntityAggregator = fileEntityAggregator;
            _saveSelectedVersionCommand = new SaveSelectedVersionCommandImplementation(this);
            _cancelCommand = new CancelCommandImplementation(this);
            _diffStampsList = new List<DiffStampAdapter>();

            ResolveDiffstampsDescriptorsGet();
        }

        private void ResolveDiffstampsDescriptorsGet()
        {
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += new DoWorkEventHandler(
                (Object sender, DoWorkEventArgs eventAgrs) =>
                {
                    Task<IList<File>> diffStampsDescriptorTask = CloudFileTrackingAggregatorSingleton.
                        Instance.
                        TryGetAllDiffStampsDescriptorsFromFileRecordDirectoryAsync(
                            _fileEntityAggregator.GoogleDriveParentId);
                    diffStampsDescriptorTask.Wait();
                    foreach (File diffStampDescriptor in diffStampsDescriptorTask.Result)
                    {
                        String userDescription =  diffStampDescriptor.Title.Substring(
                            0, diffStampDescriptor.Title.LastIndexOf('.'));
                        DateTime dateTimeCreated = DateTime.Parse(diffStampDescriptor.CreatedDate);
                        //For user the dummy (empty from data) entities are shown
                        _diffStampsList.Add(new DiffStampAdapter(null, userDescription, dateTimeCreated));
                    }
                });
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                (Object sender, RunWorkerCompletedEventArgs eventAgrs) =>
                {
                    _parentWindow.IsEnabled = true;
                    NotifyPropertyChanged("DiffStampsList");
                    NotifyPropertyChanged("SelectedItem");
                });
            _parentWindow.IsEnabled = false;
            backgroundWorker.RunWorkerAsync();
        }

        public List<DiffStampAdapter> DiffStampsList
        {
            get { return _diffStampsList; }
        }

        public ICommand SaveSelectedVersionCommand
        {
            get { return _saveSelectedVersionCommand; }
        }

        public ICommand CancelCommand
        {
            get { return _cancelCommand; }
        }

        public DiffStampAdapter SelectedDiffStamp
        {
            get { return _selectedDiffStamp; }
            set
            {
                _selectedDiffStamp = value;
            }
        }

        private class CancelCommandImplementation : ICommand
        {
            private ViewModel _viewModel;

            public CancelCommandImplementation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }


            public void Execute(object parameter)
            {
                _viewModel._parentWindow.DialogResult = false;
            }
        }

        private class SaveSelectedVersionCommandImplementation : ICommand
        {
            private ViewModel _viewModel;

            public SaveSelectedVersionCommandImplementation(ViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CanExecute(object parameter)
            {
                return _viewModel.SelectedDiffStamp != default(DiffStampAdapter);
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }


            public void Execute(object parameter)
            {
                FileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.FileName = _viewModel._fileEntityAggregator.FileName;
                saveFileDialog.Title = "Save specific version";
                Boolean? saveFileDialogResult = saveFileDialog.ShowDialog(_viewModel._parentWindow);
                if (saveFileDialogResult == true)
                {
                    ContinuousProgressWindow progressWindow =
                        new ContinuousProgressWindow("Downloading and applying patches");
                    BackgroundWorker backgroundWorker = new BackgroundWorker();
                    backgroundWorker.DoWork += new DoWorkEventHandler(
                        (Object sender, DoWorkEventArgs eventArgs) =>
                        {
                            Task<Byte[]> latestFileVersionTask = default(Task<Byte[]>);
                            try
                            {
                                latestFileVersionTask =
                                    CloudFileTrackingAggregatorSingleton.
                                    Instance.
                                    TryGetFilePatchedUntilDateTime(
                                        _viewModel._fileEntityAggregator,
                                        _viewModel._selectedDiffStamp.DateTimeCreated);
                                latestFileVersionTask.Wait();
                                IO.FileStream fileStream = default(IO.FileStream);
                                try
                                {
                                    fileStream = new IO.FileStream(saveFileDialog.FileName, IO.FileMode.Create);
                                    Byte[] fileData = latestFileVersionTask.Result;
                                    fileStream.Write(fileData, 0, fileData.Length);
                                }
                                catch (Exception)
                                {
                                    MessageBox.Show(
                                    "Failed to save file",
                                    "Error while saving a file");
                                }
                                finally
                                {
                                    fileStream.Dispose();
                                }
                            }
                            catch(Exception)
                            {
                                MessageBox.Show(
                                    "Failed to download or patch file",
                                    "Error while downloading and patching");
                            }
                        });
                    backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                        (Object sender, RunWorkerCompletedEventArgs eventArgs) =>
                        {
                            _viewModel._parentWindow.IsEnabled = true;
                            progressWindow.Close();
                        });
                    _viewModel._parentWindow.IsEnabled = false;
                    progressWindow.Show();
                    backgroundWorker.RunWorkerAsync();
                }
                else
                {
                    //Cancelled by user
                }
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
