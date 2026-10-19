using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ScanRepositories : Popup
    {
        /// <summary>
        ///     The directories that are scanned to discover the repositories.
        /// </summary>
        public ObservableCollection<Models.ScanDir> Dirs
        {
            get;
        }

        public bool HasDirs
        {
            get => _hasDirs;
            private set => SetProperty(ref _hasDirs, value);
        }

        /// <summary>
        ///     Whether the result of a scan is available and can be applied.
        /// </summary>
        public bool HasResult
        {
            get => _hasResult;
            private set
            {
                if (SetProperty(ref _hasResult, value))
                    OnPropertyChanged(nameof(SureText));
            }
        }

        /// <summary>
        ///     Keeps the current structure (grouping) of the repository list and only merges the scanned repositories into it.
        /// </summary>
        public bool IsKeepStructure
        {
            get => _isKeepStructure;
            set
            {
                if (SetProperty(ref _isKeepStructure, value))
                {
                    if (value)
                        IsReplaceStructure = false;

                    OnPropertyChanged(nameof(RemovedCount));
                }
            }
        }

        /// <summary>
        ///     Rebuilds the repository list from the result of the scan.
        /// </summary>
        public bool IsReplaceStructure
        {
            get => _isReplaceStructure;
            set
            {
                if (SetProperty(ref _isReplaceStructure, value))
                {
                    if (value)
                        IsKeepStructure = false;

                    OnPropertyChanged(nameof(RemovedCount));
                }
            }
        }

        public int FoundCount
        {
            get => _foundCount;
            private set => SetProperty(ref _foundCount, value);
        }

        public int NewCount
        {
            get => _newCount;
            private set => SetProperty(ref _newCount, value);
        }

        public int RemovedCount
        {
            get => IsReplaceStructure ? _removedInReplace : _removedInKeep;
        }

        public override string SureText
        {
            get => HasResult ? App.Text("ScanRepositories.Apply") : App.Text("ScanRepositories.Scan");
        }

        public ScanRepositories(bool autoScan = false)
        {
            var configured = Preferences.Instance.RepositoryScanDirs;
            Dirs = new ObservableCollection<Models.ScanDir>(configured ?? new List<Models.ScanDir>());
            _hasDirs = Dirs.Count > 0;
            _isKeepStructure = true;

            if (autoScan)
                _ = ScanAsync();
        }

        public void AddDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var normalized = path.Replace('\\', '/').TrimEnd('/');
            if (Dirs.Any(x => PathComparer.Equals(x.Path, normalized)))
                return;

            var dir = new Models.ScanDir() { Path = normalized };
            Dirs.Add(dir);
            Preferences.Instance.RepositoryScanDirs.Add(dir);

            HasDirs = Dirs.Count > 0;
            SaveConfig();
        }

        public void RemoveDirectory(Models.ScanDir dir)
        {
            if (dir == null)
                return;

            Dirs.Remove(dir);
            Preferences.Instance.RepositoryScanDirs.Remove(dir);

            HasDirs = Dirs.Count > 0;
            SaveConfig();
        }

        /// <summary>
        ///     Persists the current configuration of the directories to scan.
        /// </summary>
        public void SaveConfig()
        {
            Preferences.Instance.Save();
        }

        public override async Task<bool> Sure()
        {
            if (!HasResult)
            {
                await ScanAsync();
                return false;
            }

            await ApplyAsync();
            return true;
        }

        /// <summary>
        ///     Scans the configured directories and prepares the result that can be applied afterwards.
        /// </summary>
        public async Task ScanAsync()
        {
            if (_isScanning)
                return;

            if (Dirs.Count == 0)
            {
                Models.Notification.Send(null, App.Text("ScanRepositories.NoDirectories"), true);
                return;
            }

            _isScanning = true;
            InProgress = true;
            HasResult = false;
            ProgressDescription = App.Text("ScanRepositories.Scanning");

            try
            {
                SaveConfig();

                _scanResult = await Models.RepositoryScanner.ScanAsync(Dirs, path =>
                {
                    ProgressDescription = $"{App.Text("ScanRepositories.Scanning")} {path}...";
                });

                var registered = GetRegisteredRepositories();
                _registeredPaths = registered;

                FoundCount = _scanResult.Repositories.Count;
                NewCount = _scanResult.Repositories.Count(x => !registered.Contains(x.Path));
                _removedInKeep = registered.Count(x => !Directory.Exists(x));
                _removedInReplace = registered.Count(x => !_scanResult.Repositories.Any(y => y.Path.Equals(x, StringComparison.OrdinalIgnoreCase)));

                OnPropertyChanged(nameof(RemovedCount));
                HasResult = true;

                if (_scanResult.UntrustedCount > 0)
                    Models.Notification.Send(null, App.Text("TrustRepository.ScanSkipped", _scanResult.UntrustedCount));
            }
            catch (Exception ex)
            {
                Native.OS.LogException(ex);
                Models.Notification.Send(null, ex.Message, true);
            }
            finally
            {
                _isScanning = false;
                InProgress = false;
            }
        }

        /// <summary>
        ///     Applies the result of the scan to the list of the managed repositories.
        /// </summary>
        private async Task ApplyAsync()
        {
            var prefs = Preferences.Instance;
            var result = _scanResult;

            if (IsReplaceStructure)
            {
                // The current structure is replaced by the result of the scan.
                prefs.RepositoryNodes.Clear();

                foreach (var repo in result.Repositories)
                {
                    var node = CreateNode(repo.Path);
                    prefs.AddNode(node, FindOrCreateGroup(prefs, repo.GroupPath), false);
                    await node.UpdateStatusAsync(false, null);
                }
            }
            else
            {
                // The current structure is kept, the scanned repositories are merged into it.
                foreach (var repo in result.Repositories)
                {
                    if (_registeredPaths.Contains(repo.Path))
                        continue;

                    var node = CreateNode(repo.Path);
                    prefs.AddNode(node, FindOrCreateGroup(prefs, repo.GroupPath), false);
                    await node.UpdateStatusAsync(false, null);
                }

                // The repositories that have been deleted from the disk are removed from the list.
                prefs.AutoRemoveInvalidNode();
            }

            prefs.Save();
            Welcome.Instance.Refresh();
        }

        private static RepositoryNode FindOrCreateGroup(Preferences prefs, string groupPath)
        {
            if (string.IsNullOrEmpty(groupPath))
                return null;

            return prefs.FindOrCreateGroupRecursive(groupPath);
        }

        private static RepositoryNode CreateNode(string path)
        {
            return new RepositoryNode()
            {
                Id = path,
                Name = Path.GetFileName(path),
                Bookmark = 0,
                IsRepository = true,
            };
        }

        private static HashSet<string> GetRegisteredRepositories()
        {
            var repos = new HashSet<string>(PathComparer);
            CollectRegisteredRepositories(Preferences.Instance.RepositoryNodes, repos);
            return repos;
        }

        private static void CollectRegisteredRepositories(List<RepositoryNode> nodes, HashSet<string> repos)
        {
            foreach (var node in nodes)
            {
                if (node.IsRepository)
                    repos.Add(node.Id);
                else
                    CollectRegisteredRepositories(node.SubNodes, repos);
            }
        }

        /// <summary>
        ///     The paths of the repositories are compared case sensitively on Linux only, as the file
        ///     system there is case sensitive as well.
        /// </summary>
        private static StringComparer PathComparer
        {
            get => OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        }

        private bool _hasDirs = false;
        private bool _hasResult = false;
        private bool _isKeepStructure = true;
        private bool _isReplaceStructure = false;
        private int _foundCount = 0;
        private int _newCount = 0;
        private int _removedInKeep = 0;
        private int _removedInReplace = 0;
        private bool _isScanning = false;
        private Models.RepositoryScanResult _scanResult = new();
        private HashSet<string> _registeredPaths = new(StringComparer.OrdinalIgnoreCase);    }
}
