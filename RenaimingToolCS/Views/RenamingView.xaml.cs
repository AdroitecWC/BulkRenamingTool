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
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This is where the help documentation will go.", "Help", MessageBoxButton.OK, MessageBoxImage.Question);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This is where the settings window will open.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        vm.InputFolderPath = dropped[0];
                        vm.LoadFilesFromInputFolder(dropped[0]);
                    }
                }
            }
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
                        vm.ExcelFilePath = files[0];
                        vm.LoadExcelMapping(files[0]);
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
            }
        }

        private void ExcelDropZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is RenamingViewModel vm && vm.BrowseExcelFileCommand.CanExecute(null))
            {
                vm.BrowseExcelFileCommand.Execute(null);
            }
        }

        private void OutputDropZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is RenamingViewModel vm && vm.BrowseOutputFolderCommand.CanExecute(null))
            {
                vm.BrowseOutputFolderCommand.Execute(null);
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