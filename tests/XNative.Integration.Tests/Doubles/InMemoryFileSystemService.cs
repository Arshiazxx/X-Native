using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XNative.Core.Abstractions;

namespace XNative.Integration.Tests.Doubles
{
    /// <summary>
    /// Hand-rolled, dictionary-backed <see cref="IFileSystemService"/> double.
    /// Paths are normalised with <see cref="Path.GetFullPath(string)"/> so that
    /// <c>..</c> segments and rooted manifest paths resolve exactly the way a
    /// real file system would resolve them — that fidelity is what makes the
    /// manifest path-traversal gate tests meaningful. Adding a file implicitly
    /// registers every parent directory, like a real tree.
    /// </summary>
    public sealed class InMemoryFileSystemService : IFileSystemService
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _directories = new HashSet<string>(StringComparer.Ordinal);

        public InMemoryFileSystemService AddFile(string path, string content = "")
        {
            string full = Normalize(path);
            _files[full] = content;

            string? parent = Path.GetDirectoryName(full);
            while (!string.IsNullOrEmpty(parent))
            {
                _directories.Add(parent);
                parent = Path.GetDirectoryName(parent);
            }

            return this;
        }

        public InMemoryFileSystemService AddDirectory(string path)
        {
            _directories.Add(Normalize(path));
            return this;
        }

        public bool DirectoryExists(string path)
        {
            return _directories.Contains(Normalize(path));
        }

        public bool FileExists(string path)
        {
            return _files.ContainsKey(Normalize(path));
        }

        public string[] GetFiles(string directory, string pattern)
        {
            string dir = Normalize(directory);
            return _files.Keys
                .Where(file => string.Equals(Path.GetDirectoryName(file), dir, StringComparison.Ordinal))
                .Where(file => MatchesPattern(Path.GetFileName(file), pattern))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToArray();
        }

        public string[] GetDirectories(string directory)
        {
            string dir = Normalize(directory);
            return _directories
                .Where(candidate => string.Equals(Path.GetDirectoryName(candidate), dir, StringComparison.Ordinal))
                .OrderBy(candidate => candidate, StringComparer.Ordinal)
                .ToArray();
        }

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string full = Normalize(path);
            if (!_files.TryGetValue(full, out string? content))
            {
                // FileNotFoundException is an IOException, matching what a real
                // file system surfaces for a missing file.
                throw new FileNotFoundException("File not found in the in-memory file system.", full);
            }

            return Task.FromResult(content);
        }

        public Stream OpenRead(string path)
        {
            string full = Normalize(path);
            if (!_files.TryGetValue(full, out string? content))
            {
                // Matches ReadAllTextAsync: a missing file surfaces as
                // FileNotFoundException, like a real file system.
                throw new FileNotFoundException("File not found in the in-memory file system.", full);
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(content), writable: false);
        }

        private static string Normalize(string path)
        {
            return Path.GetFullPath(path);
        }

        private static bool MatchesPattern(string fileName, string pattern)
        {
            // Blank patterns fail closed, exactly as the real
            // SystemFileSystemService does (see the IFileSystemService
            // contract): a null, empty, or whitespace-only pattern
            // matches nothing and is never widened into a
            // match-everything search.
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            // Observed Directory.GetFiles quirk, reproduced so the
            // double teaches the same behaviour: the pattern "."
            // alone matches every file in the directory.
            if (pattern == ".")
            {
                return true;
            }

            return Win32NameMatcher.IsMatch(pattern, fileName);
        }

        /// <summary>
        /// Reproduces the pattern matching <see cref="Directory.GetFiles(string, string)"/>
        /// performs (its default Win32 match type), so this double agrees
        /// with the real <c>SystemFileSystemService</c> on every pattern
        /// shape, not just the common ones. The semantics are the classic
        /// Win32/DOS wildcard rules, established empirically against
        /// <see cref="Directory.GetFiles(string, string)"/> (the public
        /// <c>FileSystemName.MatchesWin32Expression</c> is NOT the same
        /// matcher — it diverges on exactly the quirk shapes below):
        /// a <c>*</c> followed by a <c>.</c> becomes a DOS_STAR that
        /// anchors the following dot at a dot in the name (last first);
        /// a <c>.</c> adjacent to a wildcard becomes a DOS_DOT that can
        /// also match zero-width at the end of the name; a <c>?</c>
        /// becomes a DOS_QM that matches one non-dot character, or zero
        /// characters at a dot or the end, and can consume a trailing
        /// dot; and <c>*.</c> followed by nothing but <c>?</c>s matches
        /// any name containing a dot. Casing follows the platform
        /// default, exactly like the real enumeration: case-insensitive
        /// on Windows, case-sensitive elsewhere.
        /// </summary>
        private static class Win32NameMatcher
        {
            private static readonly bool IgnoreCase = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            private enum ElementKind
            {
                Literal,
                Star,
                DosStar,
                DosQm,
                DosDot,
            }

            private readonly struct Element
            {
                public Element(ElementKind kind, char value)
                {
                    Kind = kind;
                    Char = value;
                }

                public ElementKind Kind { get; }

                public char Char { get; }
            }

            public static bool IsMatch(string pattern, string name)
            {
                List<Element> elements = Translate(pattern);
                var memo = new Dictionary<(int, int), bool>();
                return Rec(0, 0);

                bool Rec(int patternIndex, int nameIndex)
                {
                    if (memo.TryGetValue((patternIndex, nameIndex), out bool cached))
                    {
                        return cached;
                    }

                    bool result = Compute(patternIndex, nameIndex);
                    memo[(patternIndex, nameIndex)] = result;
                    return result;
                }

                bool Compute(int patternIndex, int nameIndex)
                {
                    if (patternIndex == elements.Count)
                    {
                        return nameIndex == name.Length;
                    }

                    Element element = elements[patternIndex];
                    switch (element.Kind)
                    {
                        case ElementKind.Literal:
                            return nameIndex < name.Length
                                && CharsEqual(name[nameIndex], element.Char)
                                && Rec(patternIndex + 1, nameIndex + 1);
                        case ElementKind.DosDot:
                            if (nameIndex < name.Length && name[nameIndex] == '.')
                            {
                                return Rec(patternIndex + 1, nameIndex + 1);
                            }

                            return nameIndex == name.Length && Rec(patternIndex + 1, nameIndex);
                        case ElementKind.DosQm:
                            if (nameIndex < name.Length && name[nameIndex] != '.')
                            {
                                return Rec(patternIndex + 1, nameIndex + 1);
                            }

                            if (nameIndex < name.Length && name[nameIndex] == '.' && nameIndex == name.Length - 1)
                            {
                                // A '.' that is the name's last character
                                // can be consumed like a normal character
                                // — or matched zero-width, leaving the '.'
                                // for a following pattern element; try both.
                                return Rec(patternIndex + 1, nameIndex + 1) || Rec(patternIndex + 1, nameIndex);
                            }

                            // At a '.' or at the end of the name:
                            // matches zero characters.
                            return Rec(patternIndex + 1, nameIndex);
                        case ElementKind.Star:
                            for (int k = nameIndex; k <= name.Length; k++)
                            {
                                if (Rec(patternIndex + 1, k))
                                {
                                    return true;
                                }
                            }

                            return false;
                        case ElementKind.DosStar:
                        {
                            // Always followed by a DosDot in this
                            // translation. "*." followed by nothing but
                            // '?'s matches any name containing a '.' (the
                            // '?'s match any extension, of any length).
                            if (patternIndex + 2 < elements.Count
                                && AllDosQm(patternIndex + 2)
                                && name.IndexOf('.', nameIndex) >= 0)
                            {
                                return true;
                            }

                            // The pair anchors at a '.' in the remaining
                            // name, trying the LAST '.' first and backing
                            // off to earlier ones. Only when the remainder
                            // has no '.' at all does the star consume the
                            // rest and the dot match zero-width at the
                            // end — and not when yet another dot element
                            // follows the pair.
                            bool anyDot = false;
                            int dot = nameIndex < name.Length
                                ? name.LastIndexOf('.', name.Length - 1, name.Length - nameIndex)
                                : -1;
                            while (dot >= nameIndex)
                            {
                                anyDot = true;
                                if (Rec(patternIndex + 2, dot + 1))
                                {
                                    return true;
                                }

                                if (dot == 0)
                                {
                                    break;
                                }

                                dot = name.LastIndexOf('.', dot - 1, dot);
                            }

                            if (anyDot)
                            {
                                return false;
                            }

                            if (patternIndex + 2 < elements.Count
                                && elements[patternIndex + 2].Kind == ElementKind.DosDot)
                            {
                                return false;
                            }

                            return Rec(patternIndex + 2, name.Length);
                        }
                        default:
                            return false;
                    }
                }

                bool AllDosQm(int from)
                {
                    for (int i = from; i < elements.Count; i++)
                    {
                        if (elements[i].Kind != ElementKind.DosQm)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            private static List<Element> Translate(string pattern)
            {
                var elements = new List<Element>();
                for (int i = 0; i < pattern.Length; i++)
                {
                    char c = pattern[i];
                    switch (c)
                    {
                        case '*':
                            bool followedByDot = i + 1 < pattern.Length && pattern[i + 1] == '.';
                            elements.Add(new Element(
                                followedByDot ? ElementKind.DosStar : ElementKind.Star, c));
                            break;
                        case '?':
                            elements.Add(new Element(ElementKind.DosQm, c));
                            break;
                        case '.':
                            bool prevIsDosStar = elements.Count > 0
                                && elements[elements.Count - 1].Kind == ElementKind.DosStar;
                            bool nextIsStarOrQm = i + 1 < pattern.Length
                                && (pattern[i + 1] == '*' || pattern[i + 1] == '?');
                            elements.Add(new Element(
                                prevIsDosStar || nextIsStarOrQm ? ElementKind.DosDot : ElementKind.Literal, c));
                            break;
                        default:
                            elements.Add(new Element(ElementKind.Literal, c));
                            break;
                    }
                }

                return elements;
            }

            private static bool CharsEqual(char a, char b)
            {
                return IgnoreCase
                    ? char.ToUpperInvariant(a) == char.ToUpperInvariant(b)
                    : a == b;
            }
        }
    }
}
