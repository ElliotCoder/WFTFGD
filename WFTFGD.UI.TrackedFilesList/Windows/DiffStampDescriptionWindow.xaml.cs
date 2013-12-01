using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace WFTFGD.UI.TrackedFilesList.Windows
{
    /// <summary>
    /// Логика взаимодействия для DiffStampDescriptionWindow.xaml
    /// </summary>
    public partial class DiffStampDescriptionWindow : Window
    {
        

        public DiffStampDescriptionWindow()
        {
            InitializeComponent();
        }

        public new String ShowDialog()
        {
            if (base.ShowDialog() == true)
            {
                if (String.IsNullOrEmpty(txtboxDescription.Text) ||
                    String.IsNullOrWhiteSpace(txtboxDescription.Text))
                {
                    return "[No Description]";
                }
                else
                {
                    return txtboxDescription.Text;
                }
            }
            else
            {
                return default(String);
            }
        }

        private void btnMakeSnapshot_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        /*
        //Prevent textbox overflow
        private void txtboxDescription_PreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key != Key.Back)
            {


                if (txtboxDescription.LineCount <= MaxLines)
                {
                    if (MeasureString(txtboxDescription.Text).Width >= txtboxDescription.Width)
                    {
                        if (txtboxDescription.LineCount < MaxLines)
                        {
                            txtboxDescription.Text += "\n";
                        }
                        else
                        {
                            e.Handled = true;
                        }
                    }
                }
                else
                {
                    if (MeasureString(txtboxDescription.Text).Width >= txtboxDescription.Width)
                    {
                        e.Handled = true;
                    }
                }
            }
            
        }
        */
        private Size MeasureString(string candidate)
        {
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    this.txtboxDescription.FontFamily,
                    this.txtboxDescription.FontStyle,
                    this.txtboxDescription.FontWeight,
                    this.txtboxDescription.FontStretch),
                this.txtboxDescription.FontSize,
                Brushes.Black);

            return new Size(formattedText.Width, formattedText.Height);
        }

        //Prevent too long string
        private void txtboxDescription_KeyDown(object sender, KeyEventArgs e)
        {
            
        }
    }
}
