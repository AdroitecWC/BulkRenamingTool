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

            var licErr = LicenseManager.CheckLicenseDetailed();

            switch (licErr)
            {
                case LicenseError.None:
                    // Valid — fall through to main window

                    // A floating activation that failed on its first attempt (server down,
                    // no seats yet, etc.) gets saved and silently retried on every launch --
                    // this is the first launch where it actually got a seat, so say so
                    // instead of jumping straight to the main window with no acknowledgement.
                    if (LicenseManager.IsActivationPending())
                    {
                        LicenseManager.ClearActivationPending();
                        MessageBox.Show(
                            "License activated successfully.",
                            "License Activated", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;

                case LicenseError.NoSeatsAvailable:
                    MessageBox.Show(
                        $"All {LicenseManager.LastSeatsInUse}/{LicenseManager.LastSeatsMax} license seats are currently in use." + Environment.NewLine +
                        "Please wait for another user to close the application and try again.",
                        "No License Seats Available",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (!ShouldOpenLicenseWindow(licErr)) { Application.Current.Shutdown(); return; }
                    if (!TryActivateViaLicenseWindow()) { Application.Current.Shutdown(); return; }
                    break;

                case LicenseError.ServerUnreachable:
                    MessageBox.Show(
                        "Cannot reach the license server." + Environment.NewLine +
                        "Please check your network connection and try again.",
                        "License Server Unavailable",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    if (!ShouldOpenLicenseWindow(licErr)) { Application.Current.Shutdown(); return; }
                    if (!TryActivateViaLicenseWindow()) { Application.Current.Shutdown(); return; }
                    break;

                case LicenseError.InvalidLicense:
                    MessageBox.Show(
                        "This license is not registered on the license server." + Environment.NewLine +
                        "Please contact your administrator.",
                        "Invalid License",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    if (!ShouldOpenLicenseWindow(licErr)) { Application.Current.Shutdown(); return; }
                    if (!TryActivateViaLicenseWindow()) { Application.Current.Shutdown(); return; }
                    break;

                case LicenseError.ProductNotLicensed:
                case LicenseError.AmbiguousCustomer:
                    // Activation Code itself was right but the server side isn't ready yet
                    // (no tranche loaded, or tranches span more than one customer) -- same
                    // "already configured but can't get a seat right now" situation as the
                    // cases above, so offer the same choice instead of always reopening the
                    // activation window with no explanation.
                    if (!ShouldOpenLicenseWindow(licErr)) { Application.Current.Shutdown(); return; }
                    if (!TryActivateViaLicenseWindow()) { Application.Current.Shutdown(); return; }
                    break;

                case LicenseError.Expired:
                    // No blocking dialog -- go straight to the activation window so
                    // loading a renewed license is one step, not an OK click first.
                    if (!TryActivateViaLicenseWindow()) { Application.Current.Shutdown(); return; }
                    break;

                case LicenseError.WrongProduct:
                    MessageBox.Show(
                        "This license was issued for a different Adroitec product." + Environment.NewLine +
                        "Please use the license generated for " + LicenseManager.AppProduct + "." + Environment.NewLine +
                        "Click OK to load the correct license.",
                        "Wrong Product",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    if (!TryActivateViaLicenseWindow()) { Application.Current.Shutdown(); return; }
                    break;

                default:
                    // Missing file, not activated, machine mismatch, tampered, transferred --
                    // show the activation window directly.
                    if (!TryActivateViaLicenseWindow()) { Application.Current.Shutdown(); return; }
                    break;
            }

            // License IS valid (either already, or just activated) — open the main window.
            var mainWindow = new MainWindow();
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            mainWindow.Show();
        }

        /// <summary>
        /// A floating license that's already configured but can't get a seat right
        /// now (server down, no seats free, code not yet provisioned) is a different
        /// situation from "never activated" -- ask before jumping straight into the
        /// full activation window, rather than auto-opening it (or silently exiting)
        /// with no explanation. Returns true if the caller should open LicenseWindow.
        /// </summary>
        private bool ShouldOpenLicenseWindow(LicenseError licErr)
        {
            bool isFloatingSeatError = licErr == LicenseError.ServerUnreachable ||
                                        licErr == LicenseError.NoSeatsAvailable ||
                                        licErr == LicenseError.InvalidLicense ||
                                        licErr == LicenseError.ProductNotLicensed ||
                                        licErr == LicenseError.AmbiguousCustomer;

            if (!(LicenseManager.IsFloatingConfigured() && isFloatingSeatError)) return true;

            var answer = MessageBox.Show(
                "Network License is not available." + Environment.NewLine + Environment.NewLine +
                "Do you want to change the license?",
                "Network License Unavailable", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return answer == MessageBoxResult.Yes;
        }

        private bool TryActivateViaLicenseWindow()
        {
            var lic = new LicenseWindow();
            return lic.ShowDialog() == true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LicenseManager.FloatingRelease();
            base.OnExit(e);
        }
    }

}
