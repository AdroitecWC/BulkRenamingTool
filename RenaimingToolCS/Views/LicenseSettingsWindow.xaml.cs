using System.Diagnostics;
using System.Windows;
using RenaimingToolCS.ViewModels;

namespace RenaimingToolCS.Views
{
    public partial class LicenseSettingsWindow : Window
    {
        public LicenseSettingsWindow()
        {
            InitializeComponent();
            Loaded += LicenseSettingsWindow_Loaded;
        }

        private void LicenseSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LicenseSettingsViewModel vm)
            {
                vm.RequestClose += () => Close();

                vm.RequestShutdown += () =>
                {
                    Close();

                    // Deactivate/Change License leave the install with no valid license --
                    // relaunch the exe so the activation window comes back up on its own
                    // instead of leaving the user to find and reopen it manually.
                    try
                    {
                        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath)) Process.Start(exePath);
                    }
                    catch
                    {
                        // Best-effort -- if relaunch fails, the app still closes below.
                    }

                    if (Application.Current.MainWindow != null)
                        Application.Current.MainWindow.Close();

                    Application.Current.Shutdown();
                };
            }

            RefreshBorrowPanels();
        }

        /// <summary>Shows the Borrow panel, the "Borrow active until X" panel, or the
        /// "no floating license" note, depending on whether a borrow is currently active
        /// and whether a floating license is configured at all. Re-run after any
        /// borrow/return so the UI stays in sync.</summary>
        private void RefreshBorrowPanels()
        {
            if (LicenseManager.TryGetActiveBorrow(out var expiresLocal))
            {
                pnlBorrow.Visibility = Visibility.Collapsed;
                pnlBorrowActive.Visibility = Visibility.Visible;
                txtBorrowUnavailable.Visibility = Visibility.Collapsed;
                txtBorrowStatus.Text = $"Borrow active until {expiresLocal:yyyy-MM-dd HH:mm}";
            }
            else if (LicenseManager.IsFloatingConfigured())
            {
                pnlBorrow.Visibility = Visibility.Visible;
                pnlBorrowActive.Visibility = Visibility.Collapsed;
                txtBorrowUnavailable.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlBorrow.Visibility = Visibility.Collapsed;
                pnlBorrowActive.Visibility = Visibility.Collapsed;
                txtBorrowUnavailable.Visibility = Visibility.Visible;
            }
        }

        private void btnBorrow_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(txtBorrowAmount.Text.Trim(), out var amount) || amount <= 0)
            {
                MessageBox.Show("Enter a positive number to borrow.", "Borrow",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var isHours = cmbBorrowUnit.SelectedIndex == 1;
            var hours = isHours ? amount : amount * 24;

            btnBorrow.IsEnabled = false;
            try
            {
                var ok = LicenseManager.BorrowSeat(hours, out var message);
                MessageBox.Show(message, ok ? "Borrowed" : "Borrow Failed",
                    MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
                if (ok) RefreshBorrowPanels();
            }
            finally
            {
                btnBorrow.IsEnabled = true;
            }
        }

        private void btnReturnBorrow_Click(object sender, RoutedEventArgs e)
        {
            btnReturnBorrow.IsEnabled = false;
            try
            {
                var ok = LicenseManager.ReturnBorrowedSeat(out var message);
                MessageBox.Show(message, ok ? "Returned" : "Return Failed",
                    MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                RefreshBorrowPanels();
            }
            finally
            {
                btnReturnBorrow.IsEnabled = true;
            }
        }
    }
}
