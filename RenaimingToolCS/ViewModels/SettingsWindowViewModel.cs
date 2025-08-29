using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using RenaimingToolCS.CreoFunctions;
using RenaimingToolCS.Helpers;
using System;
using System.Windows.Input;

namespace RenaimingToolCS.ViewModels
{
    internal class SettingsWindowViewModel : ObservableObject
    {
        public event Action RequestClose;

        // Settings properties for all 5 paths
        private string _creoPath;
        public string CreoPath
        {
            get => _creoPath;
            set => SetProperty(ref _creoPath, value);
        }

        private string _proDirectory;
        public string ProDirectory
        {
            get => _proDirectory;
            set => SetProperty(ref _proDirectory, value);
        }

        private string _proCommMsgExe;
        public string ProCommMsgExe
        {
            get => _proCommMsgExe;
            set => SetProperty(ref _proCommMsgExe, value);
        }

        private string _vbApiRegisterBatPath;
        public string VbApiRegisterBatPath
        {
            get => _vbApiRegisterBatPath;
            set => SetProperty(ref _vbApiRegisterBatPath, value);
        }

        private string _commonFilesFolder;
        public string CommonFilesFolder
        {
            get => _commonFilesFolder;
            set => SetProperty(ref _commonFilesFolder, value);
        }

        // Commands for browse buttons
        public ICommand BrowseCreoPathCommand { get; }
        public ICommand BrowseProDirectoryCommand { get; }
        public ICommand BrowseProCommMsgExeCommand { get; }
        public ICommand BrowseVbApiRegisterBatCommand { get; }
        public ICommand BrowseCommonFilesFolderCommand { get; }

        // Save / Cancel / Confirm commands
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ConfirmCommand { get; }

        public SettingsWindowViewModel()
        {
            // Load values from SettingsManager or default to empty string
            CreoPath = SettingsManager.Instance.CreoPath ?? string.Empty;
            ProDirectory = SettingsManager.Instance.ProDirectory ?? string.Empty;
            ProCommMsgExe = SettingsManager.Instance.ProCommMsgExe ?? string.Empty;
            VbApiRegisterBatPath = SettingsManager.Instance.VbApiRegisterBatPath ?? string.Empty;
            CommonFilesFolder = SettingsManager.Instance.CommonFilesFolder ?? string.Empty;

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
            //SaveSettings();
            RequestClose?.Invoke();
        }

        private void OnCancel()
        {
            // Optionally reset changes
            RequestClose?.Invoke();
        }

        private void SaveSettings()
        {


            // Save each setting to your SettingsManager or wherever you persist configs
            

            SettingsManager.Instance.CreoPath = CreoPath;
            SettingsManager.Instance.ProDirectory = ProDirectory;
            SettingsManager.Instance.ProCommMsgExe = ProCommMsgExe;
            SettingsManager.Instance.VbApiRegisterBatPath = VbApiRegisterBatPath;
            SettingsManager.Instance.CommonFilesFolder = CommonFilesFolder;

            var bat = new CreateAndRunBatchFile
            {
                CreoPath = SettingsManager.Instance.CreoPath,
                ProDirectory = SettingsManager.Instance.ProDirectory,
                ProCommMsgExe = SettingsManager.Instance.ProCommMsgExe,
                VbApiRegisterBatPath = SettingsManager.Instance.VbApiRegisterBatPath
                // CommonFilesFolder might not be needed for batch
            };

            bat.CreateAndRunBatchFileMethod();
            Console.WriteLine("Settings saved.");
        }

        // Browse dialogs implementations
        private void BrowseForCreoPath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Creo Executable (parametric.exe)|parametric.exe",
                Title = "Select parametric.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                CreoPath = dialog.FileName;
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
                ProDirectory = dialog.SelectedPath;
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
                ProCommMsgExe = dialog.FileName;
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
                VbApiRegisterBatPath = dialog.FileName;
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
                CommonFilesFolder = dialog.SelectedPath;
            }
        }
    }
}
