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
        //    protected override void OnStartup(StartupEventArgs e)
        //    {
        //        base.OnStartup(e);

        //        // GUI Mode
        //        if (!LicenseManager.CheckLicense())
        //        {
        //            var licWin = new LicenseWindow();
        //            bool? activated = licWin.ShowDialog();
        //            if (activated != true)
        //            {
        //                // User canceled activation, exit app
        //                Environment.Exit(0);
        //            }
        //        }

        //        var intro = new LicenseWindow();
        //        intro.Show();
        //    }
    }

}
