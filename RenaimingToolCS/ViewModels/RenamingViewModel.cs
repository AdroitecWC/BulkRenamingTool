using ClosedXML.Excel;
using Ookii.Dialogs.Wpf;
using pfcls;

using RenaimingToolCS.CreoFunctions;
using RenaimingToolCS.Helpers;
using RenaimingToolCS.ViewModels.Creo;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            fileName = fileName.Trim().ToLowerInvariant();

            // Remove known Creo extensions and optional version numbers
            var pattern = @"\.(prt|asm|drw|frm|sec)(\.\d+)?$";
            fileName = Regex.Replace(fileName, pattern, "");

            // Remove trailing .1, .2, etc. if not already removed
            fileName = Regex.Replace(fileName, @"\.\d+$", "");

            return fileName;
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
                    string folderPath = Path.GetDirectoryName(excelPath);
                    string logFilePath = Path.Combine(folderPath, $"DuplicateLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                    File.WriteAllText(logFilePath, duplicateLog.ToString());

                    var result = MessageBox.Show(
                        "Duplicate entries found and skipped.\nWould you like to open the duplicate log file?",
                        "Duplicate Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = logFilePath,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load Excel mapping: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private string StripCreoExtensions(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return fileName;

            // Normalize casing
            fileName = fileName.ToLowerInvariant();

            // Regular expression to match "name.ext.version"
            var match = Regex.Match(fileName, @"^(.*)\.(prt|asm|drw)\.\d+$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Fallback: strip after last dot
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

                        
                        worksheet.Cell(1, 1).Value = "Original Name";
                        worksheet.Cell(1, 2).Value = "New Name";
                        worksheet.Cell(1, 3).Value = "File Path";

                        int row = 2;
                        foreach (var file in Files)
                        {
                            worksheet.Cell(row, 3).Value = file.FullPath;       // Add FullPath property in your model
                            worksheet.Cell(row, 1).Value = file.OriginalName;
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
            var renamedModels = new StringBuilder();
            var errorMessages = new StringBuilder();
            var creo = new OpenAndCloseCreo();
            int renamedCount = 0;

            // Create and show the progress form
            ProgressInfoForm progressForm = new ProgressInfoForm("Starting rename...");
            progressForm.Show();

            try
            {
                var renameMap = Files
                    .Where(f => !string.IsNullOrWhiteSpace(f.OldName) && !string.IsNullOrWhiteSpace(f.NewName))
                    .ToDictionary(f => f.OldName.Trim(), f => f.NewName.Trim(), StringComparer.OrdinalIgnoreCase);

                string folderPath = InputFolderPath;

                if (!string.IsNullOrWhiteSpace(OutputFolderPath))
                {
                    folderPath = OutputFolderPath;

                    try
                    {
                        // Clean existing output folder
                        if (Directory.Exists(OutputFolderPath))
                            Directory.Delete(OutputFolderPath, true);

                        // Copy files and subdirectories
                        CopyDirectory(InputFolderPath, OutputFolderPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to copy files to output folder:\n{ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }


                creo.RunProe(folderPath);
                CreoFileHelper.PurgeFolder(folderPath);
                CreoSessionManager.Instance.InitializeCreoSession();

                var session = CreoSessionManager.Instance.Session;
                var baseSession = (IpfcBaseSession)session;

                CreoFileHelper.OpenAllCreoModelsInFolder(InputFolderPath);

                var loadedModels = baseSession.ListModels();
                int total = loadedModels.Count;

                for (int i = 0; i < total; i++)
                {
                    var model = loadedModels[i];
                    string currentName = model.InstanceName;

                    int currentIndex = i + 1;
                    int percent = (int)(currentIndex * 100.0 / total);
                    //progressForm.UpdateProgress(percent, $"Renaming: {currentName}");

                    if (renameMap.TryGetValue(currentName, out var newName))
                    {
                        if (!string.Equals(currentName, newName, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                model.Rename(newName, true);
                                model.Save();
                                renamedCount++;
                                progressForm.UpdateProgress(percent,$"Renamed '({currentIndex}/{total}): {currentName}' to '{newName}'");
                                renamedModels.AppendLine($"Renamed '{currentName}' to '{newName}'");
                            }
                            catch (Exception ex)
                            {
                                errorMessages.AppendLine($"Failed to rename '{currentName}' to '{newName}': {ex.Message}");
                            }
                        }
                    }
                }

                // Save log
                var finalLog = new StringBuilder();
                if (renamedModels.Length > 0)
                    finalLog.AppendLine("Renamed Models:\n" + renamedModels.ToString());
                if (errorMessages.Length > 0)
                    finalLog.AppendLine("Errors:\n" + errorMessages.ToString());

                string logFilePath = Path.Combine(folderPath, $"RenameLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(logFilePath, finalLog.ToString());
                progressForm.Close();
                MessageBoxResult result = MessageBox.Show(
                    $"{renamedCount} file(s) renamed.\nOpen log?",
                    "Rename Complete", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logFilePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                creo.KillCreO();
                progressForm.Close();
            }
        }



        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string targetFilePath = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFilePath);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, targetSubDir);
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
        private string _fullPath;
        public string FullPath
        {
            get => _fullPath;
            set => SetProperty(ref _fullPath, value);
        }
       
    }
}
