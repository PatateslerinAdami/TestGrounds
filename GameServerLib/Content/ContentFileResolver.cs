using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LeagueSandbox.GameServer.Content
{
    internal class ContentFileResolver
    {
        private readonly string _packagePath;
        private readonly Dictionary<string, Dictionary<string, List<string>>> _fileIndexes = new();
        private static readonly EnumerationOptions CaseInsensitiveNames = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            MatchType = MatchType.Simple,
            AttributesToSkip = 0,
            IgnoreInaccessible = false
        };

        public ContentFileResolver(string packagePath)
        {
            _packagePath = packagePath;
        }

        public string? ResolveJsonFile(string contentType, string name)
        {
            var path = Path.Combine(_packagePath, contentType, name, name + ".json");
            if (File.Exists(path))
            {
                return path;
            }

            return ResolveCaseInsensitiveJsonFile(contentType, name);
        }

        internal string? ResolveCaseInsensitiveJsonFile(string contentType, string name)
        {
            if (!_fileIndexes.TryGetValue(contentType, out var index))
            {
                index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                _fileIndexes.Add(contentType, index);
            }

            var relativePath = Path.Combine(name, name + ".json");
            if (!index.TryGetValue(relativePath, out var matches))
            {
                matches = FindMatches(Path.Combine(_packagePath, contentType), name);
                index.Add(relativePath, matches);
            }

            if (matches.Count == 0)
            {
                return null;
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Ambiguous {contentType} content '{name}' in package '{_packagePath}': " +
                    $"multiple files differ only by capitalization: {string.Join(", ", matches.OrderBy(path => path, StringComparer.Ordinal))}");
            }

            return matches[0];
        }

        private static List<string> FindMatches(string contentTypePath, string name)
        {
            var matches = new List<string>();
            if (!Directory.Exists(contentTypePath))
            {
                return matches;
            }

            if (string.IsNullOrEmpty(name) || Path.GetFileName(name) != name)
            {
                return matches;
            }

            foreach (var directory in Directory.EnumerateDirectories(contentTypePath, name, CaseInsensitiveNames))
            {
                if (!string.Equals(Path.GetFileName(directory), name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(directory, name + ".json", CaseInsensitiveNames))
                {
                    if (!string.Equals(Path.GetFileName(file), name + ".json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    matches.Add(file);
                }
            }

            return matches;
        }
    }
}
