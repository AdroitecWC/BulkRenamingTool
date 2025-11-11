using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using RenaimingToolCS.CreoFunctions;
using RenaimingToolCS.Helpers;
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

        // History collections bound to the ComboBox's ItemsSource
        public ObservableCollection<string> CreoPathHistory { get; }
        public ObservableCollection<string> ProDirectoryHistory { get; }
        public ObservableCollection<string> ProCommMsgExeHistory { get; }
        public ObservableCollection<string> VbApiRegisterBatPathHistory { get; }
        public ObservableCollection<string> CommonFilesFolderHistory { get; }
        #endregion

        #region Commands
        public ICommand BrowseCreoPathCommand { get; }
        public ICommand BrowseProDirectoryCommand { get; }
        public ICommand BrowseProCommMsgExeCommand { get; }
        public ICommand BrowseVbApiRegisterBatCommand { get; }
        public ICommand BrowseCommonFilesFolderCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ConfirmCommand { get; }
        #endregion

        public SettingsWindowViewModel()
        {
            // Load current values and history from SettingsManager
            CurrentCreoPath = SettingsManager.Instance.CurrentCreoPath;
            CreoPathHistory = SettingsManager.Instance.CreoPathHistory;

            CurrentProDirectory = SettingsManager.Instance.CurrentProDirectory;
            ProDirectoryHistory = SettingsManager.Instance.ProDirectoryHistory;

            CurrentProCommMsgExe = SettingsManager.Instance.CurrentProCommMsgExe;
            ProCommMsgExeHistory = SettingsManager.Instance.ProCommMsgExeHistory;

            CurrentVbApiRegisterBatPath = SettingsManager.Instance.CurrentVbApiRegisterBatPath;
            VbApiRegisterBatPathHistory = SettingsManager.Instance.VbApiRegisterBatPathHistory;

            CurrentCommonFilesFolder = SettingsManager.Instance.CurrentCommonFilesFolder;
            CommonFilesFolderHistory = SettingsManager.Instance.CommonFilesFolderHistory;

            RenamingMode = SettingsManager.Instance.RenamingMode;

            // Initialize commands
            BrowseCreoPathCommand = new RelayCommand(BrowseForCreoPath);
            BrowseProDirectoryCommand = new RelayCommand(BrowseForProDirectory);
            BrowseProCommMsgExeCommand = new RelayCommand(BrowseForProCommMsgExe);
            BrowseVbApiRegisterBatCommand = new RelayCommand(BrowseForVbApiRegisterBat);
            BrowseCommonFilesFolderCommand = new RelayCommand(BrowseForCommonFilesFolder);

            SaveCommand = new RelayCommand(OnSave);
            CancelCommand = new RelayCommand(OnCancel);
            ConfirmCommand = new RelayCommand(OnConfirm);
        }

        private void OnSave()
        {
            SaveSettings();
            // Do not close window
        }

        private void OnConfirm()
        {
            // Confirm restart with user
            var result = MessageBox.Show("Do you want to restart the application to apply the new settings?",
                                         "Confirm Restart", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {



                var res = new RenamingViewModel();
                    res.RestartApplication();
            }
        }

       
        private void OnCancel()
        {
            // Discard changes and close
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
                // Update renaming mode (independent of Creo paths)
                SettingsManager.Instance.RenamingMode = RenamingMode;

                // Update the 'Current' path properties in the SettingsManager
                SettingsManager.Instance.CurrentCreoPath = CurrentCreoPath;
                SettingsManager.Instance.CurrentProDirectory = CurrentProDirectory;
                SettingsManager.Instance.CurrentProCommMsgExe = CurrentProCommMsgExe;
                SettingsManager.Instance.CurrentVbApiRegisterBatPath = CurrentVbApiRegisterBatPath;
                SettingsManager.Instance.CurrentCommonFilesFolder = CurrentCommonFilesFolder;

                // Add the current values to their respective history lists
                SettingsManager.Instance.AddPathToHistory(CurrentCreoPath, CreoPathHistory);
                SettingsManager.Instance.AddPathToHistory(CurrentProDirectory, ProDirectoryHistory);
                SettingsManager.Instance.AddPathToHistory(CurrentProCommMsgExe, ProCommMsgExeHistory);
                SettingsManager.Instance.AddPathToHistory(CurrentVbApiRegisterBatPath, VbApiRegisterBatPathHistory);
                SettingsManager.Instance.AddPathToHistory(CurrentCommonFilesFolder, CommonFilesFolderHistory);

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
        private void BrowseForCreoPath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Creo Executable (parametric.exe)|parametric.exe",
                Title = "Select parametric.exe"
            };
            if (dialog.ShowDialog() == true)
            {
                CurrentCreoPath = dialog.FileName;
            }
        }

        private void BrowseForProDirectory()
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select the Parametric folder (PRO_DIRECTORY)"
            };
            if (dialog.ShowDialog() == true)
            {
                CurrentProDirectory = dialog.SelectedPath;
            }
        }

        private void BrowseForProCommMsgExe()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable (pro_comm_msg.exe)|pro_comm_msg.exe",
                Title = "Select pro_comm_msg.exe"
            };
            if (dialog.ShowDialog() == true)
            {
                CurrentProCommMsgExe = dialog.FileName;
            }
        }

        private void BrowseForVbApiRegisterBat()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Batch File (vb_api_register.bat)|vb_api_register.bat",
                Title = "Select vb_api_register.bat"
            };
            if (dialog.ShowDialog() == true)
            {
                CurrentVbApiRegisterBatPath = dialog.FileName;
            }
        }

        private void BrowseForCommonFilesFolder()
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select the Common Files folder"
            };
            if (dialog.ShowDialog() == true)
            {
                CurrentCommonFilesFolder = dialog.SelectedPath;
            }
        }
        #endregion
    }
}