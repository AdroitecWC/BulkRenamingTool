using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace RenaimingToolCS.ViewModels
{
    public static class LicenseManager
    {
        private const string LicfilePwd = "Kbe@Adr";
        private const string LicenseFilePath = @"Resources\License\License.lic";
        private const string RegPath = @"Software\MyCompany\MyTool";

        public static bool CheckLicense()
        {
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFilePath);
            var tempPath = Path.Combine(Path.GetTempPath(), "TempLicCheck.txt");

            if (!File.Exists(fullPath))
                return false;

            // Decrypt License.lic
            var key = CreateKey(LicfilePwd);
            var iv = CreateIV(LicfilePwd);
            try
            {
                EncryptOrDecryptFile(fullPath, tempPath, key, iv, CryptoAction.ActionDecrypt);
            }
            catch
            {
                return false;
            }

            // Read decrypted license
            try
            {
                using (var reader = new StreamReader(tempPath))
                {
                    var expiry = reader.ReadLine()?.Replace("LastDate:", "").Trim();
                    if (!string.Equals(expiry, "Nil", StringComparison.OrdinalIgnoreCase) &&
                        DateTime.TryParse(expiry, out var expiryDate))
                    {
                        if (expiryDate < DateTime.Today)
                            return false;
                    }

                    // Validate request code
                    var expectedCode = GenerateLicenseCode();
                    foreach (char ch in expectedCode)
                    {
                        if (reader.EndOfStream)
                            return false;

                        var val = reader.ReadLine();
                        if (!int.TryParse(val, out int intVal) || intVal != (int)ch)
                            return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateLicenseCode()
        {
            return GetSystemInfo();
        }

        private static string GetSystemInfo()
        {
            var info = new StringBuilder();
            info.Append(GetWMI("Win32_Processor", "ProcessorId"));
            info.Append(GetWMI("Win32_BaseBoard", "Product"));
            info.Append(GetWMI("Win32_DiskDrive", "Signature"));
            info.Append(GetWMI("Win32_BIOS", "Version"));

            return new string(info.ToString().Where(char.IsLetterOrDigit).ToArray());
        }

        private static string GetWMI(string className, string propertyName)
        {
            try
            {
                var searcher = new ManagementObjectSearcher($"SELECT * FROM {className}");
                foreach (var obj in searcher.Get())
                {
                    var value = obj[propertyName];
                    if (value != null)
                        return value.ToString();
                }
            }
            catch
            {
                // Ignore WMI errors
            }

            return "";
        }

        public enum CryptoAction
        {
            ActionEncrypt = 1,
            ActionDecrypt = 2
        }

        public static byte[] CreateKey(string password)
        {
            using var sha = new SHA512Managed();
            var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(password));
            return hash.Take(32).ToArray(); // AES-256
        }

        public static byte[] CreateIV(string password)
        {
            using var sha = new SHA512Managed();
            var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(password));
            return hash.Skip(32).Take(16).ToArray(); // AES block size = 16 bytes
        }

        public static void EncryptOrDecryptFile(string inputPath, string outputPath, byte[] key, byte[] iv, CryptoAction direction)
        {
            using var fsInput = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            using var fsOutput = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var csp = new RijndaelManaged();

            CryptoStream cs;
            if (direction == CryptoAction.ActionEncrypt)
                cs = new CryptoStream(fsOutput, csp.CreateEncryptor(key, iv), CryptoStreamMode.Write);
            else
                cs = new CryptoStream(fsOutput, csp.CreateDecryptor(key, iv), CryptoStreamMode.Write);

            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = fsInput.Read(buffer, 0, buffer.Length)) > 0)
            {
                cs.Write(buffer, 0, bytesRead);
            }

            cs.Close();
        }
    }
}
