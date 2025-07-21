using RenaimingToolCS.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RenaimingToolCS.Views
{
    public partial class RenamingView : UserControl
    {
        public RenamingView()
        {
            InitializeComponent();
            this.DataContext = new RenamingViewModel();
        }

        private void InputFolder_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void InputFolder_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (dropped.Length > 0)
                {
                    string path = dropped[0];
                    if (Directory.Exists(path))
                    {
                        var vm = this.DataContext as RenamingViewModel;
                        if (vm != null)
                        {
                            vm.InputFolderPath = path;
                            vm.LoadFilesFromInputFolder(path);
                        }
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
                if (dropped.Length > 0)
                {
                    string path = dropped[0];
                    if (Directory.Exists(path))
                    {
                        var vm = this.DataContext as RenamingViewModel;
                        if (vm != null)
                        {
                            vm.OutputFolderPath = path;
                        }
                    }
                }
            }
        }


        private void Excel_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void Excel_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && Path.GetExtension(files[0]).ToLower() is ".xls" or ".xlsx")
                {
                    var vm = DataContext as RenamingViewModel;
                    vm.ExcelFilePath = files[0];
                    vm.LoadExcelMapping(files[0]);
                }
            }
        }

        private void ThemeToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ToggleSymbol.Text = "☀️";
            ApplyLightTheme();
        }

        private void ThemeToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ToggleSymbol.Text = "🌙";
            ApplyDarkTheme();
        }

        private void ApplyLightTheme()
        {
            this.Background = new SolidColorBrush(Color.FromRgb(211, 211, 240)); // LightGray background

            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.Background = Brushes.White;
                textBox.BorderBrush = Brushes.Gray;
                textBox.Foreground = Brushes.Black; // ✅ Add this to ensure text is visible
            }

            foreach (var textBlock in FindVisualChildren<TextBlock>(this))
            {
                // Set specific label texts to black in light mode
                if (textBlock.Text == "Input Folder:" ||
                    textBlock.Text == "Output Folder:" ||
                    textBlock.Text == "Excel File:" ||
                    textBlock.Text == "Files to Rename")
                {
                    textBlock.Foreground = Brushes.Black;
                }
                else
                {
                    textBlock.Foreground = Brushes.Black; // ✅ Or use default dark foreground
                }
            }

            foreach (var dataGrid in FindVisualChildren<DataGrid>(this))
            {
                dataGrid.Background = Brushes.White;
                dataGrid.RowBackground = Brushes.WhiteSmoke;
                dataGrid.AlternatingRowBackground = Brushes.Gainsboro;
                dataGrid.Foreground = Brushes.Black; // ✅ Make sure DataGrid text is also visible
            }

            ToggleSymbol.Text = "☀️";
        }


        private void ApplyDarkTheme()
        {
            this.Background = new SolidColorBrush(Color.FromRgb(34, 34, 34)); // dark bg

            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.Background = new SolidColorBrush(Color.FromRgb(46, 46, 46));
                textBox.Foreground = Brushes.White;
                textBox.BorderBrush = Brushes.Gray;
            }

            foreach (var textBlock in FindVisualChildren<TextBlock>(this))
            {
                textBlock.Foreground = Brushes.White;
            }

            foreach (var dataGrid in FindVisualChildren<DataGrid>(this))
            {
                dataGrid.Background = new SolidColorBrush(Color.FromRgb(46, 46, 46));
                dataGrid.Foreground = Brushes.White;
                dataGrid.RowBackground = new SolidColorBrush(Color.FromRgb(58, 58, 58));
                dataGrid.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(66, 66, 66));
            }
        }

        // Utility to get all children of a given type in visual tree
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {   
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                        yield return t;

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                        yield return childOfChild;
                }
            }
        }

    }
}
