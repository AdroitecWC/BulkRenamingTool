using RenaimingToolCS.ViewModels;
using RenaimingToolCS.Views;
using System;
using System.Configuration;
using System.Data;
using System.Windows;

namespace RenaimingToolCS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)

        {
            base.OnStartup(e);

            // Default ShutdownMode is OnLastWindowClose — with no window open yet,
            // closing the LicenseWindow (the only open window at that point) would
            // trigger shutdown immediately, racing with the MainWindow.Show() call
            // right after it and closing the whole app instead of continuing on.
            // Switch to explicit shutdown until we know which window is staying open.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Check if the license is valid.
            if (LicenseManager.CheckLicense())
            {
                // If license is valid, open the main application window.
                // Replace 'MainWindow' with the actual name of your main window if it's different.
                var mainWindow = new MainWindow();
                ShutdownMode = ShutdownMode.OnLastWindowClose;
                mainWindow.Show();
            }
            else
            {
                // If license is NOT valid, show the license activation window.
                var licWin = new LicenseWindow();
                bool? activated = licWin.ShowDialog(); // Show as a modal dialog

                if (activated == true)
                {
                    // If the user successfully activated the license, open the main window.
                    var mainWindow = new MainWindow();
                    ShutdownMode = ShutdownMode.OnLastWindowClose;
                    mainWindow.Show();
                }
                else
                {
                    // If the user canceled or closed the license window, shut down the app.
                    Application.Current.Shutdown();
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LicenseManager.FloatingRelease();
            base.OnExit(e);
        }
    }

}
