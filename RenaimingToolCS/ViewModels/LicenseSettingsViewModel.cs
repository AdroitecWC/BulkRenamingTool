using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using RenaimingToolCS.Helpers;
using RenaimingToolCS.Views;

namespace RenaimingToolCS.ViewModels
{
    public class LicenseSettingsViewModel : INotifyPropertyChanged
    {
        // -------------------------------
        // Floating-license gating
        // -------------------------------
        // Deactivate/Transfer only make sense for a node-locked license — a floating license
        // (whether via a Floating .lic file or an Activation-Code-based activation with no
        // .lic file at all) is bound to the license server, not this machine, so the whole
        // Deactivate/Transfer choice is hidden and only "Change License" is offered instead.
        // IsFloatingConfigured() (not IsFloatingLicense(), which only ever looks at a .lic
        // file) is what actually covers both cases.
        private readonly bool _isFloating = LicenseManager.IsFloatingConfigured();

        public Visibility NodeLockedActionsVisibility => _isFloating ? Visibility.Collapsed : Visibility.Visible;

        // -------------------------------
        // License Action Selection
        // -------------------------------
        private bool _isDeactivateSelected = true;
        public bool IsDeactivateSelected
        {
            get => _isDeactivateSelected;
            set
            {
                if (_isDeactivateSelected != value)
                {
                    _isDeactivateSelected = value;

                    if (value)
                    {
                        _isTransferSelected = false;
                        OnPropertyChanged(nameof(IsTransferSelected));
                    }

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PrimaryLicenseActionText));
                }
            }
        }

        private bool _isTransferSelected;
        public bool IsTransferSelected
        {
            get => _isTransferSelected;
            set
            {
                if (_isTransferSelected != value)
                {
                    _isTransferSelected = value;

                    if (value)
                    {
                        _isDeactivateSelected = false;
                        OnPropertyChanged(nameof(IsDeactivateSelected));
                    }

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PrimaryLicenseActionText));
                }
            }
        }

        public string PrimaryLicenseActionText => IsTransferSelected ? "Transfer License" : "Deactivate License";

        // -------------------------------
        // Commands
        // -------------------------------
        public ICommand DeactivateLicenseCommand { get; }
        public ICommand TransferLicenseCommand { get; }
        public ICommand PrimaryLicenseActionCommand { get; }
        public ICommand ChangeLicenseTypeCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ConfirmCommand { get; }

        // -------------------------------
        // Events
        // -------------------------------
        public event Action RequestClose;
        public event Action RequestShutdown;

        // -------------------------------
        // Constructor
        // -------------------------------
        public LicenseSettingsViewModel()
        {
            DeactivateLicenseCommand = new RelayCommand(DeactivateLicense);
            TransferLicenseCommand = new RelayCommand(TransferLicense);
            PrimaryLicenseActionCommand = new RelayCommand(ExecutePrimaryLicenseAction);
            ChangeLicenseTypeCommand = new RelayCommand(ChangeLicenseType);

            CancelCommand = new RelayCommand(Cancel);
            ConfirmCommand = new RelayCommand(Confirm);
        }

        // -------------------------------
        // Actions
        // -------------------------------
        private void ExecutePrimaryLicenseAction()
        {
            if (IsTransferSelected)
            {
                TransferLicense();
                return;
            }

            if (IsDeactivateSelected)
            {
                DeactivateLicense();
                return;
            }

            MessageBox.Show(
                "Please select a license action.",
                "No Action Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void DeactivateLicense()
        {
            if (!IsDeactivateSelected)
            {
                MessageBox.Show(
                    "Please select 'Deactivate License' option to proceed.",
                    "Action Not Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "This will permanently deactivate the license and restart the application." +
                Environment.NewLine + Environment.NewLine +
                "The activation window will reopen right away — you'll need a new license to continue using the software." +
                Environment.NewLine + Environment.NewLine +
                "Do you want to continue?",
                "Deactivate License",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            if (LicenseManager.DeactivateLicense())
            {
                MessageBox.Show(
                    "License deactivated successfully. The application will now restart.",
                    "License Deactivated",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                RequestShutdown?.Invoke();
            }
            else
            {
                MessageBox.Show(
                    "License deactivation failed or no active license found.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void TransferLicense()
        {
            if (_isFloating)
            {
                MessageBox.Show(
                    "Network licenses cannot be transferred — they are bound to the license server, not this machine.",
                    "Not Available",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!IsTransferSelected)
            {
                MessageBox.Show(
                    "Please select 'Transfer License' option to proceed.",
                    "Action Not Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var vm = new TransferLicenseViewModel();
            var win = new TransferLicenseWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            win.ShowDialog();
        }

        private void ChangeLicenseType()
        {
            var result = MessageBox.Show(
                "This will clear the current license activation (returning any borrowed/floating seat) and restart the application." +
                Environment.NewLine + Environment.NewLine +
                "The activation window will reopen right away so you can activate a new license, node-locked or floating." +
                Environment.NewLine + Environment.NewLine +
                "Do you want to continue?",
                "Change License",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            LicenseManager.PrepareForLicenseTypeSwitch();
            RequestShutdown?.Invoke();
        }

        private void Cancel()
        {
            RequestClose?.Invoke();
        }

        private void Confirm()
        {
            RequestClose?.Invoke();
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
