using System;
using System.Collections.Generic;
using System.Globalization;
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
        InvalidLicense,     // Floating: uid/code not registered on server
        WrongProduct,       // License is for a different product
        ProductNotLicensed, // Floating: Activation Code is valid, but no tranche is loaded for this product yet
        AmbiguousCustomer   // Floating: Activation Code matched tranches for more than one customer
    }

    public static class LicenseManager
    {
        private const string LicfilePwd = "Kbe@Adr";
        public const string AppProduct = "Bulk Rename";
        // Scoped by ToolName -- these paths used to be the literal, identical string across
        // every Adroitec tool ("...\MyTool\..."), so activating one tool with a License Key
        // would overwrite the same registry value another tool reads back, making it look
        // like "every tool accepts the same key". Each tool now gets its own subkey.
        private const string UsedLicensesRegPath = @"Software\Adroitec Engineering Solutions Pvt Ltd\BulkRenameToolEdition\UsedLicenses";
        private const string SettingsRegPath = @"Software\Adroitec Engineering Solutions Pvt Ltd\BulkRenameToolEdition\Settings";

        // ── Floating license runtime state ───────────────────────────────────────
        private static string _seatToken = "";
        private static string _serverUrl = "";
        private static System.Timers.Timer? _heartbeatTimer;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };

        // Filled after a NoSeatsAvailable response so the caller can show specifics
        public static int LastSeatsInUse = 0;
        public static int LastSeatsMax = 0;

        /// <summary>The server URL the last checkout attempt actually used -- lets a
        /// "server not available" dialog show what was tried instead of leaving the user
        /// to guess whether the address they typed even parsed correctly.</summary>
        public static string LastServerUrl => _serverUrl;

        // Filled by ActivateWithKey so the caller can tell "server unreachable"
        // apart from other failures without parsing the message string.
        public static LicenseError LastActivationError = LicenseError.None;

        // Same idea as LastActivationError, but for BorrowSeat.
        public static LicenseError LastBorrowError = LicenseError.None;

        // =====================================================
        // PUBLIC API
        // =====================================================

        /// <summary>True if the license is valid and (for floating) a seat was obtained.</summary>
        public static bool CheckLicense() => CheckLicenseDetailed() == LicenseError.None;

        /// <summary>Returns the specific reason for failure, or None on success.</summary>
        public static LicenseError CheckLicenseDetailed()
        {
            // A live borrow is checked first and needs no network call at all -- this is
            // what lets the app run fully offline for the borrow window. See TryGetActiveBorrow.
            if (TryGetActiveBorrow(out _)) return LicenseError.None;

            // A key-based activation (License Key field, no .lic file at all) takes
            // priority over a local file every time — once someone's activated with
            // a key, that's the intended source of truth for this install.
            var activatedUid = GetActivatedUid();
            if (activatedUid != null)
            {
                _serverUrl = GetServerUrlOverride() ?? "";
                if (_serverUrl == "") return LicenseError.LicenseTampered;
                return FloatingCheckoutDetailed(activatedUid, GenerateLicenseCode(), isActivationCode: true);
            }

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

                // ── FLOATING LICENSE ─────────────────────────────────────────────
                // Checked before the local LastDate gate below on purpose: a floating
                // tranche's expiry lives on the NLM's live pool and can be renewed there
                // without this client ever re-downloading a new .lic, so the date baked
                // into whichever file happens to be loaded is not authoritative -- let
                // FloatingCheckoutDetailed's server round-trip decide instead (it maps the
                // server's own "license_expired" reason to LicenseError.Expired).
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

                if (data.TryGetValue("LastDate", out var lastDate) && lastDate != "Nil")
                {
                    if (DateTime.TryParse(lastDate, out var exp) && exp < DateTime.Today)
                        return LicenseError.Expired;
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
            // Activation-Code-based floating activation has no .lic file at all -- release
            // the checked-out seat and forget the saved code so the next launch requires
            // reactivation, instead of falling through to the file-based path below and
            // reporting "no active license found" for an install that IS actually active.
            var activatedUid = GetActivatedUid();
            var didSomething = false;
            if (activatedUid != null)
            {
                FloatingRelease();
                ClearActivatedUid();
                didSomething = true;
            }

            // Always also clear any file-based activation, even when an Activation Code was
            // the live path above -- a stale .lic file left next to the exe from an earlier
            // attempt (still marked Activated=True) must not resurface as a bogus license on
            // the next launch once the code is cleared and CheckLicenseDetailed() falls
            // through to the file-based path. Only release a floating seat for it if the
            // file path was actually the one in use (activatedUid was null) -- otherwise
            // that seat was already released above.
            var fullPath = GetLicensePath();
            if (File.Exists(fullPath))
            {
                try
                {
                    var data = ReadLicense(DecryptFile(fullPath));

                    if (activatedUid == null && data.TryGetValue("LicenseType", out var licType) && licType == "Floating")
                        FloatingRelease();

                    data["Activated"] = "False";
                    EncryptToFile(SerializeLicense(data), fullPath);
                    didSomething = true;
                }
                catch
                {
                    // Corrupt/unreadable file -- leave didSomething as-is from the code branch.
                }
            }

            return didSomething;
        }

        // =====================================================
        // CHANGE LICENSE TYPE (switch node-locked <-> floating)
        // =====================================================

        /// <summary>Best-effort cleanup of whatever activation currently exists (borrow,
        /// activation-code floating, or file-based license), so a "Change License Type"
        /// action always leaves this install clean and ready for LicenseWindow to activate
        /// a different type on the next launch. Unlike DeactivateLicense(), this succeeds
        /// even if nothing was active yet -- the point is guaranteed clean state, not
        /// reporting whether there was anything to clean.</summary>
        public static bool PrepareForLicenseTypeSwitch()
        {
            if (TryGetActiveBorrow(out _))
            {
                ReturnBorrowedSeat(out _);
            }

            var activatedUid = GetActivatedUid();
            if (activatedUid != null)
            {
                FloatingRelease();
                ClearActivatedUid();
            }

            // Always also clear any file-based activation, even when an Activation Code was
            // the live path above -- a stale .lic file left next to the exe from an earlier
            // attempt (still marked Activated=True, possibly with an old/expired LastDate)
            // must not resurface once the code is cleared and CheckLicenseDetailed() falls
            // through to the file-based path on the next launch. Only release a floating
            // seat for it if the file path was actually the one in use (activatedUid was
            // null) -- otherwise that seat was already released above.
            var fullPath = GetLicensePath();
            if (File.Exists(fullPath))
            {
                try
                {
                    var data = ReadLicense(DecryptFile(fullPath));

                    if (activatedUid == null && data.TryGetValue("LicenseType", out var licType) && licType == "Floating")
                        FloatingRelease();

                    data["Activated"] = "False";
                    EncryptToFile(SerializeLicense(data), fullPath);
                }
                catch
                {
                    // A corrupt/unreadable file shouldn't block switching -- LicenseWindow
                    // will just treat it as absent/invalid on the next launch.
                }
            }

            // Deliberately NOT clearing FloatingServerOverride here -- it's just the last
            // connection endpoint, not tied to which license type is active, and wiping it
            // forced a blind retype of the server IP:port on every Change License (the
            // Network License Server field otherwise starts blank) where any typo/blank
            // entry then looked exactly like the server being unreachable. LicenseWindow
            // now pre-fills from it via GetLastServerAddress(); the user can still type a
            // different address to point at a different server.
            return true;
        }

        // =====================================================
        // TRANSFER LICENSE (node-locked only, one-time)
        // =====================================================
        public static bool TransferLicense(string newSystemCode, string savePath)
        {
            var fullPath = GetLicensePath();
            if (!File.Exists(fullPath)) return false;

            try
            {
                var data = ReadLicense(DecryptFile(fullPath));

                // Floating licenses cannot be machine-transferred
                if (data.TryGetValue("LicenseType", out var licType) && licType == "Floating")
                    return false;

                if (data.TryGetValue("Transferred", out var transferred) && transferred == "True")
                    return false;

                data["Activated"] = "False";
                data["Transferred"] = "True";
                EncryptToFile(SerializeLicense(data), fullPath);
                MarkLicenseUsed("TRANSFERRED_LIC_" + data.GetValueOrDefault("LicenseUid", ""));

                var newData = new Dictionary<string, string>(data)
                {
                    ["LicenseId"] = newSystemCode,
                    ["Activated"] = "True",
                    ["Transferred"] = "False"
                };

                foreach (var k in newData.Keys.Where(x => x.StartsWith("C")).ToList())
                    newData.Remove(k);
                for (var i = 0; i < newSystemCode.Length; i++)
                    newData[$"C{i}"] = ((int)newSystemCode[i]).ToString();

                EncryptToFile(SerializeLicense(newData), savePath);
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
        private static LicenseError FloatingCheckoutDetailed(string uid, string machineCode, bool isActivationCode = false)
        {
            try
            {
                var machineName = Environment.MachineName;
                var fields = new Dictionary<string, string>
                {
                    ["machine"] = machineCode,
                    ["machine_name"] = machineName,
                    ["product"] = AppProduct
                };
                // A file-based/legacy per-product License Key is sent as "uid" (exact-match
                // lookup on the server); the shared, admin-set Activation Code is sent as
                // "code" (resolved by product name instead) -- see LicenseServer.Checkout().
                fields[isActivationCode ? "code" : "uid"] = uid;
                var body = new FormUrlEncodedContent(fields);

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
                if (reason == "wrong_product") return LicenseError.WrongProduct;
                if (reason == "product_not_licensed") return LicenseError.ProductNotLicensed;
                if (reason == "ambiguous_customer") return LicenseError.AmbiguousCustomer;
                if (reason == "license_expired") return LicenseError.Expired;
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

        // =====================================================
        // FLOATING — BORROW / RETURN (offline use for N days)
        // =====================================================

        /// <summary>
        /// Resolves the currently-active floating source (Activation Code or file-based
        /// Floating license) the same way CheckLicenseDetailed()'s floating branches do,
        /// for BorrowSeat() to reuse. Kept as a read-only duplication rather than a shared
        /// refactor of CheckLicenseDetailed() itself, to avoid touching that already-audited
        /// path -- if the resolution rules there ever change, mirror the change here too.
        /// </summary>
        private static (string Uid, bool IsActivationCode, string ServerUrl)? ResolveFloatingSource()
        {
            var activatedUid = GetActivatedUid();
            if (activatedUid != null)
            {
                var url = GetServerUrlOverride() ?? "";
                if (url == "") return null;
                return (activatedUid, true, url);
            }

            var fullPath = GetLicensePath();
            if (!File.Exists(fullPath)) return null;
            try
            {
                var data = ReadLicense(DecryptFile(fullPath));
                if (!data.TryGetValue("LicenseType", out var licType) || licType != "Floating") return null;

                var uid = data.GetValueOrDefault("LicenseUid", "");
                var url = GetServerUrlOverride() ?? data.GetValueOrDefault("ServerUrl", "").TrimEnd('/');
                if (uid == "" || url == "") return null;
                return (uid, false, url);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Checks out a seat for N hours of fully offline use (the server clamps
        /// this to both its own max-borrow-window and the license's own expiry — see
        /// LicenseServer.Borrow()). On success, stops any live heartbeat (the server
        /// already converted or would reject a duplicate live session) and writes an
        /// encrypted, machine-bound Borrow.lic — see TryGetActiveBorrow for how it's read
        /// back with no network call.</summary>
        public static bool BorrowSeat(double hours, out string message)
        {
            LastBorrowError = LicenseError.None;

            var source = ResolveFloatingSource();
            if (source == null)
            {
                message = "Activate a floating license first.";
                return false;
            }
            var (uid, isActivationCode, url) = source.Value;
            _serverUrl = url;

            try
            {
                var fields = new Dictionary<string, string>
                {
                    ["machine"] = GenerateLicenseCode(),
                    ["machine_name"] = Environment.MachineName,
                    ["product"] = AppProduct,
                    ["hours"] = hours.ToString(CultureInfo.InvariantCulture)
                };
                fields[isActivationCode ? "code" : "uid"] = uid;
                var body = new FormUrlEncodedContent(fields);

                string json;
                try
                {
                    var resp = _http.PostAsync($"{url}/borrow.php", body).GetAwaiter().GetResult();
                    json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    LastBorrowError = LicenseError.ServerUnreachable;
                    message = $"Could not reach a license server at {url}.";
                    return false;
                }

                if (!json.TrimStart().StartsWith("{"))
                {
                    LastBorrowError = LicenseError.ServerUnreachable;
                    message = $"No license server responded at {url}.";
                    return false;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var success = root.TryGetProperty("success", out var successEl) &&
                              successEl.ValueKind == JsonValueKind.True;

                if (success &&
                    root.TryGetProperty("borrow_token", out var tokenEl) &&
                    root.TryGetProperty("expires_utc", out var expEl) &&
                    DateTime.TryParse(expEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresUtc))
                {
                    // The server is authoritative on the borrow window -- stored verbatim,
                    // never computed locally, so a skewed client clock can't extend it.
                    StopHeartbeat();
                    _seatToken = "";
                    WriteBorrowLicense(uid, isActivationCode, tokenEl.GetString() ?? "", expiresUtc);

                    // The server silently shortens the requested period if it would run
                    // past this license's own expiry, or past its own max-borrow-window --
                    // say so explicitly instead of just handing back a shorter date with
                    // no explanation for why it doesn't match what was typed.
                    var clampedReason = root.TryGetProperty("clamped_reason", out var clampEl) ? clampEl.GetString() : "";
                    var clampNote = clampedReason switch
                    {
                        "license_expiry" => " (shortened -- this license expires before the period you requested.)",
                        "max_window" => " (shortened -- borrowing is capped at the maximum allowed window.)",
                        _ => ""
                    };
                    message = $"Borrowed for offline use until {expiresUtc.ToLocalTime():yyyy-MM-dd HH:mm}.{clampNote}";
                    return true;
                }

                var reason = root.TryGetProperty("reason", out var reasonEl) ? reasonEl.GetString() : "";
                LastBorrowError = reason switch
                {
                    "no_seats" => LicenseError.NoSeatsAvailable,
                    "invalid_license" => LicenseError.InvalidLicense,
                    "wrong_product" => LicenseError.WrongProduct,
                    "product_not_licensed" => LicenseError.ProductNotLicensed,
                    "ambiguous_customer" => LicenseError.AmbiguousCustomer,
                    "license_expired" => LicenseError.Expired,
                    _ => LicenseError.NoSeatsAvailable
                };
                message = reason switch
                {
                    "no_seats" => "No seats available to borrow right now.",
                    "invalid_license" => "The server does not recognise this license.",
                    "wrong_product" => "This license was issued for a different Adroitec product.",
                    "product_not_licensed" => "This server doesn't have a license for this product loaded yet.",
                    "ambiguous_customer" => "This server has licenses loaded for more than one customer.",
                    "license_expired" => "This license has expired.",
                    _ => "Could not borrow a seat."
                };
                return false;
            }
            catch (Exception ex)
            {
                LastBorrowError = LicenseError.ServerUnreachable;
                message = $"Borrow failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>True if Borrow.lic exists, is bound to this machine, and hasn't passed its
        /// ExpiresUtc yet -- entirely local, no network call, so this works while offline.
        /// A borrow past its expiry self-cleans here without needing to contact the server
        /// (which independently frees the seat on its own schedule regardless).</summary>
        public static bool TryGetActiveBorrow(out DateTime expiresLocal)
        {
            expiresLocal = default;
            var path = GetBorrowLicensePath();
            if (!File.Exists(path)) return false;

            try
            {
                var data = ReadLicense(DecryptFile(path));

                // Bound to the machine that borrowed it -- a copied Borrow.lic must not
                // grant offline access on a second PC.
                var machineId = GenerateLicenseCode();
                if (!data.TryGetValue("MachineId", out var storedMachineId) || storedMachineId != machineId)
                    return false;

                for (var i = 0; i < machineId.Length; i++)
                {
                    var key = $"C{i}";
                    if (!data.TryGetValue(key, out var stored) ||
                        !int.TryParse(stored, out var storedVal) ||
                        storedVal != machineId[i])
                    {
                        return false;
                    }
                }

                if (!data.TryGetValue("ExpiresUtc", out var expStr) ||
                    !DateTime.TryParse(expStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresUtc))
                {
                    return false;
                }

                if (expiresUtc <= DateTime.UtcNow)
                {
                    try { File.Delete(path); } catch { }
                    return false;
                }

                expiresLocal = expiresUtc.ToLocalTime();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Returns a borrowed seat early. Only clears the local Borrow.lic on a
        /// successful, parseable server response -- a failed/unreachable call leaves it in
        /// place so offline use keeps working until the borrow naturally expires, and the
        /// user can retry once back online.</summary>
        public static bool ReturnBorrowedSeat(out string message)
        {
            var path = GetBorrowLicensePath();
            if (!File.Exists(path))
            {
                message = "No active borrow to return.";
                return false;
            }

            Dictionary<string, string> data;
            try
            {
                data = ReadLicense(DecryptFile(path));
            }
            catch
            {
                message = "Could not read the local borrow record.";
                return false;
            }

            var token = data.GetValueOrDefault("BorrowToken", "");
            var url = data.GetValueOrDefault("ServerUrl", "");
            if (token == "" || url == "")
            {
                message = "Local borrow record is incomplete.";
                return false;
            }

            try
            {
                var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["seat_token"] = token });
                var resp = _http.PostAsync($"{url}/release.php", body).GetAwaiter().GetResult();
                var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!json.TrimStart().StartsWith("{"))
                {
                    message = "No response from the license server -- try again once you're back online.";
                    return false;
                }
            }
            catch
            {
                message = "Could not reach the license server -- try again once you're back online.";
                return false;
            }

            try { File.Delete(path); } catch { }
            message = "Returned successfully.";
            return true;
        }

        private static void WriteBorrowLicense(string uid, bool isActivationCode, string token, DateTime expiresUtc)
        {
            var machineId = GenerateLicenseCode();
            var data = new Dictionary<string, string>
            {
                ["Product"] = AppProduct,
                ["Uid"] = uid,
                ["IsActivationCode"] = isActivationCode ? "True" : "False",
                ["BorrowToken"] = token,
                ["ServerUrl"] = _serverUrl,
                ["StartedUtc"] = DateTime.UtcNow.ToString("O"),
                ["ExpiresUtc"] = expiresUtc.ToString("O"),
                ["MachineId"] = machineId
            };
            for (var i = 0; i < machineId.Length; i++)
                data[$"C{i}"] = ((int)machineId[i]).ToString();

            EncryptToFile(SerializeLicense(data), GetBorrowLicensePath());
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

        /// <summary>True if this install is set up to use a floating license at all — either via
        /// a Floating .lic file or via a saved Activation Code — regardless of whether a seat
        /// can currently be checked out.</summary>
        public static bool IsFloatingConfigured() => GetActivatedUid() != null || IsFloatingLicense();

        // =====================================================
        // MACHINE CODE
        // =====================================================
        // Per-user, always-writable license storage under %APPDATA% -- NOT next to the
        // exe. A dev build's exe folder happens to be writable, which masked this for a
        // long time, but an MSI installs into C:\Program Files (x86)\..., which a normal
        // (non-admin) user cannot write to, so creating "Resources\License\" there
        // throws UnauthorizedAccessException on first launch. One-time migration: if an
        // old license file sits next to the exe from a pre-fix install, copy it here so
        // re-activation isn't required.
        public static string GetLicensePath()
        {
            var path = Path.Combine(GetLicenseDataDir(), "License.lic");
            MigrateLegacyLicenseFileIfNeeded("License.lic", path);
            return path;
        }

        public static string GetBorrowLicensePath()
        {
            var path = Path.Combine(GetLicenseDataDir(), "Borrow.lic");
            MigrateLegacyLicenseFileIfNeeded("Borrow.lic", path);
            return path;
        }

        private static string GetLicenseDataDir()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Adroitec Engineering Solutions Pvt Ltd", "BulkRenameToolEdition", "License");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private static void MigrateLegacyLicenseFileIfNeeded(string fileName, string newPath)
        {
            try
            {
                if (File.Exists(newPath)) return;
                var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "License", fileName);
                if (File.Exists(legacyPath)) File.Copy(legacyPath, newPath);
            }
            catch
            {
                // Best-effort -- a failed migration just means re-activation is needed,
                // not a crash on launch.
            }
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
        /// Name shown on the Network License Manager app), probes its checkout endpoint,
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

            // Typing/pasting a full URL (e.g. "http://172.16.20.77:8080", possibly with
            // the "/web/api/floating" path already on it, since that's exactly the
            // internal format this same address gets saved/displayed in) is a completely
            // natural thing to do -- without stripping this first, splitting on the first
            // ':' below would parse "http" as the host and mangle the port entirely,
            // silently building a URL that can never connect and reporting the server as
            // unreachable even though it's fine. Strip scheme, any path, and a trailing
            // slash so both the bare "host:port" and a full pasted URL resolve the same.
            const string httpPrefix = "http://";
            const string httpsPrefix = "https://";
            if (addr.StartsWith(httpsPrefix, StringComparison.OrdinalIgnoreCase))
                addr = addr.Substring(httpsPrefix.Length);
            else if (addr.StartsWith(httpPrefix, StringComparison.OrdinalIgnoreCase))
                addr = addr.Substring(httpPrefix.Length);

            var slashIndex = addr.IndexOf('/');
            if (slashIndex >= 0) addr = addr.Substring(0, slashIndex);
            addr = addr.Trim();
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

        /// <summary>
        /// The previously saved server address in the same "host:port" form the user
        /// types into the Network License Server field, or "" if none is saved -- lets
        /// LicenseWindow pre-fill that field instead of forcing a blind retype every
        /// time activation is redone (e.g. after Change License clears the saved value),
        /// where a typo/blank entry looks exactly like the server being unreachable.
        /// </summary>
        public static string GetLastServerAddress()
        {
            var url = GetServerUrlOverride();
            if (url == null) return "";

            var trimmed = url;
            const string httpPrefix = "http://";
            if (trimmed.StartsWith(httpPrefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(httpPrefix.Length);

            const string apiSuffix = "/web/api/floating";
            if (trimmed.EndsWith(apiSuffix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - apiSuffix.Length);

            return trimmed;
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

        /// <summary>
        /// Activates a floating license by Activation Code alone -- the single code an
        /// admin sets on Network License Manager and distributes to every tool, covering
        /// whatever products are currently loaded there -- against the given server
        /// address, with no .lic file needed at all. Saves both so every future launch
        /// checks out the same way (see CheckLicenseDetailed).
        /// </summary>
        public static bool ActivateWithKey(string uid, string serverAddress, out string message)
        {
            uid = (uid ?? "").Trim();
            if (uid == "")
            {
                message = "Enter the Activation Code shown in Network License Manager.";
                return false;
            }

            var url = BuildServerUrl(serverAddress);
            if (url == null)
            {
                message = "Enter a server address, e.g. vizserver:1122 or vizserver@1122.";
                return false;
            }

            SaveServerUrlOverride(url);
            SaveActivatedUid(uid);

            // FloatingCheckoutDetailed reads the server address from the static _serverUrl
            // field, not a parameter -- without this, the very first Activate click in a
            // fresh process posts to a bare "/checkout.php" with no host at all (relative
            // URI, no BaseAddress on the HttpClient), which throws and gets caught as
            // ServerUnreachable regardless of what was typed or how well it parsed. Every
            // other caller of FloatingCheckoutDetailed (CheckLicenseDetailed's two floating
            // branches) already sets this first; this was the one path that didn't.
            _serverUrl = url;

            var result = FloatingCheckoutDetailed(uid, GenerateLicenseCode(), isActivationCode: true);
            LastActivationError = result;
            if (result == LicenseError.None)
            {
                ClearActivationPending();
                message = "Activated successfully.";
                return true;
            }

            // Only a genuinely wrong code should un-stick the saved activation --
            // NoSeatsAvailable/ServerUnreachable are transient, and ProductNotLicensed/
            // AmbiguousCustomer mean the code itself was right but the server side isn't
            // ready yet -- all should keep retrying on the next launch instead of forcing
            // re-entry of a code the user already typed correctly. Since the code was
            // accepted and saved despite the failure, flag it so the app can surface a
            // "License activated successfully" message the moment a later retry finally
            // gets a seat -- otherwise that first success is silent (see IsActivationPending).
            if (result == LicenseError.InvalidLicense) ClearActivatedUid();
            else SetActivationPending();

            message = result switch
            {
                LicenseError.NoSeatsAvailable => $"No seats available ({LastSeatsInUse}/{LastSeatsMax} in use).",
                LicenseError.InvalidLicense => "The server does not recognise this Activation Code.",
                LicenseError.ServerUnreachable => $"Could not reach a license server at {url}.",
                LicenseError.WrongProduct => "This Activation Code was issued for a different Adroitec product.",
                LicenseError.ProductNotLicensed => "This server doesn't have a license for this product loaded yet -- contact your admin.",
                LicenseError.AmbiguousCustomer => "This server has licenses loaded for more than one customer -- contact your admin.",
                _ => $"Activation failed ({result})."
            };
            return false;
        }

        private static void SaveActivatedUid(string uid)
        {
            using var k = Registry.CurrentUser.CreateSubKey(SettingsRegPath);
            k?.SetValue("ActivatedFloatingUid", uid);
        }

        private static string? GetActivatedUid()
        {
            using var k = Registry.CurrentUser.OpenSubKey(SettingsRegPath);
            var v = k?.GetValue("ActivatedFloatingUid") as string;
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        private static void ClearActivatedUid()
        {
            using var k = Registry.CurrentUser.OpenSubKey(SettingsRegPath, writable: true);
            k?.DeleteValue("ActivatedFloatingUid", throwOnMissingValue: false);
        }

        private static void SetActivationPending()
        {
            using var k = Registry.CurrentUser.CreateSubKey(SettingsRegPath);
            k?.SetValue("ActivationPendingConfirmation", "True");
        }

        /// <summary>True if a floating activation was accepted and saved but never got a
        /// confirmed seat -- the caller should show a one-time success message the moment
        /// CheckLicense() next succeeds, then call ClearActivationPending().</summary>
        public static bool IsActivationPending()
        {
            using var k = Registry.CurrentUser.OpenSubKey(SettingsRegPath);
            return (k?.GetValue("ActivationPendingConfirmation") as string) == "True";
        }

        public static void ClearActivationPending()
        {
            using var k = Registry.CurrentUser.OpenSubKey(SettingsRegPath, writable: true);
            k?.DeleteValue("ActivationPendingConfirmation", throwOnMissingValue: false);
        }

        // =====================================================
        // REGISTRY
        // =====================================================
        private static bool IsLicenseUsed(string id)
        {
            using var k = Registry.CurrentUser.OpenSubKey(UsedLicensesRegPath);
            return k?.GetValue(id) != null;
        }

        private static void MarkLicenseUsed(string id)
        {
            using var k = Registry.CurrentUser.CreateSubKey(UsedLicensesRegPath);
            k?.SetValue(id, DateTime.Now.ToString("s"));
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
