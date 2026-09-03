using Microsoft.Win32;
using RenaimingToolCS.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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

            // Ask where to save the transferred license
            var dlg = new SaveFileDialog
            {
                Filter = "License File (*.lic)|*.lic",
                FileName = "License.lic"
            };

            if (dlg.ShowDialog() != true)
                return;

            // Call backend transfer logic
            bool success = LicenseManager.TransferLicense(
                NewSystemCode.Trim(),
                dlg.FileName);

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

            // Close window
            RequestClose?.Invoke(true);

            // Shutdown app (ONLY HERE)
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
