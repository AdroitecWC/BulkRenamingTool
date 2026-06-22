using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using RenaimingToolCS.CreoFunctions;
using RenaimingToolCS.Helpers;
using RenaimingToolCS.Views;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace RenaimingToolCS.ViewModels
{
    internal class SettingsWindowViewModel : ObservableObject
    {
        public event Action RequestClose;

        #region Properties
        // Properties for the currently selected path in the ComboBox
        private string _currentCreoPath;
        public string CurrentCreoPath
        {
            get => _currentCreoPath;
            set => SetProperty(ref _currentCreoPath, value);
        }

        private string _currentProDirectory;
        public string CurrentProDirectory
        {
            get => _currentProDirectory;
            set => SetProperty(ref _currentProDirectory, value);
        }

        private string _currentProCommMsgExe;
        public string CurrentProCommMsgExe
        {
            get => _currentProCommMsgExe;
            set => SetProperty(ref _currentProCommMsgExe, value);
        }

        private string _currentVbApiRegisterBatPath;
        public string CurrentVbApiRegisterBatPath
        {
            get => _currentVbApiRegisterBatPath;
            set => SetProperty(ref _currentVbApiRegisterBatPath, value);
        }

        private string _currentCommonFilesFolder;
        public string CurrentCommonFilesFolder
        {
            get => _currentCommonFilesFolder;
            set => SetProperty(ref _currentCommonFilesFolder, value);
        }

        private string _renamingMode;
        public string RenamingMode
        {
            get => _renamingMode;
            set => SetProperty(ref _renamingMode, value);
        }
        private bool _isDeactivateSelected = true;
        public bool IsDeactivateSelected
        {
            get => _isDeactivateSelected;
            set
            {
                if (SetProperty(ref _isDeactivateSelected, value))
                {
                    if (value)
                    {
                        _isTransferSelected = false;
                        OnPropertyChanged(nameof(IsTransferSelected));
                    }

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
                if (SetProperty(ref _isTransferSelected, value))
                {
                    if (value)
                    {
                        _isDeactivateSelected = false;
                        OnPropertyChanged(nameof(IsDeactivateSelected));
                    }

                    OnPropertyChanged(nameof(PrimaryLicenseActionText));
                }
            }
        }

        public string PrimaryLicenseActionText =>
            IsTransferSelected ? "Transfer License" : "Deactivate License";
        // History collections bound to the ComboBox's ItemsSource
        public ObservableCollection<string> CreoPathHistory { get; }
        public ObservableCollection<string> ProDirectoryHistory { get; }
        public ObservableCollection<string> ProCommMsgExeHistory { get; }
        public ObservableCollection<string> VbApiRegisterBatPathHistory { get; }
        public ObservableCollection<string> CommonFilesFolderHistory { get; }
        #endregion

        #region Commands
      
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand PrimaryLicenseActionCommand { get; }
        #endregion

        public SettingsWindowViewModel()
        {
            // Load current values and history from SettingsManager
           

            RenamingMode = SettingsManager.Instance.RenamingMode;

            // Initialize commands
           

            SaveCommand = new RelayCommand(OnSave);
            CancelCommand = new RelayCommand(OnCancel);
            PrimaryLicenseActionCommand = new RelayCommand(ExecutePrimaryLicenseAction);
            ConfirmCommand = new RelayCommand(OnConfirm);
        }

        private void OnSave()
        {
            SaveSettings();
            // Do not close window
        }
        private void OnCancel()
        {
            // Discard changes and close
            RequestClose?.Invoke();
        }
        private void OnConfirm()
        {
           
            RequestClose?.Invoke();
        }
        private void closeCreo()
        {
            string[] creoProcessNames = { "xtop", "creosvcs", "nmsd", "pfclscom", "pro_comm_msg" };

            foreach (var name in creoProcessNames)
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit();
                        Console.WriteLine($"Killed {name} process: {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to kill {name} process {process.Id}: {ex.Message}");
                    }
                }

                if (processes.Length == 0)
                {
                    Console.WriteLine($"No {name} processes found.");
                }
            }
        }

        private void SaveSettings()
        {
            try
            {
         

                // Persist all changes to the settings file
                SettingsManager.Instance.Save();

                // Only run batch file creation if Creo paths are configured
                if (!string.IsNullOrEmpty(CurrentCreoPath) && !string.IsNullOrEmpty(CurrentProDirectory))
                {
                    // Show message box and get user response
                    var result = MessageBox.Show(
                        "Do you want to close Creo processes before saving settings?",
                        "Info",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        // User clicked Yes -> close Creo processes
                        closeCreo();
                    }

                    // Run the batch file creation logic
                    var bat = new CreateAndRunBatchFile
                    {
                        CreoPath = SettingsManager.Instance.CurrentCreoPath,
                        ProDirectory = SettingsManager.Instance.CurrentProDirectory,
                        ProCommMsgExe = SettingsManager.Instance.CurrentProCommMsgExe,
                        VbApiRegisterBatPath = SettingsManager.Instance.CurrentVbApiRegisterBatPath
                    };
                    //bat.CreateAndRunBatchFileMethod();
                }

                MessageBox.Show("Settings saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Console.WriteLine("Settings saved.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Browse Dialogs
     
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
                "This will permanently deactivate the license and close the application.\n\n" +
                "You will need a new license to continue using the software.\n\n" +
                "Do you want to continue?",
                "Deactivate License",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            if (LicenseManager.DeactivateLicense())
            {
                MessageBox.Show(
                    "License deactivated successfully. The application will now close.",
                    "License Deactivated",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Application.Current.Shutdown(); // 👈 matches your app flow
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
        private void ExecutePrimaryLicenseAction(object obj)
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
   
        #endregion

    }
}