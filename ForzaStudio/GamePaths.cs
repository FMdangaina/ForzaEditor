namespace ForzaStudio
{
    using System;
    using System.IO;

    // The game refers to its files as GAME:\Media\..., where GAME: is the game's base folder (the one
    // that contains Media). The base folder is found from any file or folder inside it.
    public static class GamePaths
    {
        private const string Prefix = "game:\\";

        public static bool IsGamePath(string value)
        {
            return value != null && value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && value.Length > Prefix.Length;
        }

        // "GAME:\Media\UI\x.tga" -> "Media\UI\x.tga"
        public static string RelativePath(string gamePath)
        {
            return gamePath.Substring(Prefix.Length).Replace('/', '\\').TrimStart('\\');
        }

        // The game base folder for a path inside it: the parent of its Media folder, or the folder
        // itself if it contains Media. Null when the path is not inside a game folder.
        public static string FindRoot(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            string current = Path.GetFullPath(path);
            if (Directory.Exists(Path.Combine(current, "Media")))
            {
                return current;
            }
            while (!string.IsNullOrEmpty(current))
            {
                if (string.Equals(Path.GetFileName(current), "Media", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetDirectoryName(current);
                }
                current = Path.GetDirectoryName(current);
            }
            return null;
        }

        // Where a GAME:\ file really is: a loose file, or an entry of a mounted zip.
        public class GameFile
        {
            public string FilePath;     // loose file (or folder)
            public string ZipPath;      // or the zip that holds it...
            public string EntryName;    // ...and its name inside

            public bool InZip
            {
                get { return this.ZipPath != null; }
            }

            public string Name
            {
                get { return this.InZip ? this.EntryName : this.FilePath; }
            }

            public override string ToString()
            {
                return this.InZip ? this.ZipPath + " > " + this.EntryName : this.FilePath;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, CachedZip> zips = new System.Collections.Generic.Dictionary<string, CachedZip>(StringComparer.OrdinalIgnoreCase);

        private class CachedZip
        {
            public DateTime Written;
            public ForzaZip Zip;
        }

        // The index of a zip, kept while the file does not change.
        public static ForzaZip OpenZip(string path)
        {
            DateTime written = File.GetLastWriteTimeUtc(path);
            CachedZip cached;
            if (!zips.TryGetValue(path, out cached) || cached.Written != written)
            {
                cached = new CachedZip();
                cached.Written = written;
                cached.Zip = new ForzaZip(path);
                zips[path] = cached;
            }
            return cached.Zip;
        }

        // The game mounts each zip at its own folder (Media\UI\Textures.zip holds Media\UI\Textures\...),
        // and the built game has .xds files where the database still names the .tga source.
        public static GameFile Locate(string root, string gamePath)
        {
            if (root == null || !IsGamePath(gamePath))
            {
                return null;
            }
            string relative = RelativePath(gamePath);
            string[] candidates = { relative, null };
            string extension = Path.GetExtension(relative).ToLowerInvariant();
            if (extension == ".tga" || extension == ".dds" || extension == ".png")
            {
                candidates[1] = Path.ChangeExtension(relative, ".xds");
            }
            foreach (string candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }
                string full = Path.Combine(root, candidate);
                if (File.Exists(full) || Directory.Exists(full))
                {
                    GameFile loose = new GameFile();
                    loose.FilePath = ActualCase(root, full);
                    return loose;
                }
                string[] parts = candidate.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = parts.Length - 1; i >= 1; i--)
                {
                    string zipPath = Path.Combine(root, string.Join("\\", parts, 0, i)) + ".zip";
                    if (!File.Exists(zipPath))
                    {
                        continue;
                    }
                    try
                    {
                        ForzaZip.Entry entry = OpenZip(zipPath).Find(string.Join("/", parts, i, parts.Length - i));
                        if (entry != null)
                        {
                            GameFile inside = new GameFile();
                            inside.ZipPath = ActualCase(root, zipPath);
                            inside.EntryName = entry.Name;
                            return inside;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return null;
        }

        public static byte[] Read(GameFile file)
        {
            if (file.InZip)
            {
                ForzaZip zip = OpenZip(file.ZipPath);
                return zip.Read(zip.Find(file.EntryName));
            }
            return File.ReadAllBytes(file.FilePath);
        }

        // The file on disk for a GAME:\ path, or null.
        public static string Resolve(string root, string gamePath)
        {
            if (root == null || !IsGamePath(gamePath))
            {
                return null;
            }
            string full = Path.Combine(root, RelativePath(gamePath));
            return (File.Exists(full) || Directory.Exists(full)) ? full : null;
        }

        // A car's files: Media\cars\<MediaName>.zip on the disc with archives, or the uncompressed
        // Media\cars\<MediaName> folder.
        public static string FindCar(string root, string mediaName)
        {
            if (root == null || string.IsNullOrEmpty(mediaName))
            {
                return null;
            }
            string cars = Path.Combine(Path.Combine(root, "Media"), "Cars");
            string archive = Path.Combine(cars, mediaName + ".zip");
            if (File.Exists(archive))
            {
                return ActualCase(root, archive);
            }
            string folder = Path.Combine(cars, mediaName);
            return Directory.Exists(folder) ? ActualCase(root, folder) : null;
        }

        // Media\<folder>\<name>.zip, or the uncompressed Media\<folder>\<name> (cars, wheels...).
        public static string FindMedia(string root, string folder, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            string parent = Path.Combine(Path.Combine(root, "Media"), folder);
            string archive = Path.Combine(parent, name + ".zip");
            if (File.Exists(archive))
            {
                return ActualCase(root, archive);
            }
            string directory = Path.Combine(parent, name);
            return Directory.Exists(directory) ? ActualCase(root, directory) : null;
        }

        // The path below root with the letter case it has on disk (the game does not care about it).
        private static string ActualCase(string root, string path)
        {
            string result = root;
            foreach (string part in path.Substring(root.Length).Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] found = Directory.GetFileSystemEntries(result, part);
                result = (found.Length == 1) ? found[0] : Path.Combine(result, part);
            }
            return result;
        }
    }
}
