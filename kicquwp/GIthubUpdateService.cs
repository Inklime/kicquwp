using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Storage;
using Windows.System;
using System.Net.Http;

namespace kicquwp
{
    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string LatestTag { get; set; }
        public string ReleaseUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public string ReleaseName { get; set; }
        public DateTime PublishedAt { get; set; }
        public bool IsPrerelease { get; set; }
        public string Error { get; set; }
    }

    public class GitHubUpdateService
    {
        private const string OWNER = "Inklime";
        private const string REPO = "kicquwp";
        private static readonly HttpClient _httpClient = new HttpClient();
        static GitHubUpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "kicquwp-app");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }
        public static string GetCurrentVersionString()
        {
            var v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        public static Version GetCurrentVersion()
        {
            var pv = Package.Current.Id.Version;
            return new Version((int)pv.Major, (int)pv.Minor, (int)pv.Build, (int)pv.Revision);
        }
        private static Version ParseTagToVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            tag = tag.Trim().TrimStart('v', 'V');
            var dash = tag.IndexOf('-');
            if (dash > 0) tag = tag.Substring(0, dash);
            Version ver;
            if (Version.TryParse(tag, out ver)) return ver;
            var parts = tag.Split('.');
            if (parts.Length == 3 && Version.TryParse(tag + ".0", out ver)) return ver;
            if (parts.Length == 2 && Version.TryParse(tag + ".0.0", out ver)) return ver;
            if (parts.Length == 1 && Version.TryParse(tag + ".0.0.0", out ver)) return ver;
            return null;
        }
        public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool includePrerelease = false)
        {
            var result = new UpdateCheckResult { CurrentVersion = GetCurrentVersionString() };
            try
            {
                string url = includePrerelease
                    ? $"https://api.github.com/repos/{OWNER}/{REPO}/releases"
                    : $"https://api.github.com/repos/{OWNER}/{REPO}/releases/latest";

                var resp = await _httpClient.GetAsync(new Uri(url));
                if (!resp.IsSuccessStatusCode)
                {
                    result.Error = $"GitHub { (int)resp.StatusCode} {resp.ReasonPhrase}";
                    return result;
                }
                var jsonStr = await resp.Content.ReadAsStringAsync();
                JsonObject json;
                if (includePrerelease)
                {
                    var arr = JsonArray.Parse(jsonStr);
                    json = arr[0].GetObject();
                }
                else
                {
                    json = JsonObject.Parse(jsonStr);
                }
                var tagName = json["tag_name"].GetString();
                result.LatestTag = tagName;
                result.ReleaseUrl = json["html_url"].GetString();
                result.ReleaseNotes = json.ContainsKey("body") ? json["body"].GetString() : "";
                result.ReleaseName = json.ContainsKey("name") ? json["name"].GetString() : tagName;

                var cur = GetCurrentVersion();
                var latest = ParseTagToVersion(tagName);
                if (latest == null)
                {
                    result.LatestVersion = tagName;
                    result.IsUpdateAvailable = tagName != result.CurrentVersion;
                }
                else
                {
                    result.LatestVersion = latest.ToString();
                    result.IsUpdateAvailable = latest > cur;
                }
                ApplicationData.Current.LocalSettings.Values["LastUpdateCheck"] = DateTimeOffset.Now.ToString("o");
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
        }
        public async Task<bool> OpenReleasePageAsync(string url = null)
        {
            if (string.IsNullOrEmpty(url)) url = $"https://github.com/{OWNER}/{REPO}/releases";
            return await Launcher.LaunchUriAsync(new Uri(url));
        }
    }
}