using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace RenaimingToolCS.CreoFunctions
{
    class CreateAndRunBatchFile
    {
        // You need to provide these paths before calling CreateAndRunBatchFile()
        public string VbApiRegisterBatPath { get; set; }
        public string CreoPath { get; set; }
        public string ProDirectory { get; set; }
        public string ProCommMsgExe { get; set; }

        public void CreateAndRunBatchFileMethod()
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(VbApiRegisterBatPath) ||
                string.IsNullOrWhiteSpace(CreoPath) ||
                string.IsNullOrWhiteSpace(ProDirectory) ||
                string.IsNullOrWhiteSpace(ProCommMsgExe))
            {
                MessageBox.Show("All paths must be set before running batch file creation.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Generate batch content with paths injected
            string batchContent = $@"
@echo off
chcp 1252 >nul

echo ----------------------------------------
echo Registering Creo COM Automation (pfclscom)
echo ----------------------------------------

REM ================================================
REM ✅ Path to vb_api_register.bat (from settings)
call ""{VbApiRegisterBatPath}""

IF %ERRORLEVEL% NEQ 0 (
    echo Failed to register Creo COM server.
    pause
    exit /b 1
)

echo.
echo ----------------------------------------
echo Setting Environment Variables (System Scope)
echo ----------------------------------------

REM ✅ Environment variable values from settings
setx PROE_PATH ""{CreoPath}"" /M
setx PRO_DIRECTORY ""{ProDirectory}"" /M
setx PRO_COMM_MSG_EXE ""{ProCommMsgExe}"" /M

echo.
echo ----------------------------------------
echo ✅ Done. Please restart your computer or log off/on
echo for environment variables to take effect system-wide.
echo ----------------------------------------

pause
exit /b 0
";

            try
            {
                // Save batch file in temp folder (or any folder you want)
                string batchFilePath = Path.Combine(Path.GetTempPath(), "RegisterCreoCOM.bat");

                File.WriteAllText(batchFilePath, batchContent);

                // Run the batch file as admin
                RunBatchAsAdmin(batchFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to create or run batch file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunBatchAsAdmin(string batchFilePath)
        {
            if (!IsRunAsAdmin())
            {
                var proc = new ProcessStartInfo
                {
                    FileName = batchFilePath,
                    UseShellExecute = true,
                    Verb = "runas",  // triggers UAC prompt
                    WindowStyle = ProcessWindowStyle.Normal
                };

                try
                {
                    Process.Start(proc);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to run batch as admin: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Already running as admin, just run batch
                Process.Start(batchFilePath);
            }
        }

        private bool IsRunAsAdmin()
        {
            var wi = WindowsIdentity.GetCurrent();
            var wp = new WindowsPrincipal(wi);
            return wp.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
