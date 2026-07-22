using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Timers;
using Microsoft.Win32;

namespace RenaimingToolCS.ViewModels
{
    public enum LicenseError
    {
        None,               // License is valid
        LicenseFileMissing, // .lic file not found
        NotActivated,       // Activated flag is False
        Expired,            // LastDate is in the past
        LicenseTransferred, // License was transferred away from this machine
        MachineMismatch,    // LicenseId does not match this hardware
        LicenseTampered,    // Decryption failed / file corrupt
        CryptoError,        // Unexpected crypto exception
        NoSeatsAvailable,   // Floating: all seats checked out
        ServerUnreachable,  // Floating: cannot reach license server
        InvalidLicense,     // Floating: uid not registered on server
        WrongProduct        // License is for a different product
    }

    public static class LicenseManager
    {
        private const string LicfilePwd = "Kbe@Adr";
        private const string AppProduct = "Bulk Rename";
        private const string UsedLicensesRegPath = @"Software\Adroitec Engineering Solutions Pvt Ltd\MyTool\UsedLicenses";
        private const string SettingsRegPath = @"Software\Adroitec Engineering Solutions Pvt Ltd\MyTool\Settings";

        // ── Floating license runtime state ───────────────────────────────────────
        private static string _seatToken = "";
        private static string _serverUrl = "";
        private static System.Timers.Timer? _heartbeatTimer;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };

        // Filled after a NoSeatsAvailable response so the caller can show specifics
        public static int LastSeatsInUse = 0;
        public static int LastSeatsMax = 0;

        // =====================================================
        // PUBLIC API
        // =====================================================

        /// <summary>True if the license is valid and (for floating) a seat was obtained.</summary>
        public static bool CheckLicense() => CheckLicenseDetailed() == LicenseError.None;

        /// <summary>Returns the specific reason for failure, or None on success.</summary>
        public static LicenseError CheckLicenseDetailed()
        {
            var fullPath = GetLicensePath();
            if (!File.Exists(fullPath)) return LicenseError.LicenseFileMissing;

            Dictionary<string, string> data;
            try
            {
                data = ReadLicense(DecryptFile(fullPath));
            }
            catch
            {
                return LicenseError.CryptoError;
            }

            try
            {
                if (!data.TryGetValue("Activated", out var activated) || activated != "True")
                    return LicenseError.NotActivated;

                if (data.TryGetValue("LastDate", out var lastDate) && lastDate != "Nil")
                {
                    if (DateTime.TryParse(lastDate, out var exp) && exp < DateTime.Today)
                        return LicenseError.Expired;
                }

                // ── FLOATING LICENSE ─────────────────────────────────────────────
                if (data.TryGetValue("LicenseType", out var licType) && licType == "Floating")
                {
                    var licProduct = data.GetValueOrDefault("Product", "");
                    if (!licProduct.StartsWith(AppProduct, StringComparison.OrdinalIgnoreCase))
                        return LicenseError.WrongProduct;

                    var uid = data.GetValueOrDefault("LicenseUid", "");
                    // A manually entered + verified address (Network License Server field) takes
                    // precedence over whatever ServerUrl is baked into the .lic file, since that's
                    // exactly the point of letting an admin repoint a client without reissuing a license.
                    _serverUrl = GetServerUrlOverride() ?? data.GetValueOrDefault("ServerUrl", "").TrimEnd('/');
                    if (uid == "" || _serverUrl == "") return LicenseError.LicenseTampered;

                    return FloatingCheckoutDetailed(uid, GenerateLicenseCode());
                }

                // ── NODE-LOCKED LICENSE ──────────────────────────────────────────
                var currentSystemCode = GenerateLicenseCode();

                if (data.TryGetValue("LicenseUid", out var licUid) && IsLicenseUsed("TRANSFERRED_LIC_" + licUid))
                    return LicenseError.LicenseTransferred;

                if (!data.TryGetValue("LicenseId", out var licenseId) || licenseId != currentSystemCode)
                    return LicenseError.MachineMismatch;

                // Character-by-character binding
                for (var i = 0; i < currentSystemCode.Length; i++)
                {
                    var key = $"C{i}";
                    if (!data.TryGetValue(key, out var stored) ||
                        !int.TryParse(stored, out var storedVal) ||
                        storedVal != currentSystemCode[i])
                    {
                        return LicenseError.MachineMismatch;
                    }
                }

                return LicenseError.None;
            }
            catch
            {
                return LicenseError.LicenseTampered;
            }
        }

        // =====================================================
        // ACTIVATE LICENSE (FIRST TIME — node-locked only)
        // =====================================================
        public static bool ActivateLicense()
        {
            var fullPath = GetLicensePath();
            if (!File.Exists(fullPath)) return false;

            try
            {
                var data = ReadLicense(DecryptFile(fullPath));

                // Floating licenses are pre-activated by the server
                if (data.TryGetValue("LicenseType", out var licType) && licType == "Floating")
                    return true;

                var currentSystemCode = GenerateLicenseCode();

                if (data.TryGetValue("LicenseUid", out var licUid) && IsLicenseUsed("TRANSFERRED_LIC_" + licUid))
                    return false;

                if (!data.TryGetValue("LicenseId", out var licenseId) || licenseId != currentSystemCode)
                    return false;

                data["Activated"] = "True";
                data["Transferred"] = "False";
                EncryptToFile(SerializeLicense(data), fullPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // =====================================================
        // DEACTIVATE LICENSE
        // =====================================================
        public static bool DeactivateLicense()
        {
            var fullPath = GetLicensePath();
            if (!File.Exists(fullPath)) return false;

            try
            {
                var data = ReadLicense(DecryptFile(fullPath));

                if (data.TryGetValue("LicenseType", out var licType) && licType == "Floating")
                    FloatingRelease();

                data["Activated"] = "False";
                EncryptToFile(SerializeLicense(data), fullPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // =====================================================
        // FLOATING — CHECKOUT / RELEASE / HEARTBEAT
        // =====================================================
        private static LicenseError FloatingCheckoutDetailed(string uid, string machineCode)
        {
            try
            {
                var machineName = Environment.MachineName;
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["uid"] = uid,
                    ["machine"] = machineCode,
                    ["machine_name"] = machineName
                });

                string json;
                try
                {
                    var resp = _http.PostAsync($"{_serverUrl}/checkout.php", body).GetAwaiter().GetResult();
                    json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    return LicenseError.ServerUnreachable;
                }

                // If the response isn't JSON (e.g. HTML 404 from a wrong port/URL), treat as unreachable
                if (!json.TrimStart().StartsWith("{"))
                    return LicenseError.ServerUnreachable;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var success = root.TryGetProperty("success", out var successEl) &&
                              successEl.ValueKind == JsonValueKind.True;

                if (success && root.TryGetProperty("seat_token", out var tokenEl))
                {
                    _seatToken = tokenEl.GetString() ?? "";
                    LastSeatsInUse = root.TryGetProperty("in_use", out var u1) ? u1.GetInt32() : 0;
                    LastSeatsMax = root.TryGetProperty("max_seats", out var m1) ? m1.GetInt32() : 0;
                    StartHeartbeat();
                    return LicenseError.None;
                }

                LastSeatsInUse = root.TryGetProperty("in_use", out var u2) ? u2.GetInt32() : 0;
                LastSeatsMax = root.TryGetProperty("max_seats", out var m2) ? m2.GetInt32() : 0;

                var reason = root.TryGetProperty("reason", out var reasonEl) ? reasonEl.GetString() : "";
                if (reason == "no_seats") return LicenseError.NoSeatsAvailable;
                if (reason == "invalid_license") return LicenseError.InvalidLicense;
                return LicenseError.NoSeatsAvailable;
            }
            catch
            {
                return LicenseError.ServerUnreachable;
            }
        }

        public static void FloatingRelease()
        {
            if (_seatToken == "" || _serverUrl == "") return;
            StopHeartbeat();
            try
            {
                var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["seat_token"] = _seatToken });
                _http.PostAsync($"{_serverUrl}/release.php", body).GetAwaiter().GetResult();
            }
            catch
            {
            }
            finally
            {
                _seatToken = "";
            }
        }

        private static void StartHeartbeat()
        {
            StopHeartbeat();
            _heartbeatTimer = new System.Timers.Timer(180000) { AutoReset = true }; // every 3 minutes
            _heartbeatTimer.Elapsed += OnHeartbeat;
            _heartbeatTimer.Start();
        }

        private static void OnHeartbeat(object? sender, ElapsedEventArgs e)
        {
            if (_seatToken == "" || _serverUrl == "") return;
            try
            {
                var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["seat_token"] = _seatToken });
                _http.PostAsync($"{_serverUrl}/heartbeat.php", body).GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        private static void StopHeartbeat()
        {
            if (_heartbeatTimer is null) return;
            _heartbeatTimer.Stop();
            _heartbeatTimer.Dispose();
            _heartbeatTimer = null;
        }

        public static bool IsFloatingLicense()
        {
            var path = GetLicensePath();
            if (!File.Exists(path)) return false;
            try
            {
                var data = ReadLicense(DecryptFile(path));
                return data.TryGetValue("LicenseType", out var t) && t == "Floating";
            }
            catch
            {
                return false;
            }
        }

        // =====================================================
        // MACHINE CODE
        // =====================================================
        public static string GetLicensePath()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "License", "License.lic");
            var dir = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return path;
        }

        // Salts the fingerprint so this tool's code never coincides with another
        // Adroitec tool's code on the same machine, even though the underlying
        // hardware fields are identical. Change per tool.
        private const string ToolName = "BulkRenameToolEdition";
        private const string SerialAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public static string GenerateLicenseCode()
        {
            // Same algorithm as SpreadSheetBasedAutomation's LicenseManager.vb:
            // hash the raw WMI fingerprint (+ ComputerSystemProduct.UUID, included
            // because the other four fields are model/firmware strings that repeat
            // across identical hardware batches) and encode it into a readable
            // dashed serial, rather than exposing the raw WMI values directly.
            var raw =
                ToolName + "|" +
                GetWMI("Win32_Processor", "ProcessorId") +
                GetWMI("Win32_BaseBoard", "Product") +
                GetWMI("Win32_DiskDrive", "Signature") +
                GetWMI("Win32_BIOS", "Version") +
                GetWMI("Win32_ComputerSystemProduct", "UUID");

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            var serial = EncodeToSerial(hashBytes, 20); // 20-char code

            return string.Join("-", SplitInChunks(serial, 5));
        }

        private static string EncodeToSerial(byte[] hash, int length)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < length; i++)
            {
                var b = hash[i % hash.Length];
                var idx = (b + i * 7) % SerialAlphabet.Length;
                sb.Append(SerialAlphabet[idx]);
            }
            return sb.ToString();
        }

        private static IEnumerable<string> SplitInChunks(string s, int chunkSize)
        {
            for (var i = 0; i < s.Length; i += chunkSize)
                yield return s.Substring(i, Math.Min(chunkSize, s.Length - i));
        }

        private static string GetWMI(string className, string propertyName)
        {
            try
            {
                var searcher = new ManagementObjectSearcher($"SELECT * FROM {className}");
                foreach (var obj in searcher.Get())
                {
                    var value = obj[propertyName];
                    if (value != null) return value.ToString() ?? "";
                }
            }
            catch
            {
                // Ignore WMI errors
            }

            return "";
        }

        // =====================================================
        // FILE IO
        // =====================================================
        private static Dictionary<string, string> ReadLicense(byte[] plainBytes)
        {
            var dict = new Dictionary<string, string>();
            var text = Encoding.UTF8.GetString(plainBytes);
            foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var idx = line.IndexOf(':');
                if (idx > 0) dict[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }
            return dict;
        }

        private static string SerializeLicense(Dictionary<string, string> data)
        {
            var sb = new StringBuilder();
            foreach (var kv in data) sb.Append($"{kv.Key}:{kv.Value}\n");
            return sb.ToString();
        }

        // =====================================================
        // NETWORK LICENSE SERVER — manual address entry + connectivity test
        // =====================================================

        /// <summary>
        /// Parses a "host", "host:port", or "host@port" address (matching the Server
        /// Name shown on the FloatingLicenseServer app), probes its checkout endpoint,
        /// and — only if a server actually answers — saves it so future floating
        /// checkouts use this address instead of the one baked into the .lic file.
        /// </summary>
        public static bool TestAndSaveServerUrl(string rawAddress, out string message)
        {
            var url = BuildServerUrl(rawAddress);
            if (url == null)
            {
                message = "Enter a server address, e.g. vizserver:1122 or vizserver@1122.";
                return false;
            }

            try
            {
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["uid"] = "__connectivity_test__",
                    ["machine"] = "connectivity-test",
                    ["machine_name"] = "connectivity-test"
                });
                var resp = _http.PostAsync($"{url}/checkout.php", body).GetAwaiter().GetResult();
                var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!json.TrimStart().StartsWith("{"))
                {
                    message = $"No license server responded at {url}.";
                    return false;
                }
            }
            catch
            {
                message = $"Could not reach a license server at {url}.\nCheck the address and try again.";
                return false;
            }

            SaveServerUrlOverride(url);
            message = $"Connected to the license server at {url} successfully.";
            return true;
        }

        private static string? BuildServerUrl(string rawAddress)
        {
            var addr = (rawAddress ?? "").Trim();
            if (addr == "") return null;

            var sep = addr.Contains('@') ? '@' : (addr.Contains(':') ? ':' : '\0');
            string host;
            var port = 80;
            if (sep != '\0')
            {
                var parts = addr.Split(sep, 2);
                host = parts[0].Trim();
                if (!int.TryParse(parts[1].Trim(), out port)) port = 80;
            }
            else
            {
                host = addr;
            }
            if (host == "") return null;

            return port == 80 ? $"http://{host}/web/api/floating" : $"http://{host}:{port}/web/api/floating";
        }

        private static void SaveServerUrlOverride(string url)
        {
            using var k = Registry.CurrentUser.CreateSubKey(SettingsRegPath);
            k?.SetValue("FloatingServerOverride", url);
        }

        private static string? GetServerUrlOverride()
        {
            using var k = Registry.CurrentUser.OpenSubKey(SettingsRegPath);
            var v = k?.GetValue("FloatingServerOverride") as string;
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        // =====================================================
        // REGISTRY
        // =====================================================
        private static bool IsLicenseUsed(string id)
        {
            using var k = Registry.CurrentUser.OpenSubKey(UsedLicensesRegPath);
            return k?.GetValue(id) != null;
        }

        // =====================================================
        // CRYPTO
        // =====================================================
        private static byte[] DecryptFile(string path)
        {
            var (key, iv) = LicKeyIv();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var ms = new MemoryStream();
            using (var fs = File.OpenRead(path))
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                fs.CopyTo(cs);
            }
            return ms.ToArray();
        }

        private static void EncryptToFile(string plainText, string path)
        {
            var (key, iv) = LicKeyIv();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var fsOut = File.Create(path);
            using var cs = new CryptoStream(fsOut, aes.CreateEncryptor(), CryptoStreamMode.Write);
            var bytes = Encoding.UTF8.GetBytes(plainText);
            cs.Write(bytes, 0, bytes.Length);
        }

        private static (byte[] Key, byte[] IV) LicKeyIv()
        {
            var hash = SHA512.HashData(Encoding.ASCII.GetBytes(LicfilePwd)); // matches PHP sha512() on the same ASCII password
            return (hash.Take(32).ToArray(), hash.Skip(32).Take(16).ToArray());
        }
    }
}
