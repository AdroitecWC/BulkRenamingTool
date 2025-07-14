
using pfcls;
using RenaimingToolCS.CreoFunctions;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace RenaimingToolCS.ViewModels.Creo
{
    public static class CreoFileHelper
    {
        public static void PurgeFolder(string folderPath)
        {
            try
            {
                folderPath = folderPath.Replace(@"\\", @"\");
                var driveLetter = Path.GetPathRoot(folderPath)?.TrimEnd('\\');

                if (string.IsNullOrEmpty(driveLetter)) return;

                string cmdArguments = $"/C cd \"{folderPath}\" && {driveLetter} && purge && exit";

                Process.Start(new ProcessStartInfo("cmd.exe", cmdArguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                })?.WaitForExit();
            }
            catch
            {
                // Optional: log error
            }
        }

        public static void OpenAllDrawingsInFolder(string folderPath)
        {
            var session = CreoSessionManager.Instance.Session;
            var baseSession = (IpfcBaseSession)session;

            CCpfcModelDescriptor modelDescriptorCreator = new CCpfcModelDescriptor();
            IpfcRetrieveModelOptions retrieveOpts = (new CCpfcRetrieveModelOptions()).Create();

            var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                    .Where(f => f.EndsWith(".drw", StringComparison.OrdinalIgnoreCase) ||
                                                Path.GetFileName(f).ToLower().Contains(".drw."))
                                    .ToList();

            foreach (var file in allFiles)
            {
                try
                {
                    string drwName = Path.GetFileNameWithoutExtension(file);

                    // This assumes file is in the current working directory of Creo
                    IpfcModelDescriptor descriptor = modelDescriptorCreator.Create((int)
                        EpfcModelType.EpfcMDL_DRAWING, drwName, null);

                    baseSession.RetrieveModelWithOpts(descriptor, retrieveOpts);
                }
                catch (Exception ex)
                {
                    // Helpful debug output
                    System.Diagnostics.Debug.WriteLine($"Failed to open drawing: {file}\n{ex}");
                    //MessageBox.Show($"Failed to open drawing: {file}\n{ex.Message}", "Open Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }



    }
}

