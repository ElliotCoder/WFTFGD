using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using WFTFGD.Adapters;
using WFTFGD.Aggregators;
using WFTFGD.UI.GetSpecificVersionWindow.MVVM;

namespace WFTFGD.UI.GetSpecificVersionWindow
{
    public partial class GetSpecificFileVersionWindow : Window
    {
        private ViewModel _viewModel;

        public GetSpecificFileVersionWindow()
        {
            InitializeComponent();
        }

        public GetSpecificFileVersionWindow(FileEntityAggregator fileEntityAggregator)
        {
            InitializeComponent();
            _viewModel = new ViewModel(this, fileEntityAggregator);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.view.DataContext = _viewModel;
        }

    }
}
