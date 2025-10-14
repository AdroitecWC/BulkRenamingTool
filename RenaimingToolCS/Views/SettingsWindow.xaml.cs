using System;
using System.Windows;
using RenaimingToolCS.ViewModels;  

namespace RenaimingToolCS.Views
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            this.Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsWindowViewModel vm)
            {
                // Prevent multiple subscriptions
                vm.RequestClose -= OnRequestClose;
                vm.RequestClose += OnRequestClose;
            }
        }

        private void OnRequestClose()
        {
            this.Close();
        }
    }


}
