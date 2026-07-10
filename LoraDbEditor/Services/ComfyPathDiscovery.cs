using System.IO;
using System.Text.Json;

namespace LoraDbEditor.Services
{
    /// <summary>
    /// Discovers sensible default locations for the loras folder and the ComfyUI <c>user-db</c>
    /// directory (which holds <c>lora-triggers.json</c> and the gallery pictures folder).
    ///
    /// <para>
    /// Comfy Desktop 2 no longer keeps models under <c>%USERPROFILE%\Documents\ComfyUI\models</c>.
    /// Managed instances live under <c>%LOCALAPPDATA%\Comfy-Desktop\ComfyUI-Installs\&lt;name&gt;</c>
    /// and pull their models from a <b>shared model space</b> mapped in via a YAML file
    /// (<c>%APPDATA%\Comfy Desktop\shared_model_paths.yaml</c>, v1 fallback
    /// <c>%APPDATA%\ComfyUI\extra_models_config.yaml</c>). Models are shared, but the <c>user-db</c>
    /// lives per-instance in-tree, so the two locations are resolved independently.
    /// </para>
    ///
    /// <para>
    /// Everything here is best-effort: any missing file, malformed YAML/JSON, or IO error is swallowed
    /// and the next candidate is tried, ending at the historical hard-coded legacy defaults so behaviour
    /// is never worse than before. The pure resolution helpers (<see cref="ParseLoraDirsFromYaml"/>,
    /// <see cref="ReadBasePathFromConfig"/>, <see cref="ResolveFirstExisting"/>) take no environment or IO
    /// dependencies so they can be unit-tested against synthetic inputs on any platform.
    /// </para>
    /// </summary>
    public static class ComfyPathDiscovery
    {
        // Electron productName folders used across Desktop generations (→ %APPDATA%\<name>, %LOCALAPPDATA%\<name>).
        private static readonly string[] DesktopProductNames = { "Comfy Desktop", "ComfyUI" };

        // Shared model-paths YAML file names: current "Comfy Desktop 2" first, v1 fallback second.
        private static readonly string[] ModelPathsYamlNames = { "shared_model_paths.yaml", "extra_models_config.yaml" };

        private const string UserDbRelative = @"user\default\user-db";
        private const string DatabaseFileName = "lora-triggers.json";
        private const string GalleryFolderName = "lora-triggers-pictures";

        // ------------------------------------------------------------------ public API

        /// <summary>The resolved loras directory, or the legacy default if nothing better is found.</summary>
        public static string DiscoverLorasPath()
        {
            return ResolveFirstExisting(EnumerateLoraCandidates(), LegacyLorasPath(), Directory.Exists);
        }

        /// <summary>
        /// The resolved <c>user-db</c> directory (parent of <c>lora-triggers.json</c> and the gallery folder),
        /// or the legacy default if nothing better is found.
        /// </summary>
        public static string DiscoverUserDbDirectory()
        {
            return ResolveFirstExisting(EnumerateUserDbCandidates(), LegacyUserDbDirectory(), Directory.Exists);
        }

        /// <summary>The resolved full path to <c>lora-triggers.json</c>.</summary>
        public static string DiscoverDatabasePath()
        {
            return Path.Combine(DiscoverUserDbDirectory(), DatabaseFileName);
        }

        /// <summary>The resolved gallery-pictures directory (sits beside the database).</summary>
        public static string DiscoverGalleryPath()
        {
            return Path.Combine(DiscoverUserDbDirectory(), GalleryFolderName);
        }

        // ------------------------------------------------------------------ pure, testable core

        /// <summary>
        /// Parses a ComfyUI <c>extra_model_paths</c>-style YAML and returns every resolved candidate
        /// loras directory, in file order. Each YAML section may declare a <c>base_path</c> and one or
        /// more <c>loras</c> entries (inline, or as a multi-line block); a relative loras entry is
        /// combined with the most recent <c>base_path</c> above it, an absolute entry is used as-is.
        /// Comments (<c>#</c>), blank lines, and surrounding quotes are ignored. Best-effort: unparsable
        /// content simply yields fewer candidates.
        /// </summary>
        internal static List<string> ParseLoraDirsFromYaml(string yaml)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(yaml))
            {
                return results;
            }

            string? currentBasePath = null;
            var lines = yaml.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var (key, value, indent) = SplitYamlLine(lines[i]);
                if (key == null)
                {
                    continue;
                }

                if (key.Equals("base_path", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        currentBasePath = value;
                    }
                    continue;
                }

                if (!key.Equals("loras", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Collect the loras value(s): an inline value, and/or a following indented block.
                var loraEntries = new List<string>();
                if (!string.IsNullOrWhiteSpace(value) && value != "|" && value != ">" && value != "|-" && value != ">-")
                {
                    loraEntries.Add(value);
                }

                for (int j = i + 1; j < lines.Length; j++)
                {
                    if (string.IsNullOrWhiteSpace(lines[j]))
                    {
                        i = j; // blank line inside a block
                        continue;
                    }

                    int childIndent = CountIndent(lines[j]);
                    var childTrimmed = lines[j].Substring(childIndent);

                    // Block content is any non-comment line indented deeper than the "loras:" key. Sibling
                    // mapping keys (base_path/checkpoints/...) sit at the same indent, ending the block.
                    bool isContinuation = childIndent > indent && !childTrimmed.StartsWith('#');
                    if (!isContinuation)
                    {
                        break;
                    }

                    var token = StripQuotes(StripInlineComment(childTrimmed.TrimStart('-', ' ').Trim()));
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        loraEntries.Add(token);
                    }
                    i = j; // consume the continuation line
                }

                foreach (var entry in loraEntries)
                {
                    var resolved = ResolveAgainstBase(currentBasePath, entry);
                    if (resolved != null)
                    {
                        results.Add(resolved);
                    }
                }
            }

            return results;
        }

        /// <summary>Reads the electron-store <c>basePath</c> from a Desktop <c>config.json</c> body, or null.</summary>
        internal static string? ReadBasePathFromConfig(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("basePath", out var bp) &&
                    bp.ValueKind == JsonValueKind.String)
                {
                    var value = bp.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            catch (JsonException)
            {
                // Malformed config must never break discovery.
            }

            return null;
        }

        /// <summary>Returns the first candidate for which <paramref name="exists"/> is true, else <paramref name="fallback"/>.</summary>
        internal static string ResolveFirstExisting(IEnumerable<string> candidates, string fallback, Func<string, bool> exists)
        {
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && exists(candidate))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        // ------------------------------------------------------------------ YAML line helpers

        /// <summary>
        /// Splits a YAML line into (key, value, indent). Returns key=null for blank/comment lines or lines
        /// with no <c>key:</c> mapping. value is null when the line is not a mapping (e.g. a block-scalar
        /// continuation), and an empty string when the key has no inline value.
        /// </summary>
        private static (string? Key, string? Value, int Indent) SplitYamlLine(string line)
        {
            if (line == null)
            {
                return (null, null, 0);
            }

            int indent = CountIndent(line);
            var trimmed = line.Substring(indent);
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith('-'))
            {
                return (null, null, indent);
            }

            int colon = trimmed.IndexOf(':');
            if (colon < 0)
            {
                return (null, null, indent); // not a mapping line
            }

            var key = trimmed.Substring(0, colon).Trim();
            var value = StripQuotes(StripInlineComment(trimmed.Substring(colon + 1).Trim()));
            return (key, value, indent);
        }

        private static int CountIndent(string line)
        {
            int indent = 0;
            while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t'))
            {
                indent++;
            }

            return indent;
        }

        private static string StripInlineComment(string value)
        {
            // Strip a trailing " # comment" (space before #), leaving genuine '#'-in-path cases alone.
            int hash = value.IndexOf(" #", StringComparison.Ordinal);
            return hash >= 0 ? value.Substring(0, hash).Trim() : value;
        }

        private static string StripQuotes(string value)
        {
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static string? ResolveAgainstBase(string? basePath, string loraEntry)
        {
            if (string.IsNullOrWhiteSpace(loraEntry))
            {
                return null;
            }

            if (Path.IsPathRooted(loraEntry))
            {
                return loraEntry;
            }

            return string.IsNullOrWhiteSpace(basePath) ? null : Path.Combine(basePath, loraEntry);
        }

        // ------------------------------------------------------------------ Windows environment wiring

        /// <summary>Loras candidates in priority order: shared YAML → managed instances → legacy.</summary>
        private static IEnumerable<string> EnumerateLoraCandidates()
        {
            foreach (var yamlPath in ModelPathsYamlFiles())
            {
                var yaml = SafeReadAllText(yamlPath);
                if (yaml == null)
                {
                    continue;
                }

                foreach (var dir in ParseLoraDirsFromYaml(yaml))
                {
                    yield return dir;
                }
            }

            foreach (var instance in EnumerateInstanceRoots())
            {
                yield return Path.Combine(instance, "ComfyUI", "models", "loras");
            }

            yield return LegacyLorasPath();
        }

        /// <summary>User-db candidates in priority order: Desktop config basePath → managed instances → legacy.</summary>
        private static IEnumerable<string> EnumerateUserDbCandidates()
        {
            foreach (var basePath in DesktopBasePaths())
            {
                yield return Path.Combine(basePath, UserDbRelative);
            }

            // Managed instances: prefer one that already holds the database, then any that exists.
            var instanceUserDbs = new List<string>();
            foreach (var instance in EnumerateInstanceRoots())
            {
                instanceUserDbs.Add(Path.Combine(instance, "ComfyUI", UserDbRelative));
            }

            foreach (var userDb in instanceUserDbs)
            {
                if (File.Exists(Path.Combine(userDb, DatabaseFileName)))
                {
                    yield return userDb;
                }
            }

            foreach (var userDb in instanceUserDbs)
            {
                yield return userDb;
            }

            yield return LegacyUserDbDirectory();
        }

        private static IEnumerable<string> ModelPathsYamlFiles()
        {
            var appData = SafeFolder(Environment.SpecialFolder.ApplicationData);
            if (appData == null)
            {
                yield break;
            }

            foreach (var product in DesktopProductNames)
            {
                foreach (var name in ModelPathsYamlNames)
                {
                    yield return Path.Combine(appData, product, name);
                }
            }
        }

        private static IEnumerable<string> DesktopBasePaths()
        {
            var appData = SafeFolder(Environment.SpecialFolder.ApplicationData);
            if (appData == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var product in DesktopProductNames)
            {
                var json = SafeReadAllText(Path.Combine(appData, product, "config.json"));
                if (json == null)
                {
                    continue;
                }

                var basePath = ReadBasePathFromConfig(json);
                if (basePath != null && seen.Add(basePath))
                {
                    yield return basePath;
                }
            }
        }

        /// <summary>Every managed-instance root under the known ComfyUI-Installs parents.</summary>
        private static IEnumerable<string> EnumerateInstanceRoots()
        {
            var parents = new List<string>();

            var localAppData = SafeFolder(Environment.SpecialFolder.LocalApplicationData);
            if (localAppData != null)
            {
                parents.Add(Path.Combine(localAppData, "Comfy-Desktop", "ComfyUI-Installs"));
                foreach (var product in DesktopProductNames)
                {
                    parents.Add(Path.Combine(localAppData, product, "ComfyUI-Installs"));
                }
            }

            var userProfile = SafeFolder(Environment.SpecialFolder.UserProfile);
            if (userProfile != null)
            {
                parents.Add(Path.Combine(userProfile, "ComfyUI-Installs"));
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parent in parents)
            {
                if (!seen.Add(parent))
                {
                    continue;
                }

                string[] roots;
                try
                {
                    roots = Directory.Exists(parent) ? Directory.GetDirectories(parent) : Array.Empty<string>();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var root in roots)
                {
                    yield return root;
                }
            }
        }

        private static string LegacyLorasPath()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Documents", "ComfyUI", "models", "loras");
        }

        private static string LegacyUserDbDirectory()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Documents", "ComfyUI", "user", "default", "user-db");
        }

        private static string? SafeReadAllText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        }

        private static string? SafeFolder(Environment.SpecialFolder folder)
        {
            var value = Environment.GetFolderPath(folder);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
