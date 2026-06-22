using Microsoft.Win32;
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
        public enum LicenseError
        {
            None,
            LicenseFileMissing,
            NotActivated,
            Expired,
            LicenseTransferred,
            MachineMismatch,
            LicenseTampered,
            CryptoError
        }

        private const string LicfilePwd = "Kbe@Adr";
        private const string UsedLicensesRegPath = @"Software\MyCompany\MyTool\UsedLicenses";

        // =====================================================
        // LICENSE STORAGE LOCATION
        // =====================================================
        public static string GetLicensePath()
        {
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(basePath, "MyCompany", "RenamingTool", "License");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return Path.Combine(folder, "License.lic");
        }

        // =====================================================
        // CHECK LICENSE
        // =====================================================
        public static bool CheckLicense()
        {
            string fullPath = GetLicensePath();
            if (!File.Exists(fullPath)) return false;

            string tempPath = Path.GetTempFileName();

            try
            {
                Decrypt(fullPath, tempPath);
                var data = ReadLicense(tempPath);

                if (!data.ContainsKey("Activated") || data["Activated"] != "True")
                    return false;

                if (data.ContainsKey("LastDate") && data["LastDate"] != "Nil")
                {
                    if (DateTime.TryParse(data["LastDate"], out DateTime exp) &&
                        exp < DateTime.Today)
                        return false;
                }

                string currentSystemCode = GenerateLicenseCode();

                if (data.ContainsKey("LicenseUid") &&
                    IsLicenseUsed("TRANSFERRED_LIC_" + data["LicenseUid"]))
                    return false;

                if (!data.ContainsKey("LicenseId") || data["LicenseId"] != currentSystemCode)
                    return false;

                for (int i = 0; i < currentSystemCode.Length; i++)
                {
                    string key = $"C{i}";
                    if (!data.ContainsKey(key) ||
                        (int)currentSystemCode[i] != int.Parse(data[key]))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        // =====================================================
        // ACTIVATE LICENSE
        // =====================================================
        public static bool ActivateLicense()
        {
            string fullPath = GetLicensePath();
            if (!File.Exists(fullPath)) return false;

            string tempPath = Path.GetTempFileName();

            try
            {
                Decrypt(fullPath, tempPath);
                var data = ReadLicense(tempPath);

                string currentSystemCode = GenerateLicenseCode();

                if (data.ContainsKey("LicenseUid") &&
                    IsLicenseUsed("TRANSFERRED_LIC_" + data["LicenseUid"]))
                    return false;

                if (!data.ContainsKey("LicenseId") || data["LicenseId"] != currentSystemCode)
                    return false;

                data["Activated"] = "True";
                data["Transferred"] = "False";

                WriteLicense(tempPath, data);
                Encrypt(tempPath, fullPath);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        // =====================================================
        // DEACTIVATE LICENSE
        // =====================================================
        public static bool DeactivateLicense()
        {
            string fullPath = GetLicensePath();
            if (!File.Exists(fullPath)) return false;

            string tempPath = Path.GetTempFileName();

            try
            {
                Decrypt(fullPath, tempPath);
                var data = ReadLicense(tempPath);

                data["Activated"] = "False";

                WriteLicense(tempPath, data);
                Encrypt(tempPath, fullPath);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        // =====================================================
        // TRANSFER LICENSE
        // =====================================================
        public static bool TransferLicense(string newSystemCode, string savePath)
        {
            string fullPath = GetLicensePath();
            if (!File.Exists(fullPath)) return false;

            string tempPath = Path.GetTempFileName();

            try
            {
                Decrypt(fullPath, tempPath);
                var data = ReadLicense(tempPath);

                if (data.ContainsKey("Transferred") && data["Transferred"] == "True")
                    return false;

                data["Activated"] = "False";
                data["Transferred"] = "True";

                WriteLicense(tempPath, data);
                Encrypt(tempPath, fullPath);

                MarkLicenseUsed("TRANSFERRED_LIC_" + data["LicenseUid"]);

                var newData = new Dictionary<string, string>(data);

                newData["LicenseId"] = newSystemCode;
                newData["Activated"] = "True";
                newData["Transferred"] = "False";

                foreach (var key in newData.Keys.Where(k => k.StartsWith("C")).ToList())
                {
                    newData.Remove(key);
                }

                for (int i = 0; i < newSystemCode.Length; i++)
                {
                    newData[$"C{i}"] = ((int)newSystemCode[i]).ToString();
                }

                WriteLicense(tempPath, newData);
                Encrypt(tempPath, savePath);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        // =====================================================
        // FILE IO
        // =====================================================
        private static Dictionary<string, string> ReadLicense(string path)
        {
            var dict = new Dictionary<string, string>();

            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Contains(":"))
                {
                    var p = line.Split(new[] { ':' }, 2);
                    dict[p[0].Trim()] = p[1].Trim();
                }
            }

            return dict;
        }

        private static void WriteLicense(string path, Dictionary<string, string> data)
        {
            using (var w = new StreamWriter(path, false))
            {
                foreach (var kv in data)
                {
                    w.WriteLine($"{kv.Key}:{kv.Value}");
                }
            }
        }

        // =====================================================
        // REGISTRY
        // =====================================================
        private static bool IsLicenseUsed(string id)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(UsedLicensesRegPath))
            {
                return k != null && k.GetValue(id) != null;
            }
        }

        private static void MarkLicenseUsed(string id)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(UsedLicensesRegPath))
            {
                k.SetValue(id, DateTime.Now.ToString("s"));
            }
        }

        // =====================================================
        // MACHINE CODE
        // =====================================================
        public static string GenerateLicenseCode()
        {
            string raw =
                GetWMI("Win32_Processor", "ProcessorId") +
                GetWMI("Win32_BaseBoard", "Product") +
                GetWMI("Win32_DiskDrive", "Signature") +
                GetWMI("Win32_BIOS", "Version");

            return new string(raw.Where(char.IsLetterOrDigit).ToArray());
        }

        private static string GetWMI(string cls, string prop)
        {
            try
            {
                foreach (ManagementObject o in new ManagementObjectSearcher($"SELECT * FROM {cls}").Get())
                {
                    if (o[prop] != null)
                        return o[prop].ToString();
                }
            }
            catch { }

            return "";
        }

        // =====================================================
        // CRYPTO
        // =====================================================
        private static void Encrypt(string input, string output)
        {
            Crypto(input, output, true);
        }

        private static void Decrypt(string input, string output)
        {
            Crypto(input, output, false);
        }

        private static void Crypto(string input, string output, bool encrypt)
        {
            byte[] key = SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(LicfilePwd));

            using (var aes = new RijndaelManaged())
            {
                aes.Key = key.Take(32).ToArray();
                aes.IV = key.Skip(32).Take(16).ToArray();

                using (var fsIn = new FileStream(input, FileMode.Open))
                using (var fsOut = new FileStream(output, FileMode.Create))
                using (var cs = new CryptoStream(
                    fsOut,
                    encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor(),
                    CryptoStreamMode.Write))
                {
                    fsIn.CopyTo(cs);
                }
            }
        }
    }
}
