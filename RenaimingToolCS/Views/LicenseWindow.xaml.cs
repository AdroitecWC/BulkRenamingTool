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
                var licPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\License\License.lic");
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
                var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\License\License.lic");
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(selectedFile, targetPath, true);

                if (LicenseManager.CheckLicense())
                {
                    MessageBox.Show("License Activated!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Invalid license file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Activation failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
