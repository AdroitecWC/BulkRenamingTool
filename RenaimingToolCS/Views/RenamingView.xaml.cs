using RenaimingToolCS.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RenaimingToolCS.Views
{
    public partial class RenamingView : UserControl
    {
        public RenamingView()
        {
            InitializeComponent();
            this.DataContext = new RenamingViewModel();
            ApplyLightTheme();
            InitializeRenamingMode();
        }

        private void InitializeRenamingMode()
        {
            // Set the ComboBox to match the current mode from settings
            string currentMode = RenaimingToolCS.Helpers.SettingsManager.Instance.RenamingMode;
            foreach (ComboBoxItem item in RenamingModeComboBox.Items)
            {
                if (item.Tag.ToString() == currentMode)
                {
                    RenamingModeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void RenamingModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // The data binding and property setter will handle the mode change
            // This event is kept for any additional UI-specific logic if needed
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This is where the help documentation will go.", "Help", MessageBoxButton.OK, MessageBoxImage.Question);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Refresh the mode ComboBox after settings window closes
            InitializeRenamingMode();
        }
        // --- Drag and Drop Events ---
        private void InputFolder_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.Copy;
        }

        private void InputFolder_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (dropped.Length > 0 && Directory.Exists(dropped[0]))
                {
                    if (this.DataContext is RenamingViewModel vm)
                    {
                        string folderPath = dropped[0];
                        vm.InputFolderPath = folderPath;
                        vm.LoadFilesFromInputFolder(folderPath);
                        UpdateInputFolderStatus(folderPath); 
                    }
                }
            }
        }
        private void UpdateInputFolderStatus(string selectedPath)
        {
            // Change the circle's background to green and keep the number visible
            InputStatusCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17B93F"));

            // Display the selected path in the new border and make it visible
            SelectedInputPathTextBlock.Text = $"Selected: {selectedPath}";
            SelectedInputPathBorder.Visibility = Visibility.Visible;
        }
        private void UpdateExportStatus()
        {
            // Change the circle's background to green and keep the number visible
            Step2StatusCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17B93F"));
        }

        private void UpdateImportStatus(string selectedPath)
        {
            // Change the circle's background to green and keep the number visible
            ImportStatusCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17B93F"));

            SelectedImportPathTextBlock.Text = $"Selected: {System.IO.Path.GetFileName(selectedPath)}";
            SelectedImportPathBorder.Visibility = Visibility.Visible;
        }
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // Update the UI to give the user immediate feedback that the action was started.
            // The ViewModel command handles the actual export logic.
            UpdateExportStatus();
        }
        private void UpdateOutputStatus(string selectedPath)
        {
            // Change the circle's background to green and keep the number visible
            OutputStatusCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17B93F"));

            SelectedOutputPathTextBlock.Text = $"Selected: {selectedPath}";
            SelectedOutputPathBorder.Visibility = Visibility.Visible;
        }
        private void OutputFolder_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.Copy;
        }

        private void OutputFolder_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (dropped.Length > 0 && Directory.Exists(dropped[0]))
                {
                    if (this.DataContext is RenamingViewModel vm)
                    {
                        vm.OutputFolderPath = dropped[0];
                        UpdateOutputStatus(dropped[0]); 

                    }
                }
            }
        }

        private void Excel_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.Copy;
        }

        private void Excel_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && Path.GetExtension(files[0]).ToLower() is ".xls" or ".xlsx")
                {
                    if (DataContext is RenamingViewModel vm)
                    {
                        string filePath = files[0];
                        vm.ExcelFilePath = filePath;
                        vm.LoadExcelMapping(filePath);
                        UpdateImportStatus(filePath); 

                    }
                }
            }
        }

        // --- Click Events for Drop Zones ---
        private void InputDropZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is RenamingViewModel vm && vm.BrowseInputFolderCommand.CanExecute(null))
            {
                vm.BrowseInputFolderCommand.Execute(null);
                UpdateInputFolderStatus(vm.InputFolderPath);
            }
        }

        private void ExcelDropZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is RenamingViewModel vm && vm.BrowseExcelFileCommand.CanExecute(null))
            {
                string previousPath = vm.ExcelFilePath;
                vm.BrowseExcelFileCommand.Execute(null);

                if (!string.IsNullOrEmpty(vm.ExcelFilePath) && vm.ExcelFilePath != previousPath)
                {
                    UpdateImportStatus(vm.ExcelFilePath); // <-- Add this
                }
            }
        }

        private void OutputDropZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is RenamingViewModel vm && vm.BrowseOutputFolderCommand.CanExecute(null))
            {
                vm.BrowseOutputFolderCommand.Execute(null);
                UpdateOutputStatus(vm.OutputFolderPath); // <-- Add this
            }
        }

        private void PrefixSuffixTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is RenamingViewModel vm)
            {
                vm.ApplyPrefixSuffixToFiles();

                // Update Step 2 status circle to green when text is entered
                if (!string.IsNullOrEmpty(vm.PrefixSuffixTextInput))
                {
                    Step2StatusCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17B93F"));
                }
                else
                {
                    // Reset to gray if text is empty
                    Step2StatusCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#777"));
                }
            }
        }


        // --- Theme Switching Logic ---
        private void ThemeToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ApplyLightTheme();
        }

        private void ThemeToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyDarkTheme();
        }

        private void ApplyLightTheme()
        {
            // FIX: Add a null check to prevent crashing on startup
            ThemeToggleButton.IsChecked = true;
            if (LogoImage == null) return;

            this.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            LogoImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/logo.png"));

            foreach (var expander in FindVisualChildren<Expander>(this))
            {
                expander.Background = Brushes.White;
                expander.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                expander.Foreground = Brushes.Black;
            }

            foreach (var textBlock in FindVisualChildren<TextBlock>(this))
            {
                if (FindVisualParent<Button>(textBlock) != null) continue;
                bool isUntouchable = textBlock.Text == "☀️" || textBlock.Text == "🌙" || (textBlock.Text.Length == 1 && Char.IsDigit(textBlock.Text[0]));
                if (!isUntouchable)
                {
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                }
            }

            foreach (var grid in FindVisualChildren<Grid>(this))
            {
                if (grid.AllowDrop) grid.Background = new SolidColorBrush(Color.FromRgb(249, 249, 249));
            }

            foreach (var dataGrid in FindVisualChildren<DataGrid>(this))
            {
                dataGrid.Background = Brushes.White;
                dataGrid.Foreground = Brushes.Black;
                dataGrid.RowBackground = new SolidColorBrush(Color.FromRgb(248, 248, 248));
                dataGrid.AlternatingRowBackground = Brushes.White;
                dataGrid.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                dataGrid.HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                dataGrid.VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                dataGrid.GridLinesVisibility = DataGridGridLinesVisibility.All;
            }

            ForceDataGridRefresh();
        }
        private void ForceDataGridRefresh()
        {
            foreach (var dataGrid in FindVisualChildren<DataGrid>(this))
            {
                dataGrid.Items.Refresh();
                dataGrid.UpdateLayout();

                foreach (var row in FindVisualChildren<DataGridRow>(dataGrid))
                {
                    row.InvalidateVisual();
                }
            }
        }

        private void ApplyDarkTheme()
        {
            // FIX: Add a null check to prevent crashing on startup
            if (LogoImage == null) return;

            this.Background = new SolidColorBrush(Color.FromRgb(34, 34, 34));
            LogoImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/logo_light.png"));

            foreach (var expander in FindVisualChildren<Expander>(this))
            {
                expander.Background = new SolidColorBrush(Color.FromRgb(46, 46, 46));
                expander.BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68));
                expander.Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            }

            foreach (var textBlock in FindVisualChildren<TextBlock>(this))
            {
                if (FindVisualParent<Button>(textBlock) != null) continue;
                bool isUntouchable = textBlock.Text == "☀️" || textBlock.Text == "🌙" || (textBlock.Text.Length == 1 && Char.IsDigit(textBlock.Text[0]));
                if (!isUntouchable)
                {
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                }
            }

            foreach (var grid in FindVisualChildren<Grid>(this))
            {
                if (grid.AllowDrop) grid.Background = new SolidColorBrush(Color.FromRgb(58, 58, 58));
            }

            foreach (var dataGrid in FindVisualChildren<DataGrid>(this))
            {
                dataGrid.Background = new SolidColorBrush(Color.FromRgb(46, 46, 46));
                dataGrid.Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                dataGrid.RowBackground = new SolidColorBrush(Color.FromRgb(58, 58, 58));
                dataGrid.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(68, 68, 68));
                dataGrid.BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                dataGrid.HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                dataGrid.VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                dataGrid.GridLinesVisibility = DataGridGridLinesVisibility.All;
            }

            ForceDataGridRefresh();
        }


        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t) { yield return t; }
                    foreach (T childOfChild in FindVisualChildren<T>(child)) { yield return childOfChild; }
                }
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            else return FindVisualParent<T>(parentObject);
        }
    }
}