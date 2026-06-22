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

            if (LicenseManager.CheckLicense())
            {
                string licPath = LicenseManager.GetLicensePath();

                if (File.Exists(licPath))
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(licPath))
                        {
                            string line = reader.ReadLine();
                            if (line != null && line.StartsWith("LastDate:"))
                            {
                                string expDate = line.Replace("LastDate:", "").Trim();
                                lblExpiry.Text = $"License valid until {expDate}";
                                lblExpiry.Visibility = Visibility.Visible;
                            }
                        }
                    }
                    catch
                    {
                        // intentionally ignored (same as VB)
                    }
                }
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
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
            string selectedFile = txtFilePath.Text.Trim();

            if (!File.Exists(selectedFile))
            {
                MessageBox.Show("License file not found.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string targetPath = LicenseManager.GetLicensePath();

                // Copy license to app location
                File.Copy(selectedFile, targetPath, true);

                // 🔑 ACTIVATE FIRST
                if (!LicenseManager.ActivateLicense())
                {
                    MessageBox.Show("License activation failed.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Optional safety check
                if (LicenseManager.CheckLicense())
                {
                    MessageBox.Show("License Activated!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("License activation verification failed.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Activation failed: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
