using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RenaimingToolCS.ViewModels;

namespace RenaimingToolCS.Views
{
    /// <summary>
    /// Interaction logic for LicenseWindow.xaml
    /// </summary>
    public partial class LicenseWindow : Window
    {
        private readonly string requestCode = LicenseManager.GenerateLicenseCode();

        public LicenseWindow()
        {
            InitializeComponent();
            txtSystemName.Text = Environment.MachineName;
            txtRequestCode.Text = requestCode;

            // Show expiry date if already licensed
            if (LicenseManager.CheckLicense())
            {
                var licPath = LicenseManager.GetLicensePath();
                if (File.Exists(licPath))
                {
                    try
                    {
                        using (var reader = new StreamReader(licPath))
                        {
                            var line = reader.ReadLine();
                            if (!string.IsNullOrEmpty(line) && line.StartsWith("LastDate:"))
                            {
                                var expDate = line.Replace("LastDate:", "").Trim();
                                lblExpiry.Text = $"License valid until {expDate}";
                                lblExpiry.Visibility = Visibility.Visible;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore read errors
                    }
                }
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "License Files (*.lic)|*.lic"
            };

            if (ofd.ShowDialog() == true)
            {
                txtFilePath.Text = ofd.FileName;
            }
        }

        private void btnActivate_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = txtFilePath.Text.Trim();
            if (!File.Exists(selectedFile))
            {
                MessageBox.Show("License file not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var targetPath = LicenseManager.GetLicensePath();
                File.Copy(selectedFile, targetPath, true);

                // For node-locked licenses, write the Activated flag first
                if (!LicenseManager.ActivateLicense())
                {
                    MessageBox.Show(
                        "This license is not valid for this machine.\nPlease make sure you selected the correct .lic file.",
                        "Activation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = LicenseManager.CheckLicenseDetailed();

                switch (result)
                {
                    case LicenseError.None:
                        MessageBox.Show("License activated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                        break;

                    case LicenseError.ServerUnreachable:
                        MessageBox.Show(
                            "Floating license file copied successfully.\n\n" +
                            "However, the license server could not be reached.\n" +
                            "Make sure the FloatingLicenseServer is running on the server machine and the port is correct.\n\n" +
                            "The license file has been saved — the app will work once the server is reachable.",
                            "Server Unreachable", MessageBoxButton.OK, MessageBoxImage.Warning);
                        DialogResult = true;
                        Close();
                        break;

                    case LicenseError.NoSeatsAvailable:
                        MessageBox.Show(
                            $"No seats available ({LicenseManager.LastSeatsInUse}/{LicenseManager.LastSeatsMax} in use).\n" +
                            "Ask another user to close the application to free a seat.",
                            "No Seats Available", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;

                    case LicenseError.InvalidLicense:
                        MessageBox.Show(
                            "The server does not recognise this license UID.\nPlease contact your administrator.",
                            "Invalid License", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    case LicenseError.Expired:
                        MessageBox.Show("This license has expired. Please renew it.", "Expired", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    default:
                        MessageBox.Show(
                            $"License check failed ({result}).\nPlease check the license file and try again.",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Activation failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnConnectServer_Click(object sender, RoutedEventArgs e)
        {
            var address = txtNetworkServer.Text.Trim();
            if (address == "")
            {
                MessageBox.Show("Enter a server address, e.g. vizserver:1122.", "Network License Server",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnConnectServer.IsEnabled = false;
            try
            {
                var ok = LicenseManager.TestAndSaveServerUrl(address, out var message);
                MessageBox.Show(message, ok ? "Connected" : "Connection Failed",
                    MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            finally
            {
                btnConnectServer.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
