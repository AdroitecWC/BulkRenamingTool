using RenaimingToolCS.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            // By default, the app starts in dark mode as the switch is unchecked.
            ApplyDarkTheme();
        }

        private void InputFolder_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.Copy; // Provide visual feedback
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
            e.Effects = DragDropEffects.Copy; // Provide visual feedback
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
            this.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            LogoImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/logo.png"));

            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.Background = Brushes.White;
                textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                textBox.Foreground = Brushes.Black;
            }

            foreach (var textBlock in FindVisualChildren<TextBlock>(this))
            {
                // If the TextBlock is inside a Button or DataGridColumnHeader, skip it
                // to avoid overriding their specific styles.
                if (FindVisualParent<Button>(textBlock) != null || FindVisualParent<DataGridColumnHeader>(textBlock) != null)
                {
                    continue;
                }

                // This check prevents the symbols inside the switch from changing color.
                if (textBlock.Text != "☀️" && textBlock.Text != "🌙")
                {
                    textBlock.Foreground = Brushes.Black;
                }
            }

            foreach (var dataGrid in FindVisualChildren<DataGrid>(this))
            {
                dataGrid.Background = Brushes.White;
                dataGrid.RowBackground = new SolidColorBrush(Color.FromRgb(248, 248, 248));
                dataGrid.AlternatingRowBackground = Brushes.White;
                dataGrid.Foreground = Brushes.Black;
                dataGrid.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            }
        }

        private void ApplyDarkTheme()
        {
            this.Background = new SolidColorBrush(Color.FromRgb(34, 34, 34));
            LogoImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/logo_light.png"));

            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.Background = new SolidColorBrush(Color.FromRgb(46, 46, 46));
                textBox.Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(136, 136, 136));
            }

            foreach (var textBlock in FindVisualChildren<TextBlock>(this))
            {
                // If the TextBlock is inside a Button or DataGridColumnHeader, skip it
                // to avoid overriding their specific styles.
                if (FindVisualParent<Button>(textBlock) != null || FindVisualParent<DataGridColumnHeader>(textBlock) != null)
                {
                    continue;
                }

                // This check prevents the symbols inside the switch from changing color.
                if (textBlock.Text != "☀️" && textBlock.Text != "🌙")
                {
                    textBlock.Foreground = (textBlock.FontWeight == FontWeights.Bold)
                        ? new SolidColorBrush(Color.FromRgb(204, 204, 204))
                        : new SolidColorBrush(Color.FromRgb(240, 240, 240));
                }
            }

            foreach (var dataGrid in FindVisualChildren<DataGrid>(this))
            {
                dataGrid.Background = new SolidColorBrush(Color.FromRgb(46, 46, 46));
                dataGrid.Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                dataGrid.RowBackground = new SolidColorBrush(Color.FromRgb(58, 58, 58));
                dataGrid.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(66, 66, 66));
                dataGrid.BorderBrush = new SolidColorBrush(Color.FromRgb(47, 76, 122)); // Aligned with XAML style #2F4C7A
            }
        }

        // Utility to get all children of a given type in the visual tree
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        // Utility to find a parent of a given type in the visual tree
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            if (parentObject is T parent)
                return parent;
            else
                return FindVisualParent<T>(parentObject);
        }
    }
}