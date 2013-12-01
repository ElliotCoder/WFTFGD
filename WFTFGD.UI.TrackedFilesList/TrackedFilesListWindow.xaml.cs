using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WFTFGD.Aggregators;
using WFTFGD.UI.TrackedFilesList.MVVM;

namespace WFTFGD.UI.TrackedFilesList
{
    /// <summary>
    /// Логика взаимодействия для TrackedFilesListWindow.xaml
    /// </summary>
    public partial class TrackedFilesListWindow : Window
    {
        private ViewModel _viewModel;

        public TrackedFilesListWindow(ObservableCollection<FileEntityAggregator> fileEntities)
        {
            InitializeComponent();
            _viewModel = new ViewModel(this, fileEntities);
        }

        private void TrackedFilesListWindow_Loaded(object sender, RoutedEventArgs e)
        {
            view.DataContext = _viewModel;
        }
    }
}
