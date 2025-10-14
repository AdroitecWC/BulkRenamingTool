
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
                                    .Where(f => f.EndsWith(".drw", StringComparison.OrdinalIgnoreCase) ||
                                                f.EndsWith(".prt", StringComparison.OrdinalIgnoreCase) ||
                                                f.EndsWith(".asm", StringComparison.OrdinalIgnoreCase) ||
                                                Path.GetFileName(f).ToLower().Contains(".drw.") ||
                                                Path.GetFileName(f).ToLower().Contains(".prt.") ||
                                                Path.GetFileName(f).ToLower().Contains(".asm."))
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

        public static void RunPurge(string[] args)
        {
            // Step 1: Detect machine type
            string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
            string archW6432 = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? "";
            string processor = Environment.GetEnvironmentVariable("PROCESSOR") ?? "";

            string mc = "unset";

            if (processor == "INTEL_64") mc = "ia64_nt";
            else if (processor == "INTEL_486") mc = "i486_nt";
            else if (arch == "IA64") mc = "ia64_nt";
            else if (arch == "AMD64") mc = "x86e_win64";
            else if (arch == "x86") mc = "i486_nt";
            else if (archW6432 == "AMD64") mc = "x86e_win64";

            if (mc == "unset")
            {
                Console.WriteLine("ERROR: Cannot detect machine type.");
                return;
            }

            // Step 2: Set paths
            string scriptPath = AppDomain.CurrentDomain.BaseDirectory;
            string appDir = Path.GetFullPath(Path.Combine(scriptPath, ".."));
            string cf = Path.GetFullPath(Path.Combine(scriptPath, "..", ".."));
            string proDir = Path.Combine(cf, "Common Files");

            string creoDirEnv = Environment.GetEnvironmentVariable("PRO_DIRECTORY");
            if (!string.IsNullOrEmpty(creoDirEnv))
            {
                proDir = Path.Combine(creoDirEnv, "Common Files");
                appDir = creoDirEnv;
            }

            string purgeExePath = Path.Combine(proDir, mc, "obj", "purge.exe");

            if (!File.Exists(purgeExePath))
            {
                Console.WriteLine($"ERROR: purge.exe not found at path: {purgeExePath}");
                return;
            }

            // Step 3: Build command-line arguments
            string arguments = string.Join(" ", args);

            // Step 4: Launch purge.exe
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = purgeExePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (Process proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                    Console.WriteLine($"purge.exe exited with code {proc.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to run purge.exe: {ex.Message}");
            }
        }

    }
}

