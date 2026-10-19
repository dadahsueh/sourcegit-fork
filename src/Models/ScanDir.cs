using System.IO;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    /// <summary>
    ///     A directory that is scanned to discover git repositories.
    /// </summary>
    public class ScanDir : ObservableObject
    {
        public string Path
        {
            get => _path;
            set
            {
                if (SetProperty(ref _path, value))
                    OnPropertyChanged(nameof(IsValid));
            }
        }

        /// <summary>
        ///     Whether the sub directories of this directory are scanned as well.
        /// </summary>
        public bool Recursive
        {
            get => _recursive;
            set => SetProperty(ref _recursive, value);
        }

        [JsonIgnore]
        public bool IsValid
        {
            get => !string.IsNullOrEmpty(_path) && Directory.Exists(_path);
        }

        private string _path = string.Empty;
        private bool _recursive = true;
    }
}
