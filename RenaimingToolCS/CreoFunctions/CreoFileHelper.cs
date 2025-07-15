
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

        public static void OpenAllCreoModelsInFolder(string folderPath)
        {
            var session = CreoSessionManager.Instance.Session;
            var baseSession = (IpfcBaseSession)session;

            CCpfcModelDescriptor modelDescriptorCreator = new CCpfcModelDescriptor();
            IpfcRetrieveModelOptions retrieveOpts = (new CCpfcRetrieveModelOptions()).Create();

            var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                    .Where(f => f.EndsWith(".prt", StringComparison.OrdinalIgnoreCase) ||
                                                f.EndsWith(".asm", StringComparison.OrdinalIgnoreCase) ||
                                                f.EndsWith(".drw", StringComparison.OrdinalIgnoreCase) ||
                                                Path.GetFileName(f).ToLower().Contains(".prt.") ||
                                                Path.GetFileName(f).ToLower().Contains(".asm.") ||
                                                Path.GetFileName(f).ToLower().Contains(".drw."))
                                    .ToList();

            foreach (var file in allFiles)
            {
                try
                {
                    string modelName = Path.GetFileNameWithoutExtension(file);
                    EpfcModelType modelType = GetCreoModelType(file);

                    if ((int)modelType < 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping unknown model type: {file}");
                        continue;
                    }

                    IpfcModelDescriptor descriptor = modelDescriptorCreator.Create((int)modelType, modelName, null);
                    baseSession.RetrieveModelWithOpts(descriptor, retrieveOpts);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to open model: {file}\n{ex}");
                }
            }
        }
        private static EpfcModelType GetCreoModelType(string filePath)
        {
            if (filePath.EndsWith(".prt", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(filePath).ToLower().Contains(".prt."))
                return EpfcModelType.EpfcMDL_PART;

            if (filePath.EndsWith(".asm", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(filePath).ToLower().Contains(".asm."))
                return EpfcModelType.EpfcMDL_ASSEMBLY;

            if (filePath.EndsWith(".drw", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(filePath).ToLower().Contains(".drw."))
                return EpfcModelType.EpfcMDL_DRAWING;

            // Default fallback
            return (EpfcModelType)(-1); // or EpfcMDL_PART as a default if necessary
        }


    }
}

