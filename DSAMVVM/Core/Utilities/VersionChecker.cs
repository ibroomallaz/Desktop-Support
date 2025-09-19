using System.Text.RegularExpressions;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DSAMVVM.Core.Logging;

namespace DSAMVVM.Core.Utilities
{
    // Result object used by UI; includes parsed model and raw fields read from JSON
    public class VersionCheckResult
    {
        // Parsed model if available
        public VersionInfo? Info { get; set; }

        // Raw fields read case-insensitively from JSON for stable and prerelease
        public string? StableVersion { get; set; }
        public string? StableLocation { get; set; }
        public string? StableChangelog { get; set; }

        public bool PreExists { get; set; }
        public string? PreVersion { get; set; }
        public string? PreLocation { get; set; }
        public string? PreChangelog { get; set; }

        // Error info
        public string? Error { get; set; }

        // Convenience flags
        public bool Success => string.IsNullOrEmpty(Error);
        public bool HasAnyStable => !string.IsNullOrWhiteSpace(StableVersion);
        public bool HasAnyPre => !string.IsNullOrWhiteSpace(PreVersion) || PreExists;
    }

    public static partial class VersionChecker
    {
        private const string Cat = "VersionChecker";

        // Fetches JSON, logs discovered fields, fills both the model (if possible) and raw values
        public static async Task<VersionCheckResult> CheckVersionAsync(string url, IHttpService http)
        {
            try
            {
                Log.Info(Cat, $"fetch.start url=\"{url}\"");
                var json = await http.GetStringAsync(url);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Log.Info(Cat, "fetch.empty");
                    return new VersionCheckResult { Error = "Empty version response." };
                }

                Log.Info(Cat, $"fetch.ok bytes={json.Length}");

                // Extract raw fields directly from JSON
                var res = ExtractRawFields(json);
                Log.Info(Cat,
                    $"peek.current version=\"{Val(res.StableVersion)}\" location=\"{Val(res.StableLocation)}\" changelog=\"{Val(res.StableChangelog)}\"");
                Log.Info(Cat,
                    $"peek.prerelease exists={(res.PreExists ? "true" : "false")} version=\"{Val(res.PreVersion)}\" location=\"{Val(res.PreLocation)}\" changelog=\"{Val(res.PreChangelog)}\"");

                // Attempt to deserialize into expected model types (best-effort)
                VersionInfo? info = null;
                try { info = JsonConvert.DeserializeObject<VersionInfo>(json); } catch { /* ignore */ }
                if (info == null)
                {
                    var wrapper = JsonConvert.DeserializeObject<VersionWrapper>(json);
                    info = wrapper?.Version;
                }

                if (info != null)
                {
                    var stable = info.Current?.Version ?? "(none)";
                    var pre = info.PreRelease?.Version ?? "(none)";
                    var preExists = info.PreRelease?.Exists ?? false;
                    Log.Info(Cat, $"parse.ok stable=\"{stable}\" pre=\"{pre}\" pre.exists={(preExists ? "true" : "false")}");
                }
                else
                {
                    Log.Info(Cat, "parse.fail");
                }

                res.Info = info;
                return res;
            }
            catch (Exception ex)
            {
                Log.Info(Cat, $"error msg=\"{ex.Message}\"");
                return new VersionCheckResult { Error = ex.Message };
            }
        }

        // Version comparison with prerelease awareness
        public static bool IsNewerVersion(string? currentVersion, string? newVersion)
        {
            if (string.IsNullOrWhiteSpace(newVersion)) return false;
            if (string.IsNullOrWhiteSpace(currentVersion)) return true;

            var (baseCurr, labelCurr, numCurr) = ExtractPrerelease(currentVersion);
            var (baseNew, labelNew, numNew) = ExtractPrerelease(newVersion);

            var baseCurrOk = Version.TryParse(baseCurr, out var vCurr);
            var baseNewOk = Version.TryParse(baseNew, out var vNew);

            Log.Debug(Cat, $"compare.start curr=\"{currentVersion}\" new=\"{newVersion}\" parsed.curr base=\"{baseCurr}\" label=\"{labelCurr ?? "(none)"}\" num={numCurr} parsed.new base=\"{baseNew}\" label=\"{labelNew ?? "(none)"}\" num={numNew}");

            if (baseCurrOk && baseNewOk)
            {
                if (vNew > vCurr) { Log.Debug(Cat, "compare.result base:newer"); return true; }
                if (vNew < vCurr) { Log.Debug(Cat, "compare.result base:older"); return false; }

                var currIsPre = !string.IsNullOrEmpty(labelCurr);
                var newIsPre = !string.IsNullOrEmpty(labelNew);

                if (currIsPre && !newIsPre) { Log.Debug(Cat, "compare.result prerelease->stable:newer"); return true; }
                if (!currIsPre && newIsPre) { Log.Debug(Cat, "compare.result stable->prerelease:not-newer"); return false; }
                if (!currIsPre && !newIsPre) { Log.Debug(Cat, "compare.result both-stable-equal:not-newer"); return false; }
            }

            if (!string.IsNullOrEmpty(labelCurr) && !string.IsNullOrEmpty(labelNew))
            {
                if (!labelCurr.Equals(labelNew, StringComparison.OrdinalIgnoreCase))
                {
                    if (labelCurr.Equals("alpha", StringComparison.OrdinalIgnoreCase) &&
                        labelNew.Equals("beta", StringComparison.OrdinalIgnoreCase))
                    { Log.Debug(Cat, "compare.result beta>alpha:newer"); return true; }

                    if (labelCurr.Equals("beta", StringComparison.OrdinalIgnoreCase) &&
                        labelNew.Equals("alpha", StringComparison.OrdinalIgnoreCase))
                    { Log.Debug(Cat, "compare.result alpha<beta:not-newer"); return false; }
                }

                var res = numNew > numCurr;
                Log.Debug(Cat, $"compare.result prerelease.num {(res ? "newer" : "not-newer")}");
                return res;
            }

            if (Version.TryParse(currentVersion, out var vc) && Version.TryParse(newVersion, out var vn))
            {
                var res = vn > vc;
                Log.Debug(Cat, $"compare.result fallback.semantic {(res ? "newer" : "not-newer")}");
                return res;
            }

            var ord = string.CompareOrdinal(newVersion, currentVersion) > 0;
            Log.Debug(Cat, $"compare.result fallback.ordinal {(ord ? "newer" : "not-newer")}");
            return ord;
        }

        // Parses version into base, prerelease label, and numeric suffix; accepts "alpha-2", "alpha2", "alpha.2"
        private static (string Base, string? Label, int Number) ExtractPrerelease(string version)
        {
            var match = VersionRegex().Match(version.Trim());
            if (match.Success)
            {
                var baseVersion = match.Groups["base"].Value;
                var label = match.Groups["label"].Success ? match.Groups["label"].Value : null;
                var number = int.TryParse(match.Groups["number"].Value, out var n) ? n : 1;
                Log.Debug(Cat, $"extract ok ver=\"{version}\" -> base=\"{baseVersion}\" label=\"{label ?? "(none)"}\" num={number}");
                return (baseVersion, label, number);
            }
            Log.Debug(Cat, $"extract miss ver=\"{version}\"");
            return (version, null, 1);
        }

        // Extracts raw fields from JSON regardless of schema casing or wrappers
        private static VersionCheckResult ExtractRawFields(string json)
        {
            var r = new VersionCheckResult();
            try
            {
                var token = JToken.Parse(json);
                var root = token as JObject ?? new JObject();

                // Support either top-level or wrapped {"Version": { ... }}
                var versionObj = (Find(root, "Version") as JObject) ?? root;

                var current = Find(versionObj, "Current") as JObject;
                var pre = Find(versionObj, "PreRelease") as JObject;

                r.StableVersion = ReadString(current, "version");
                r.StableLocation = ReadString(current, "location");
                r.StableChangelog = ReadString(current, "changelog");

                r.PreExists = ReadBool(pre, "exists");
                r.PreVersion = ReadString(pre, "version");
                r.PreLocation = ReadString(pre, "location");
                r.PreChangelog = ReadString(pre, "changelog");
            }
            catch (Exception ex)
            {
                r.Error = $"JSON read error: {ex.Message}";
            }
            return r;
        }

        // Case-insensitive property lookup
        private static JToken? Find(JObject obj, string name)
        {
            foreach (var p in obj.Properties())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p.Value;
            return null;
        }

        // Case-insensitive string reader
        private static string ReadString(JObject? obj, string name)
        {
            if (obj == null) return string.Empty;
            foreach (var p in obj.Properties())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p.Value?.ToString() ?? string.Empty;
            return string.Empty;
        }

        // Case-insensitive bool reader
        private static bool ReadBool(JObject? obj, string name)
        {
            if (obj == null) return false;
            foreach (var p in obj.Properties())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p.Value?.Type == JTokenType.Boolean && p.Value!.Value<bool>();
            return false;
        }

        private static string Val(string? s) => string.IsNullOrWhiteSpace(s) ? "(none)" : s!;

        // Base + optional prerelease with optional dash/dot separator
        [GeneratedRegex(@"^(?<base>\d+\.\d+\.\d+)(?:-(?<label>alpha|beta)(?:[-\.]?(?<number>\d+))?)?$",
            RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex VersionRegex();

        // Optional wrapper contract
        private sealed class VersionWrapper { public VersionInfo? Version { get; set; } }
    }
}
