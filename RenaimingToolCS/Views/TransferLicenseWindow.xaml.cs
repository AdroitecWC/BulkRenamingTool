using System.Windows;
using RenaimingToolCS.ViewModels;

namespace RenaimingToolCS.Views
{
    public partial class TransferLicenseWindow : Window
    {
        public TransferLicenseWindow()
        {
            InitializeComponent();
            Loaded += TransferLicenseWindow_Loaded;
        }

        private void TransferLicenseWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is TransferLicenseViewModel vm)
            {
                vm.RequestClose += result =>
                {
                    DialogResult = result;
                    Close();
                };
            }
        }
    }
}
