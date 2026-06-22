using RenaimingToolCS.ViewModels;
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
using System.Windows.Shapes;

namespace RenaimingToolCS.Views
{
    /// <summary>
    /// Interaction logic for TransferLicenseWindow.xaml
    /// </summary>
    public partial class TransferLicenseWindow : Window
    {
        public TransferLicenseWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is TransferLicenseViewModel vm)
            {
                vm.RequestClose += (bool result) =>
                {
                    DialogResult = result;
                    Close();
                };
            }
        }
    }
}
