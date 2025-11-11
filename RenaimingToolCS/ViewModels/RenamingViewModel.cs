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

            var latestFiles = Directory.GetFiles(folderPath)
                .GroupBy(path => Path.GetFileNameWithoutExtension(path))
                .Select(group => group.OrderByDescending(path => GetIterationNumber(path)).First())
                .ToList();

            foreach (var file in latestFiles)
            {
                var fileModel = new FileModel
                {
                    OriginalName = Path.GetFileName(file),
                    FullPath = file,
                    // REMOVED: OldName is no longer needed
                    NewName = string.Empty, // NewName starts empty for manual editing
                    IsSelected = true
                };
                fileModel.PropertyChanged += OnFileSelectionChanged;
                Files.Add(fileModel);
            }

            _isAllSelected = latestFiles.Any();
            OnPropertyChanged(nameof(IsAllSelected));
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
                    // Folder Safety Check region... (This code is correct and remains unchanged)
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

                creo.RunProe(folderPath);
                progressForm.Show();
                CreoFileHelper.PurgeFolder(folderPath);
                CreoSessionManager.Instance.InitializeCreoSession();
                //CloseBrowser();
                var session = CreoSessionManager.Instance.Session;
                var baseSession = (IpfcBaseSession)session;
                session.EraseUndisplayedModels();
                CreoFileHelper.OpenAllCreoModelsInFolder(folderPath);

                var loadedModels = baseSession.ListModels();
                int total = loadedModels.Count;

                for (int i = 0; i < total; i++)
                {
                    var model = loadedModels[i];
                    string modelOriginalFullName = CreateMappingKey(model.FileName);
                    int currentIndex = i + 1;
                    int percent = (int)(currentIndex * 100.0 / total);

                    if (renameMap.TryGetValue(modelOriginalFullName, out var newName))
                    {
                        // --- ADDED --- Store the original name before it changes.
                        string originalInstanceName = model.InstanceName;
                        //for custom names
                        //string confirmedNewName = ShowRenameConfirmationPopup(originalInstanceName, newName);
                        try
                        {
                            //model.Display();

                            model.Rename(newName, true);
                            //model.Rename(confirmedNewName, true);
                            model.Save();
                            renamedCount++;
                            
                            // --- CORRECTED --- Use the stored original name for logging.
                            progressForm.UpdateProgress(percent, $"Renamed ({currentIndex}/{total}): '{originalInstanceName}' to '{newName}'");
                            renamedModels.AppendLine($"SUCCESS: Renamed '{originalInstanceName}' to '{newName}'");
                        }
                        catch (Exception ex)
                        {
                            // --- CORRECTED --- Use the stored original name for logging.
                            errorMessages.AppendLine($"FAILED to rename '{originalInstanceName}' to '{newName}': {ex.Message}");
                        }
                    }
                }

                // --- Log Generation and Final Message (Unchanged) ---
                var finalLog = new StringBuilder();
                finalLog.AppendLine($"Rename process completed at: {DateTime.Now}");
                finalLog.AppendLine($"Total files renamed: {renamedCount}");
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
                CreoFileHelper.PurgeUsingBatch(folderPath);
                progressForm.Close();

                MessageBoxResult result = MessageBox.Show(
                    $"{renamedCount} file(s) renamed successfully.\n{(errorMessages.Length > 0 ? "Some errors occurred." : "")}\n\nWould you like to open the log file?",
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
                MessageBox.Show("A critical error occurred during the renaming process: " + ex.Message, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
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