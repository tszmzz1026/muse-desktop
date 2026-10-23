using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

internal sealed class SessionCookieData
{
    public string name { get; set; }
    public string value { get; set; }
    public string domain { get; set; }
    public string path { get; set; }
    public bool http_only { get; set; }
    public bool secure { get; set; }
    public int same_site { get; set; }
}

internal sealed class SessionCookieSnapshot
{
    public int version { get; set; }
    public long saved_at_utc_ticks { get; set; }
    public List<SessionCookieData> cookies { get; set; }
}

internal sealed class SessionCookieStore
{
    private const int MAX_SNAPSHOT_BYTES = 1024 * 1024;
    private static readonly byte[] ENTROPY = Encoding.UTF8.GetBytes("MuseDesktop.SessionCookies.v1");
    private readonly string _file_path;
    private string _last_hash;
    private DateTime _last_save_utc;

    internal SessionCookieStore(string data_path)
    {
        _file_path = Path.Combine(data_path, "session-cookies.dat");
    }

    private static bool is_muse_domain(string domain)
    {
        if (String.IsNullOrEmpty(domain)) { return false; }
        string host = domain.TrimStart('.');
        return String.Equals(host, "muse.ai", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".muse.ai", StringComparison.OrdinalIgnoreCase);
    }

    private static string cookie_key(string name, string domain, string path)
    {
        return domain + "\u001f" + path + "\u001f" + name;
    }

    internal async Task restore_async(CoreWebView2CookieManager manager)
    {
        if (!File.Exists(_file_path)) { return; }
        byte[] plain_bytes = null;
        try
        {
            FileInfo snapshot_file = new FileInfo(_file_path);
            if (snapshot_file.Length > MAX_SNAPSHOT_BYTES) { return; }
            byte[] encrypted_bytes;
            using (FileStream stream = new FileStream(_file_path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                encrypted_bytes = buffer.ToArray();
            }
            plain_bytes = ProtectedData.Unprotect(encrypted_bytes, ENTROPY, DataProtectionScope.CurrentUser);
            SessionCookieSnapshot snapshot = new JavaScriptSerializer().Deserialize<SessionCookieSnapshot>(Encoding.UTF8.GetString(plain_bytes));
            if (snapshot == null || snapshot.version != 1 || snapshot.cookies == null || snapshot.cookies.Count > 256) { return; }
            long age_ticks = DateTime.UtcNow.Ticks - snapshot.saved_at_utc_ticks;
            if (age_ticks < -TimeSpan.FromMinutes(5).Ticks || age_ticks > TimeSpan.FromDays(30).Ticks) { return; }
            var current_cookies = await manager.GetCookiesAsync("");
            HashSet<string> existing_keys = new HashSet<string>(current_cookies.Select(cookie => cookie_key(cookie.Name, cookie.Domain, cookie.Path)));
            foreach (SessionCookieData saved in snapshot.cookies)
            {
                if (saved == null || !is_muse_domain(saved.domain) || String.IsNullOrEmpty(saved.name)
                    || String.IsNullOrEmpty(saved.path) || !saved.path.StartsWith("/", StringComparison.Ordinal)
                    || !Enum.IsDefined(typeof(CoreWebView2CookieSameSiteKind), saved.same_site)
                    || existing_keys.Contains(cookie_key(saved.name, saved.domain, saved.path))) { continue; }
                CoreWebView2Cookie cookie = manager.CreateCookie(saved.name, saved.value, saved.domain, saved.path);
                cookie.IsHttpOnly = saved.http_only;
                cookie.IsSecure = saved.secure;
                cookie.SameSite = (CoreWebView2CookieSameSiteKind)saved.same_site;
                manager.AddOrUpdateCookie(cookie);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (CryptographicException) { }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        finally
        {
            if (plain_bytes != null) { Array.Clear(plain_bytes, 0, plain_bytes.Length); }
        }
    }

    internal async Task save_async(CoreWebView2CookieManager manager)
    {
        var cookies = await manager.GetCookiesAsync("");
        List<SessionCookieData> session_cookies = cookies
            .Where(cookie => cookie.IsSession && is_muse_domain(cookie.Domain))
            .OrderBy(cookie => cookie.Domain, StringComparer.Ordinal)
            .ThenBy(cookie => cookie.Path, StringComparer.Ordinal)
            .ThenBy(cookie => cookie.Name, StringComparer.Ordinal)
            .Select(cookie => new SessionCookieData
            {
                name = cookie.Name, value = cookie.Value, domain = cookie.Domain, path = cookie.Path,
                http_only = cookie.IsHttpOnly, secure = cookie.IsSecure, same_site = (int)cookie.SameSite
            }).ToList();
        if (session_cookies.Count == 0)
        {
            if (File.Exists(_file_path)) { File.Delete(_file_path); }
            _last_hash = null;
            return;
        }
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        byte[] cookie_bytes = Encoding.UTF8.GetBytes(serializer.Serialize(session_cookies));
        string payload_hash;
        try
        {
            using (SHA256 hasher = SHA256.Create()) { payload_hash = Convert.ToBase64String(hasher.ComputeHash(cookie_bytes)); }
        }
        finally { Array.Clear(cookie_bytes, 0, cookie_bytes.Length); }
        if (payload_hash == _last_hash && File.Exists(_file_path) && DateTime.UtcNow - _last_save_utc < TimeSpan.FromHours(24)) { return; }
        SessionCookieSnapshot snapshot = new SessionCookieSnapshot();
        snapshot.version = 1;
        snapshot.saved_at_utc_ticks = DateTime.UtcNow.Ticks;
        snapshot.cookies = session_cookies;
        byte[] plain_bytes = Encoding.UTF8.GetBytes(serializer.Serialize(snapshot));
        byte[] encrypted_bytes;
        try { encrypted_bytes = ProtectedData.Protect(plain_bytes, ENTROPY, DataProtectionScope.CurrentUser); }
        finally { Array.Clear(plain_bytes, 0, plain_bytes.Length); }
        Directory.CreateDirectory(Path.GetDirectoryName(_file_path));
        string temporary_path = _file_path + ".tmp";
        using (FileStream stream = new FileStream(temporary_path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            stream.Write(encrypted_bytes, 0, encrypted_bytes.Length);
            stream.Flush(true);
        }
        if (File.Exists(_file_path)) { File.Replace(temporary_path, _file_path, null); }
        else { File.Move(temporary_path, _file_path); }
        _last_hash = payload_hash;
        _last_save_utc = DateTime.UtcNow;
    }
}
