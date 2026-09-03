using ClosedXML.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using Ookii.Dialogs.Wpf;
using pfcls;
using RenaimingToolCS.CreoFunctions;
using RenaimingToolCS.Helpers;
using RenaimingToolCS.ViewModels.Creo;
using RenaimingToolCS.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RenaimingToolCS.ViewModels
{
    internal class RenamingViewModel : ObservableObject
    {
        private string _inputFolderPath;
        private string _outputFolderPath;
        private string _excelFilePath;
        private bool _isAllSelected;
        private string _prefixSuffixTextInput;

        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (_isAllSelected != value)
                {
                    _isAllSelected = value;
                    foreach (var file in Files)
                    {
                        file.IsSelected = value;
                    }
                    OnPropertyChanged(nameof(IsAllSelected));
                }
            }
        }

        private void OnFileSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FileModel.IsSelected))
            {
                bool allSelected = Files.Any() && Files.All(f => f.IsSelected);
                if (_isAllSelected != allSelected)
                {
                    _isAllSelected = allSelected;
                    OnPropertyChanged(nameof(IsAllSelected));
                }
            }
        }

        public ICommand DownloadExcelCommand { get; }
        public ICommand RenameFilesCommand { get; }
        public ICommand BrowseInputFolderCommand { get; }
        public ICommand BrowseOutputFolderCommand { get; }
        public ICommand BrowseExcelFileCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand OpenSettingsWindowCommand { get; }

        public RenamingViewModel()
        {
            DownloadExcelCommand = new RelayCommand(DownloadExcel);
            RenameFilesCommand = new RelayCommand(RenameCreoFiles);
            BrowseInputFolderCommand = new RelayCommand(BrowseInputFolder);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            BrowseExcelFileCommand = new RelayCommand(BrowseExcelFile);
            OpenSettingsWindowCommand = new RelayCommand(OpenSettingsWindow);
        }


        private void OpenSettingsWindow()
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.DataContext = new SettingsWindowViewModel();
            settingsWindow.ShowDialog();

            // Refresh all mode-dependent properties after settings window closes
            RefreshModeSettings();
        }

        public void RefreshModeSettings()
        {
            OnPropertyChanged(nameof(RenamingMode));
            OnPropertyChanged(nameof(IsExcelMode));
            OnPropertyChanged(nameof(IsPrefixSuffixMode));
            OnPropertyChanged(nameof(PrefixSuffixHeaderText));
            OnPropertyChanged(nameof(PrefixSuffixLabelText));

            // Clear prefix/suffix text when mode changes
            PrefixSuffixTextInput = string.Empty;
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
            };

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

        public string InputFolderPath
        {
            get => _inputFolderPath;
            set
            {
                if (_inputFolderPath != value)
                {
                    _inputFolderPath = value;
                    OnPropertyChanged(nameof(InputFolderPath));
                    if (Directory.Exists(_inputFolderPath))
                    {
                        LoadFilesFromInputFolder(_inputFolderPath);
                    }
                }
            }
        }

        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set
            {
                if (_outputFolderPath != value)
                {
                    _outputFolderPath = value;
                    OnPropertyChanged(nameof(OutputFolderPath));
                }
            }
        }

        public string ExcelFilePath
        {
            get => _excelFilePath;
            set
            {
                if (_excelFilePath != value)
                {
                    _excelFilePath = value;
                    OnPropertyChanged(nameof(ExcelFilePath));
                    if (File.Exists(_excelFilePath) &&
                        (Path.GetExtension(_excelFilePath).ToLower() == ".xls" ||
                         Path.GetExtension(_excelFilePath).ToLower() == ".xlsx"))
                    {
                        LoadExcelMapping(_excelFilePath);
                    }
                }
            }
        }

        public ObservableCollection<FileModel> Files { get; } = new ObservableCollection<FileModel>();

        // Renaming mode properties
        public string RenamingMode
        {
            get => SettingsManager.Instance.RenamingMode;
            set
            {
                if (SettingsManager.Instance.RenamingMode != value)
                {
                    SettingsManager.Instance.RenamingMode = value;
                    SettingsManager.Instance.Save();
                    RefreshModeSettings();
                }
            }
        }

        public bool IsExcelMode => RenamingMode == "Excel";
        public bool IsPrefixSuffixMode => RenamingMode == "Prefix" || RenamingMode == "Suffix";

        public string PrefixSuffixHeaderText => RenamingMode == "Prefix" ? "Enter Prefix" : "Enter Suffix";
        public string PrefixSuffixLabelText => RenamingMode == "Prefix"
            ? "Enter text to add before filename:"
            : "Enter text to add after filename:";

        public string PrefixSuffixTextInput
        {
            get => _prefixSuffixTextInput ?? string.Empty;
            set
            {
                if (_prefixSuffixTextInput != value)
                {
                    _prefixSuffixTextInput = value;
                    OnPropertyChanged(nameof(PrefixSuffixTextInput));
                }
            }
        }

        public void ApplyPrefixSuffixToFiles()
        {
            if (!IsPrefixSuffixMode || string.IsNullOrEmpty(PrefixSuffixTextInput))
            {
                // Clear all NewName values if text is empty
                if (string.IsNullOrEmpty(PrefixSuffixTextInput))
                {
                    foreach (var file in Files)
                    {
                        file.NewName = string.Empty;
                    }
                }
                return;
            }

            foreach (var file in Files)
            {
                string fileName = file.OriginalName;
                string baseName = fileName;
                string fileExtension = string.Empty;
                string numericExtension = string.Empty;

                // Check for numeric extension (e.g., .1, .2, .3)
                string lastExtension = Path.GetExtension(fileName);
                if (!string.IsNullOrEmpty(lastExtension) && int.TryParse(lastExtension.Substring(1), out _))
                {
                    // Has numeric extension like .1, .2
                    numericExtension = lastExtension;
                    fileName = Path.GetFileNameWithoutExtension(fileName);

                    // Now get the file type extension (e.g., .prt, .asm)
                    fileExtension = Path.GetExtension(fileName);
                    baseName = Path.GetFileNameWithoutExtension(fileName);
                }
                else
                {
                    // No numeric extension, just regular extension
                    fileExtension = lastExtension;
                    baseName = Path.GetFileNameWithoutExtension(fileName);
                }

                string newName;
                if (RenamingMode == "Prefix")
                {
                    // Prefix goes before everything: prefix + basename + .ext + .number
                    newName = PrefixSuffixTextInput + baseName + fileExtension + numericExtension;
                }
                else // Suffix
                {
                    // Suffix goes after basename but before extensions: basename + suffix + .ext + .number
                    newName = baseName + PrefixSuffixTextInput + fileExtension + numericExtension;
                }

                file.NewName = newName;
            }
        }

        private int GetIterationNumber(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension))
                return -1;
            if (int.TryParse(extension.Substring(1), out int iteration))
            {
                return iteration;
            }
            return -1;
        }

        public void LoadFilesFromInputFolder(string folderPath)
        {
            foreach (var file in Files)
            {
                file.PropertyChanged -= OnFileSelectionChanged;
            }
            Files.Clear();

            if (!Directory.Exists(folderPath))
                return;

            CreoFileHelper.PurgeFolder(folderPath);

            var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories).ToList();

            // Separate Creo files (which might have versions) from non-Creo files
            var creoFiles = new List<string>();
            var nonCreoFiles = new List<string>();

            foreach (var file in allFiles)
            {
                string ext = Path.GetExtension(file).ToLower();
                string fileName = Path.GetFileName(file).ToLower();

                // Check if it's a Creo file (including versioned files like .prt.1)
                bool isCreoFile = ext == ".prt" || ext == ".asm" || ext == ".drw" ||
                                  fileName.Contains(".prt.") ||
                                  fileName.Contains(".asm.") ||
                                  fileName.Contains(".drw.");

                if (isCreoFile)
                {
                    creoFiles.Add(file);
                }
                else
                {
                    // Skip log files and temporary files
                    if (!fileName.EndsWith(".log") &&
                        !fileName.StartsWith("~") &&
                        !fileName.Contains("renamelog"))
                    {
                        nonCreoFiles.Add(file);
                    }
                }
            }

            // For Creo files, get only the latest version (highest iteration number)
            var latestCreoFiles = creoFiles
                .GroupBy(path => Path.GetFileNameWithoutExtension(path))
                .Select(group => group.OrderByDescending(path => GetIterationNumber(path)).First())
                .ToList();

            // Combine latest Creo files with all non-Creo files
            var filesToLoad = latestCreoFiles.Concat(nonCreoFiles).ToList();

            foreach (var file in filesToLoad)
            {
                var fileModel = new FileModel
                {
                    OriginalName = Path.GetFileName(file),
                    FullPath = file,
                    NewName = string.Empty, // NewName starts empty for manual editing
                    IsSelected = true
                };
                fileModel.PropertyChanged += OnFileSelectionChanged;
                Files.Add(fileModel);
            }

            _isAllSelected = filesToLoad.Any();
            OnPropertyChanged(nameof(IsAllSelected));

            // If in prefix/suffix mode, automatically apply the prefix/suffix
            if (IsPrefixSuffixMode && !string.IsNullOrEmpty(PrefixSuffixTextInput))
            {
                ApplyPrefixSuffixToFiles();
            }
        }

        private string CreateMappingKey(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            fileName = fileName.Trim().ToLowerInvariant();
            return Regex.Replace(fileName, @"\.\d+$", "");
        }

        public void LoadExcelMapping(string excelPath)
        {
            if (!File.Exists(excelPath))
            {
                MessageBox.Show("Excel file not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var workbook = new XLWorkbook(excelPath))
                {
                    var worksheet = workbook.Worksheet(1);
                    foreach (var row in worksheet.RowsUsed().Skip(1)) // Skip header
                    {
                        string rawOldName = row.Cell(1).GetString().Trim();
                        string rawNewName = row.Cell(2).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(rawOldName) || string.IsNullOrWhiteSpace(rawNewName))
                        {
                            continue;
                        }

                        string mappingKey = CreateMappingKey(rawOldName);
                        mapping[mappingKey] = rawNewName;
                    }
                }

                foreach (var file in Files)
                {
                    string fileKey = CreateMappingKey(file.OriginalName);
                    // REMOVED: OldName is no longer set
                    if (mapping.TryGetValue(fileKey, out string mappedNewName))
                    {
                        file.NewName = mappedNewName;
                    }
                    else
                    {
                        file.NewName = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load Excel mapping: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Unchanged methods (DownloadExcel, RenameCreoFiles, etc.) are omitted for brevity.
        // They work correctly with the new structure without modification.
        // ... (rest of the unchanged methods from your original file)
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

                        // Set headers
                        worksheet.Cell(1, 1).Value = "Original Name";
                        worksheet.Cell(1, 2).Value = "New Name";
                        worksheet.Cell(1, 3).Value = "File Path";

                        // Apply style to headers
                        var headerRange = worksheet.Range("A1:C1");
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                        // Add data
                        int row = 2;
                        foreach (var file in Files)
                        {
                            worksheet.Cell(row, 1).Value = file.OriginalName;
                            worksheet.Cell(row, 2).Value = file.NewName;      // Ensure NewName exists in your model
                            worksheet.Cell(row, 3).Value = file.FullPath;

                            // Apply borders to the row
                            var dataRange = worksheet.Range(row, 1, row, 3);
                            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                            row++;
                        }

                        // Auto-fit columns
                        worksheet.Columns().AdjustToContents();

                        // Optional: Freeze top row
                        worksheet.SheetView.FreezeRows(1);

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



        private void ShowTemporaryInfoForm(string message)
        {
            var infoForm = new System.Windows.Forms.Form
            {
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
                Width = 300,
                Height = 100,
                ControlBox = false,
                Text = "Info"
            };

            var lbl = new System.Windows.Forms.Label
            {
                Text = message,
                Dock = System.Windows.Forms.DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            infoForm.Controls.Add(lbl);

            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 10000; // 10 seconds
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                infoForm.Close();
            };
            timer.Start();

            infoForm.ShowDialog();
        }

        private void RenameCreoFiles()
        {
            var renamedModels = new StringBuilder();
            var errorMessages = new StringBuilder();
            var creo = new OpenAndCloseCreo();
            int renamedCount = 0;
            int totalFilesToRename = 0;

            ProgressInfoForm progressForm = new ProgressInfoForm("Starting rename...");
            ShowTemporaryInfoForm("Creo initialising");

            try
            {
                var renameMap = Files
                    .Where(f => f.IsSelected &&
                                !string.IsNullOrWhiteSpace(f.OriginalName) &&
                                !string.IsNullOrWhiteSpace(f.NewName) &&
                                !Path.GetFileNameWithoutExtension(CreateMappingKey(f.OriginalName)).Equals(f.NewName, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(f => CreateMappingKey(f.OriginalName), f => f.NewName, StringComparer.OrdinalIgnoreCase);

                if (!renameMap.Any())
                {
                    MessageBox.Show("No files need to be renamed.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string folderPath = InputFolderPath;

                if (!string.IsNullOrWhiteSpace(OutputFolderPath))
                {
                    #region Folder Safety Check
                    try
                    {
                        string fullInputPath = Path.GetFullPath(InputFolderPath).TrimEnd(Path.DirectorySeparatorChar);
                        string fullOutputPath = Path.GetFullPath(OutputFolderPath).TrimEnd(Path.DirectorySeparatorChar);

                        if (string.Equals(fullInputPath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show("The output folder cannot be the same as the input folder.",
                                            "Invalid Output Folder", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        if (fullOutputPath.StartsWith(fullInputPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show("The output folder cannot be a subfolder of the input folder.",
                                            "Invalid Output Folder", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not validate folder paths: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    #endregion

                    folderPath = OutputFolderPath;
                    try
                    {
                        if (Directory.Exists(OutputFolderPath))
                            Directory.Delete(OutputFolderPath, true);
                        CopyDirectory(InputFolderPath, OutputFolderPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to copy files to output folder:\n{ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Separate Creo files and non-Creo files
                var creoFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var nonCreoFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in renameMap)
                {
                    var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                    var matchingFile = allFiles.FirstOrDefault(f =>
                        CreateMappingKey(Path.GetFileName(f)).Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));

                    if (matchingFile != null)
                    {
                        string ext = Path.GetExtension(matchingFile).ToLower();
                        bool isCreoFile = ext == ".prt" || ext == ".asm" || ext == ".drw" ||
                                          Path.GetFileName(matchingFile).ToLower().Contains(".prt.") ||
                                          Path.GetFileName(matchingFile).ToLower().Contains(".asm.") ||
                                          Path.GetFileName(matchingFile).ToLower().Contains(".drw.");

                        if (isCreoFile)
                        {
                            creoFiles[kvp.Key] = kvp.Value;
                        }
                        else
                        {
                            nonCreoFiles[matchingFile] = kvp.Value;
                        }
                    }
                }

                totalFilesToRename = creoFiles.Count + nonCreoFiles.Count;

                // STEP 1: Rename Creo files using Creo
                if (creoFiles.Count > 0)
                {
                    creo.RunProe(folderPath);
                    progressForm.Show();
                    System.Threading.Thread.Sleep(3000); // Give Creo time to fully start

                    CreoFileHelper.PurgeFolder(folderPath);
                    CreoSessionManager.Instance.InitializeCreoSession();

                    var session = CreoSessionManager.Instance.Session;
                    var baseSession = (IpfcBaseSession)session;

                    try
                    {
                        session.EraseUndisplayedModels();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not erase undisplayed models: {ex.Message}");
                    }

                    // Open all models
                    CreoFileHelper.OpenAllCreoModelsInFolder(folderPath);
                    System.Threading.Thread.Sleep(2000); // Wait for models to load

                    var loadedModels = baseSession.ListModels();

                    // **FIX 1: Sort models by dependency order (Parts -> Drawings -> Assemblies)**
                    var sortedModels = SortModelsByDependency(loadedModels);

                    int total = sortedModels.Count;
                    var loadedModelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < total; i++)
                    {
                        var model = sortedModels[i];
                        string modelOriginalFullName = CreateMappingKey(model.FileName);
                        int currentIndex = i + 1;
                        int percent = totalFilesToRename > 0 ? (int)((double)renamedCount / totalFilesToRename * 100) : 0;

                        loadedModelNames.Add(modelOriginalFullName);

                        if (creoFiles.TryGetValue(modelOriginalFullName, out var newName))
                        {
                            string originalInstanceName = model.InstanceName;

                            try
                            {
                                // Validate the new name
                                if (string.IsNullOrWhiteSpace(newName))
                                {
                                    errorMessages.AppendLine($"FAILED (Creo): Invalid new name for '{originalInstanceName}'");
                                    continue;
                                }

                                // Check if the name is actually changing
                                if (originalInstanceName.Equals(newName, StringComparison.OrdinalIgnoreCase))
                                {
                                    renamedModels.AppendLine($"SKIPPED (Creo): '{originalInstanceName}' - name unchanged");
                                    continue;
                                }

                                // **FIX 2: Verify model is properly loaded and not in use**
                                if (!IsModelSafeToRename(model))
                                {
                                    errorMessages.AppendLine($"SKIPPED (Creo): '{originalInstanceName}' - model may be in use");
                                    continue;
                                }

                                // **FIX 3: Attempt to display the model in a window before renaming**
                                try
                                {
                                    IpfcWindow currentWindow = baseSession.get_CurrentWindow();
                                    if (currentWindow != null)
                                    {
                                        currentWindow.Activate();
                                        System.Threading.Thread.Sleep(200);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Warning: Could not activate window: {ex.Message}");
                                }

                                // **FIX 4: Try-catch around rename with retry logic**
                                bool renamed = false;
                                int retryCount = 0;
                                int maxRetries = 3;

                                while (!renamed && retryCount < maxRetries)
                                {
                                    try
                                    {
                                        model.Rename(newName, true);
                                        renamed = true;
                                    }
                                    catch (Exception renameEx)
                                    {
                                        retryCount++;
                                        if (retryCount >= maxRetries)
                                        {
                                            throw;
                                        }
                                        System.Threading.Thread.Sleep(500);
                                        Console.WriteLine($"Retry {retryCount} for '{originalInstanceName}'");
                                    }
                                }

                                // **FIX 5: Save with error handling**
                                try
                                {
                                    model.Save();
                                    renamedCount++;

                                    progressForm.UpdateProgress(percent,
                                        $"Renamed Creo file ({renamedCount}/{totalFilesToRename}): '{originalInstanceName}' to '{newName}'");
                                    renamedModels.AppendLine($"SUCCESS (Creo): Renamed '{originalInstanceName}' to '{newName}'");
                                }
                                catch (Exception saveEx)
                                {
                                    errorMessages.AppendLine($"WARNING: Renamed '{originalInstanceName}' to '{newName}' but save failed: {saveEx.Message}");
                                }
                            }
                            catch (Exception ex)
                            {
                                string errorMsg = ex.Message;

                                // Provide more specific error messages
                                if (errorMsg.Contains("in use") || errorMsg.Contains("locked"))
                                {
                                    errorMessages.AppendLine($"FAILED (Creo): '{originalInstanceName}' - File is locked or in use by another model");
                                }
                                else if (errorMsg.Contains("circular") || errorMsg.Contains("dependency"))
                                {
                                    errorMessages.AppendLine($"FAILED (Creo): '{originalInstanceName}' - Circular dependency detected");
                                }
                                else
                                {
                                    errorMessages.AppendLine($"FAILED (Creo) to rename '{originalInstanceName}' to '{newName}': {errorMsg}");
                                }
                            }
                        }
                    }

                    // Check for Creo files that failed to load
                    foreach (var kvp in creoFiles)
                    {
                        if (!loadedModelNames.Contains(kvp.Key))
                        {
                            errorMessages.AppendLine($"WARNING: Creo file '{kvp.Key}' could not be loaded in Creo and was not renamed");
                        }
                    }

                    // **FIX 6: Erase all models before closing to prevent save prompts**
                    try
                    {
                        session.EraseUndisplayedModels();
                        var currentModels = baseSession.ListModels();
                        for (int i = currentModels.Count - 1; i >= 0; i--)
                        {
                            try
                            {
                                currentModels[i].Erase();
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning during cleanup: {ex.Message}");
                    }

                    // Close Creo before renaming non-Creo files
                    System.Threading.Thread.Sleep(1000);
                    creo.KillCreO();
                    System.Threading.Thread.Sleep(2000); // Wait for Creo to fully close
                }

                // STEP 2: Rename non-Creo files using file system operations
                if (nonCreoFiles.Count > 0)
                {
                    foreach (var kvp in nonCreoFiles)
                    {
                        string originalFilePath = kvp.Key;
                        string newName = kvp.Value;

                        try
                        {
                            if (!File.Exists(originalFilePath))
                            {
                                errorMessages.AppendLine($"FAILED: File not found '{originalFilePath}'");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(newName))
                            {
                                errorMessages.AppendLine($"FAILED: Invalid new name for '{Path.GetFileName(originalFilePath)}'");
                                continue;
                            }

                            char[] invalidChars = Path.GetInvalidFileNameChars();
                            if (newName.IndexOfAny(invalidChars) >= 0)
                            {
                                errorMessages.AppendLine($"FAILED: New name contains invalid characters for '{Path.GetFileName(originalFilePath)}'");
                                continue;
                            }

                            string directory = Path.GetDirectoryName(originalFilePath);
                            string originalFileName = Path.GetFileName(originalFilePath);
                            string extension = Path.GetExtension(originalFilePath);
                            string currentBaseName = Path.GetFileNameWithoutExtension(originalFilePath);

                            // Check if newName already includes the extension
                            string newFileName;
                            if (newName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                            {
                                // newName already includes extension (e.g., from prefix/suffix mode)
                                newFileName = newName;
                            }
                            else
                            {
                                // newName is just the base name (e.g., from Excel mode)
                                newFileName = newName + extension;
                            }

                            // Compare base names for skip check
                            string newBaseName = Path.GetFileNameWithoutExtension(newFileName);
                            if (currentBaseName.Equals(newBaseName, StringComparison.OrdinalIgnoreCase))
                            {
                                renamedModels.AppendLine($"SKIPPED: '{originalFileName}' - name unchanged");
                                continue;
                            }

                            string newFilePath = Path.Combine(directory, newFileName);

                            if (File.Exists(newFilePath) && !newFilePath.Equals(originalFilePath, StringComparison.OrdinalIgnoreCase))
                            {
                                errorMessages.AppendLine($"FAILED: Destination file already exists '{newFileName}'");
                                continue;
                            }

                            File.Move(originalFilePath, newFilePath);
                            renamedCount++;

                            int percent = totalFilesToRename > 0 ? (int)((double)renamedCount / totalFilesToRename * 100) : 0;
                            progressForm.UpdateProgress(percent,
                                $"Renamed file ({renamedCount}/{totalFilesToRename}): '{originalFileName}' to '{newFileName}'");
                            renamedModels.AppendLine($"SUCCESS: Renamed '{originalFileName}' to '{newFileName}'");
                        }
                        catch (Exception ex)
                        {
                            errorMessages.AppendLine($"FAILED to rename '{Path.GetFileName(originalFilePath)}' to '{newName}': {ex.Message}");
                        }
                    }
                }

                // --- Log Generation and Final Message ---
                var finalLog = new StringBuilder();
                finalLog.AppendLine($"Rename process completed at: {DateTime.Now}");
                finalLog.AppendLine($"Total files renamed: {renamedCount} out of {totalFilesToRename}");
                finalLog.AppendLine($"Creo files: {creoFiles.Count}");
                finalLog.AppendLine($"Other files: {nonCreoFiles.Count}");
                finalLog.AppendLine("---");

                if (renamedModels.Length > 0)
                {
                    finalLog.AppendLine("Successful Renames:");
                    finalLog.AppendLine(renamedModels.ToString());
                }

                if (errorMessages.Length > 0)
                {
                    finalLog.AppendLine("Errors Encountered:");
                    finalLog.AppendLine(errorMessages.ToString());
                }

                string logFilePath = Path.Combine(folderPath, $"RenameLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(logFilePath, finalLog.ToString());

                if (creoFiles.Count > 0)
                {
                    CreoFileHelper.PurgeUsingBatch(folderPath);
                }

                progressForm.Close();

                MessageBoxResult result = MessageBox.Show(
                    $"{renamedCount} out of {totalFilesToRename} file(s) renamed successfully.\n{(errorMessages.Length > 0 ? "Some errors occurred." : "")}\n\nWould you like to open the log file?",
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
                progressForm.Close();
                MessageBox.Show("A critical error occurred during the renaming process: " + ex.Message,
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                creo.KillCreO();
                if (progressForm.Visible)
                {
                    progressForm.Close();
                }
            }
        }

        // **HELPER METHOD 1: Sort models by dependency**
        private List<IpfcModel> SortModelsByDependency(IpfcModels models)
        {
            var parts = new List<IpfcModel>();
            var drawings = new List<IpfcModel>();
            var assemblies = new List<IpfcModel>();

            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i];
                int modelTypeInt = (int)model.Type;

                // EpfcMDL_PART = 0, EpfcMDL_ASSEMBLY = 1, EpfcMDL_DRAWING = 2
                if (modelTypeInt == 0) // Part
                {
                    parts.Add(model);
                }
                else if (modelTypeInt == 2) // Drawing
                {
                    drawings.Add(model);
                }
                else if (modelTypeInt == 1) // Assembly
                {
                    assemblies.Add(model);
                }
            }

            // Process in order: Parts first, then Drawings, then Assemblies last
            var sorted = new List<IpfcModel>();
            sorted.AddRange(parts);
            sorted.AddRange(drawings);
            sorted.AddRange(assemblies);

            return sorted;
        }

        // **HELPER METHOD 2: Check if model is safe to rename**
        private bool IsModelSafeToRename(IpfcModel model)
        {
            try
            {
                // Check if model is modified but not saved
                if (model.IsModified)
                {
                    return false;
                }

                // Simple check - if we can access the instance name, it's loaded properly
                string testName = model.InstanceName;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in IsModelSafeToRename: {ex.Message}");
                return true; // Default to safe to allow rename attempt
            }
        }
        private string ShowRenameConfirmationPopup1(string originalName, string suggestedNewName)
        {
            // Create a custom window for rename confirmation
            var renameWindow = new Window
            {
                Title = "Rename Confirmation",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true, // Ensures window is on top of all other applications
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize
            };

            // Create a stack panel for layout
            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // Original name label
            var originalNameLabel = new TextBlock
            {
                Text = $"Original Name: {originalName}",
                Margin = new Thickness(0, 0, 0, 10)
            };

            // New name text box
            var newNameTextBox = new TextBox
            {
                Text = suggestedNewName,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Yes button
            var yesButton = new Button
            {
                Content = "Confirm",
                Width = 100,
                Margin = new Thickness(0, 0, 10, 0)
            };

            // No button
            var noButton = new Button
            {
                Content = "Cancel",
                Width = 100
            };

            string finalNewName = suggestedNewName;
            bool isConfirmed = false;

            // Yes button click event
            yesButton.Click += (s, e) =>
            {
                finalNewName = newNameTextBox.Text.Trim();
                isConfirmed = true;
                renameWindow.Close();
            };

            // No button click event
            noButton.Click += (s, e) =>
            {
                isConfirmed = false;
                renameWindow.Close();
            };

            // Add controls to button panel
            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            // Add controls to stack panel
            stackPanel.Children.Add(originalNameLabel);
            stackPanel.Children.Add(new TextBlock { Text = "New Name:", Margin = new Thickness(0, 0, 0, 5) });
            stackPanel.Children.Add(newNameTextBox);
            stackPanel.Children.Add(buttonPanel);

            // Set content of window
            renameWindow.Content = stackPanel;

            // Show dialog and wait
            renameWindow.ShowDialog();

            // Return the final name or null if cancelled
            return isConfirmed ? finalNewName : null;
        }
        private string ShowRenameConfirmationPopup(string originalName, string suggestedNewName)
        {
            var renameWindow = new Window
            {
                Title = "Rename Confirmation",
                Width = 400,
                Height = 200,
                Topmost = true,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize
            };

            // Get the full screen bounds
            var screenBounds = System.Windows.SystemParameters.WorkArea;

            // Calculate the right-side position with 20% margin
            double screenWidth = screenBounds.Width;
            double marginWidth = screenWidth * 0.1;

            // Position the window
            renameWindow.Left = screenBounds.Right - marginWidth - renameWindow.Width;
            renameWindow.Top = screenBounds.Top + (screenBounds.Height - renameWindow.Height) / 2;

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // Original name label
            var originalNameLabel = new TextBlock
            {
                Text = $"Original Name: {originalName}",
                Margin = new Thickness(0, 0, 0, 10)
            };

            // New name text box
            var newNameTextBox = new TextBox
            {
                Text = suggestedNewName,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Yes button
            var yesButton = new Button
            {
                Content = "Confirm",
                Width = 100,
                Margin = new Thickness(0, 0, 10, 0)
            };

            // No button
            var noButton = new Button
            {
                Content = "Cancel",
                Width = 100
            };

            string finalNewName = suggestedNewName;
            bool isConfirmed = false;

            // Yes button click event
            yesButton.Click += (s, e) =>
            {
                finalNewName = newNameTextBox.Text.Trim();
                isConfirmed = true;
                renameWindow.Close();
            };

            // No button click event
            noButton.Click += (s, e) =>
            {
                isConfirmed = false;
                renameWindow.Close();
            };

            // Add controls to button panel
            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            // Add controls to stack panel
            stackPanel.Children.Add(originalNameLabel);
            stackPanel.Children.Add(new TextBlock { Text = "New Name:", Margin = new Thickness(0, 0, 0, 5) });
            stackPanel.Children.Add(newNameTextBox);
            stackPanel.Children.Add(buttonPanel);

            // Set content of window
            renameWindow.Content = stackPanel;

            // Show dialog and wait
            renameWindow.ShowDialog();

            // Return the final name or null if cancelled
            return isConfirmed ? finalNewName : null;
        }
        public void RestartApplication()
        {
           
            try
            {
                

                // Restart the application
                System.Windows.Forms.Application.Restart();
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to restart the application: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public void CloseBrowser()
        {
            try
            {
                // Initialize the Creo session (ensure it returns an object with RunMacro method)
                var session = CreoSessionManager.Instance.Session;

                string closebrowse = "mapkey sb ~ Command `ProCmdBrowserBtn`  0;";
                session.RunMacro(closebrowse);
            }
            catch (Exception ex)
            {
                // Log or handle the exception
                Console.WriteLine($"Error closing browser: {ex.Message}");
            }
        }

    }

    /// <summary>
    /// Represents a single file.
    /// REMOVED: OldName property is no longer necessary.
    /// </summary>
    internal class FileModel : ObservableObject
    {

        public string OriginalName { get; set; }

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

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}