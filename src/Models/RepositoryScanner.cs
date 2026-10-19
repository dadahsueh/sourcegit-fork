using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    /// <summary>
    ///     A git repository that has been discovered while scanning the configured directories.
    /// </summary>
    public class ScannedRepository
    {
        /// <summary>
        ///     The absolute path of the repository (normalized to use '/' as the separator).
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        ///     The path of the folder that contains the repository, relative to the scanned root.
        ///     Empty when the repository is placed directly under the scanned root.
        /// </summary>
        public string GroupPath { get; set; } = string.Empty;
    }

    /// <summary>
    ///     The outcome of a repository scan.
    /// </summary>
    public class RepositoryScanResult
    {
        public List<ScannedRepository> Repositories { get; } = new List<ScannedRepository>();

        /// <summary>
        ///     The number of repositories that git refused to access because they are not trusted.
        /// </summary>
        public int UntrustedCount { get; set; }
    }

    /// <summary>
    ///     Discovers the git repositories that are placed under the given directories.
    /// </summary>
    public static class RepositoryScanner
    {
        private const int MaxDepth = 5;

        public static async Task<RepositoryScanResult> ScanAsync(IEnumerable<ScanDir> dirs, Action<string> onProgress)
        {
            var result = new RepositoryScanResult();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in dirs)
            {
                if (dir == null || string.IsNullOrEmpty(dir.Path))
                    continue;

                var root = new DirectoryInfo(dir.Path);
                if (!root.Exists)
                    continue;

                onProgress?.Invoke(root.FullName);

                var normalizedRoot = Normalize(root.FullName);
                var found = new List<FoundRepository>();
                await GetRepositoriesAsync(root, found, dir.Recursive, 0, onProgress, result).ConfigureAwait(false);

                foreach (var repo in found)
                {
                    if (!visited.Add(repo.Path))
                        continue;

                    result.Repositories.Add(new ScannedRepository
                    {
                        Path = repo.Path,
                        GroupPath = GetGroupPath(normalizedRoot, repo.DiscoveredPath)
                    });
                }
            }

            return result;
        }

        private static async Task GetRepositoriesAsync(DirectoryInfo dir, List<FoundRepository> outs, bool recursive, int depth, Action<string> onProgress, RepositoryScanResult result)
        {
            var opts = new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
            };

            var subdirs = dir.GetDirectories("*", opts);
            foreach (var subdir in subdirs)
            {
                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                    continue;

                onProgress?.Invoke(subdir.FullName);

                var normalizedSelf = Normalize(subdir.FullName);
                var gitDir = Path.Combine(subdir.FullName, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    var test = await new Commands.QueryRepositoryRootPath(subdir.FullName).GetResultAsync().ConfigureAwait(false);
                    if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                    {
                        // Git reports the physical path of the repository, which may differ from the
                        // discovered one when a symbolic link is involved. The discovered path is kept
                        // as well, it is the one that is relative to the scanned root.
                        outs.Add(new FoundRepository(Normalize(test.StdOut.Trim()), normalizedSelf));
                    }
                    else if (SafeDirectories.IsUntrustedRepository(test.StdErr))
                    {
                        result.UntrustedCount++;
                    }

                    // Never walk into a repository, it may contain worktrees or submodules.
                    continue;
                }

                if (await new Commands.IsBareRepository(subdir.FullName).GetResultAsync().ConfigureAwait(false))
                {
                    outs.Add(new FoundRepository(normalizedSelf, normalizedSelf));
                    continue;
                }

                if (recursive && depth < MaxDepth)
                    await GetRepositoriesAsync(subdir, outs, recursive, depth + 1, onProgress, result).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Calculates the folder that contains the repository, relative to the scanned root.
        /// </summary>
        private static string GetGroupPath(string normalizedRoot, string discovered)
        {
            if (discovered.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var prefix = normalizedRoot + "/";
            if (!discovered.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var parent = discovered.Substring(0, discovered.LastIndexOf('/'));
            if (parent.Length <= prefix.Length)
                return string.Empty;

            return parent.Substring(prefix.Length);
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private class FoundRepository
        {
            public FoundRepository(string path, string discoveredPath)
            {
                Path = path;
                DiscoveredPath = discoveredPath;
            }

            /// <summary>
            ///     The path of the repository as it is reported by git.
            /// </summary>
            public string Path { get; }

            /// <summary>
            ///     The path of the repository as it has been discovered below the scanned root.
            /// </summary>
            public string DiscoveredPath { get; }
        }
    }
}
