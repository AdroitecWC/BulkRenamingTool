using ClosedXML.Excel;
using Ookii.Dialogs.Wpf;
using pfcls;

using RenaimingToolCS.CreoFunctions;
using RenaimingToolCS.Helpers;
using RenaimingToolCS.ViewModels.Creo;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace RenaimingToolCS.ViewModels
{
    /// <summary>
    /// ViewModel class that handles logic for the renaming tool.
    /// Binds to RenamingView.xaml to handle user input and display files.
    /// </summary>
    internal class RenamingViewModel : ObservableObject
    {
        private string _inputFolderPath;
        private string _outputFolderPath;
        private string _excelFilePath;

        public ICommand DownloadExcelCommand { get; }
        public ICommand RenameFilesCommand { get; }

        public ICommand BrowseInputFolderCommand { get; }
        public ICommand BrowseOutputFolderCommand { get; }
        public ICommand BrowseExcelFileCommand { get; }



        public RenamingViewModel()
        {
            DownloadExcelCommand = new RelayCommand(DownloadExcel);
            RenameFilesCommand= new RelayCommand(RenameCreoFiles);
            BrowseInputFolderCommand = new RelayCommand(BrowseInputFolder);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            BrowseExcelFileCommand = new RelayCommand(BrowseExcelFile);

        }
        private void BrowseInputFolder()
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select Input Folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == true)
            {
                InputFolderPath = dialog.SelectedPath;
                LoadFilesFromInputFolder(InputFolderPath);
            }
        }




        private void BrowseOutputFolder()
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select Output Folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            }
            ;

            if (dialog.ShowDialog() == true)
            {
                OutputFolderPath = dialog.SelectedPath;
            }
        }


        private void BrowseExcelFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls"
            };

            if (dialog.ShowDialog() == true)
            {
                ExcelFilePath = dialog.FileName;
                LoadExcelMapping(ExcelFilePath);
            }
        }

        /// <summary>
        /// Path of the folder containing files to be renamed.
        /// </summary>
        public string InputFolderPath
        {
            get => _inputFolderPath;
            set => SetProperty(ref _inputFolderPath, value);
        }

        /// <summary>
        /// Path of the folder where renamed files will be stored.
        /// </summary>
        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set => SetProperty(ref _outputFolderPath, value);
        }
        public string ExcelFilePath  // <--- add this property
        {
            get => _excelFilePath;
            set => SetProperty(ref _excelFilePath, value);
        }

        /// <summary>
        /// List of files displayed in the DataGrid with their original and new names.
        /// </summary>
        public ObservableCollection<FileModel> Files { get; } = new ObservableCollection<FileModel>();

        /// <summary>
        /// Loads file names from the input folder, applies Creo purge, and fills the Files collection.
        /// </summary>
        /// <param name="folderPath">Path to the input folder</param>
        public void LoadFilesFromInputFolder(string folderPath)
        {
            Files.Clear();
            if (!Directory.Exists(folderPath))
                return;

            CreoFileHelper.PurgeFolder(folderPath);

            Files.Clear();

            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                Files.Add(new FileModel
                {
                    OriginalName = fileName,
                    OldName = string.Empty,  // Initially empty or same as OriginalName
                    NewName = string.Empty
                });
            }
        }

        private string NormalizeCreoName(string fileName)
        {
            // Remove known Creo extensions with version numbers
            var pattern = @"\.(prt|asm|drw|frm|sec)\.\d+$";
            return Regex.Replace(fileName, pattern, "", RegexOptions.IgnoreCase);
        }

        public void LoadExcelMapping(string excelPath)
        {
            if (!File.Exists(excelPath))
            {
                MessageBox.Show("Excel file not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var oldNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicateLog = new StringBuilder();

            try
            {
                using (var workbook = new XLWorkbook(excelPath))
                {
                    var worksheet = workbook.Worksheet(1);

                    foreach (var row in worksheet.RowsUsed().Skip(1))
                    {
                        string rawOld = row.Cell(1).GetString().Trim();
                        string newName = row.Cell(2).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(rawOld) || string.IsNullOrWhiteSpace(newName))
                            continue;

                        // Consistently normalize
                        string normalizedOld = NormalizeCreoName(rawOld).Trim().ToLowerInvariant();
                        string normalizedNew = newName.Trim();

                        if (!oldNameSet.Add(normalizedOld))
                        {
                            duplicateLog.AppendLine($"Duplicate OLD name skipped: '{normalizedOld}'");
                            continue;
                        }

                        if (!newNameSet.Add(normalizedNew))
                        {
                            duplicateLog.AppendLine($"Duplicate NEW name skipped: '{normalizedNew}'");
                            continue;
                        }

                        mapping[normalizedOld] = normalizedNew;
                    }
                }
                var uniqueFiles = Files
                     .GroupBy(f => NormalizeCreoName(f.OriginalName).Trim().ToLowerInvariant())
                     .Select(g => g.First()) // Only one file per normalized name
                     .ToList();

                // Then map only these:
                foreach (var file in uniqueFiles)
                {
                    string fileKey = NormalizeCreoName(file.OriginalName).Trim().ToLowerInvariant();
                    file.OldName = fileKey;

                    if (mapping.TryGetValue(fileKey, out string mappedNewName))
                    {
                        file.NewName = mappedNewName;
                    }
                }

                if (duplicateLog.Length > 0)
                {
                    MessageBox.Show(
                        $"Duplicate entries found and skipped:\n\n{duplicateLog}",
                        "Duplicate Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load Excel mapping: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private string StripCreoExtensions(string fileName)
        {
            // Handles files like "PART1.prt.1" or "ASSY1.asm.9"
            string[] knownExtensions = { ".prt", ".asm", ".drw" };

            foreach (var ext in knownExtensions)
            {
                int extIndex = fileName.IndexOf(ext + ".");
                if (extIndex >= 0)
                {
                    return fileName.Substring(0, extIndex);
                }
            }

            // If no match found, fallback to removing after last dot (optional)
            int lastDotIndex = fileName.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                return fileName.Substring(0, lastDotIndex);
            }

            return fileName;
        }


        private void DownloadExcel()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = "FileList.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Files");

                        worksheet.Cell(1, 1).Value = "File Path";
                        worksheet.Cell(1, 2).Value = "Original Name";

                        int row = 2;
                        foreach (var file in Files)
                        {
                            worksheet.Cell(row, 1).Value = file.FullPath;       // Add FullPath property in your model
                            worksheet.Cell(row, 2).Value = file.OriginalName;
                            row++;
                        }

                        workbook.SaveAs(saveFileDialog.FileName);
                    }

                    MessageBox.Show("Excel file exported successfully.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export Excel: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void RenameCreoFiles()
        {
            OpenAndCloseCreo creo = new OpenAndCloseCreo();
            var renamedLog = new StringBuilder();
            var errorLog = new StringBuilder();
            // Initialize Creo session
            

            try
            {
                var WorkingDir = InputFolderPath;
                creo.RunProe(WorkingDir);
                CreoSessionManager.Instance.InitializeCreoSession();
                IpfcBaseSession session = CreoSessionManager.Instance.Session;
                session.EraseUndisplayedModels();
                // 1. Purge and open all drawings in folder
                CreoFileHelper.PurgeFolder(InputFolderPath);
                CreoFileHelper.OpenAllDrawingsInFolder(InputFolderPath);



                var baseSession = (IpfcBaseSession)session;
                var loadedModels = baseSession.ListModels();

                // 2. Create rename map from Files collection (DataGrid)
                var renameMap = Files
                    .Where(f => !string.IsNullOrWhiteSpace(f.OldName) && !string.IsNullOrWhiteSpace(f.NewName))
                    .ToDictionary(f => f.OldName, f => f.NewName);

                // 3. Rename logic
                foreach (IpfcModel model in loadedModels)
                {
                    var currentName = model.InstanceName;

                    // Only match model base name (remove extensions)
                    var baseName = StripCreoExtensions(currentName);

                    if (renameMap.TryGetValue(baseName, out var newName))
                    {
                        if (!string.Equals(baseName, newName, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                model.Rename(newName, true);
                                renamedLog.AppendLine($"Renamed '{baseName}' to '{newName}'");
                            }
                            catch (Exception ex)
                            {
                                errorLog.AppendLine($"Failed to rename '{baseName}' to '{newName}': {ex.Message}");
                            }
                        }
                    }
                }

                // 4. Log results
                var finalLog = new StringBuilder();
                if (renamedLog.Length > 0)
                {
                    finalLog.AppendLine("Renamed Models:\n" + renamedLog.ToString());
                }

                if (errorLog.Length > 0)
                {
                    finalLog.AppendLine("Errors:\n" + errorLog.ToString());
                }

                MessageBox.Show(finalLog.ToString(), "Rename Result", MessageBoxButton.OK, MessageBoxImage.Information);
                creo.KillCreO();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                creo.KillCreO();
            }
        }
        private void MoveRenamedFilesToOutputFolder(string baseName, string sourceFolder, string destinationFolder)
        {
            // Move all related Creo files (.prt.*, .asm.*, .drw.*) with the base name
            string[] extensions = { ".prt", ".asm", ".drw" };

            foreach (var ext in extensions)
            {
                var files = Directory.GetFiles(sourceFolder, $"{baseName}{ext}.*"); // handles .prt.1, .asm.2 etc.

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(destinationFolder, fileName);

                    try
                    {
                        File.Copy(file, destPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to move file {fileName}: {ex.Message}", "Move Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }



    }

    /// <summary>
    /// Represents a single file with original and new name properties.
    /// </summary>
    internal class FileModel : ObservableObject
    {
        public string OriginalName { get; set; }
        private string _oldName;
        public string OldName
        {
            get => _oldName;
            set => SetProperty(ref _oldName, value);
        }

        private string _newName;
        public string NewName
        {
            get => _newName;
            set => SetProperty(ref _newName, value);
        }
        public string FullPath { get; set; }
    }
}
