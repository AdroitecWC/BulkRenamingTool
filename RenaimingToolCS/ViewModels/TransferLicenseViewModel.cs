using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using RenaimingToolCS.Helpers;

namespace RenaimingToolCS.ViewModels
{
    public class TransferLicenseViewModel : INotifyPropertyChanged
    {
        // -------------------------------
        // Properties
        // -------------------------------
        private string _newSystemCode;
        public string NewSystemCode
        {
            get => _newSystemCode;
            set
            {
                if (_newSystemCode != value)
                {
                    _newSystemCode = value;
                    OnPropertyChanged();
                }
            }
        }

        // -------------------------------
        // Commands
        // -------------------------------
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        // -------------------------------
        // Events
        // -------------------------------
        public event Action<bool> RequestClose;

        public TransferLicenseViewModel()
        {
            ConfirmCommand = new RelayCommand(Confirm);
            CancelCommand = new RelayCommand(Cancel);
        }

        // -------------------------------
        // Command Handlers
        // -------------------------------
        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(NewSystemCode))
            {
                MessageBox.Show(
                    "Please enter a valid system code.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "License File (*.lic)|*.lic",
                FileName = "License.lic"
            };

            if (dlg.ShowDialog() != true) return;

            var success = LicenseManager.TransferLicense(NewSystemCode.Trim(), dlg.FileName);

            if (!success)
            {
                MessageBox.Show(
                    "License transfer failed or this license was already transferred.",
                    "Transfer Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(
                "License transferred successfully. The application will now close.",
                "Transfer Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RequestClose?.Invoke(true);

            Application.Current.Shutdown();
        }

        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }

        // -------------------------------
        // INotifyPropertyChanged
        // -------------------------------
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
