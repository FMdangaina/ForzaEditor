namespace ForzaStudio
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    // GAME:\Media\zipmanifest.xml lists every archive the game mounts, with where its central
    // directory is: dirstart = its offset, dirsize = its size plus the end record, direntries = files.
    // The game finds the files of a zip through these numbers, so a rewritten zip needs its line updated.
    public static class ZipManifest
    {
        public static string PathFor(string gameRoot)
        {
            return (gameRoot == null) ? null : Path.Combine(Path.Combine(gameRoot, "Media"), "zipmanifest.xml");
        }

        // "C:\...\Media\cars\x.zip" -> "game:\media\cars\x.zip" (the manifest is lower case).
        public static string GamePath(string gameRoot, string zipPath)
        {
            string root = gameRoot.TrimEnd('\\') + "\\";
            if (!zipPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return "game:\\" + zipPath.Substring(root.Length).ToLowerInvariant();
        }

        // The numbers the manifest must hold for a zip file on disk.
        public static void ReadDirectory(string zipPath, out long start, out long size, out int entries)
        {
            using (FileStream file = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int tailLength = (int) Math.Min(file.Length, 65557);
                byte[] tail = new byte[tailLength];
                file.Position = file.Length - tailLength;
                int total = 0;
                while (total < tailLength)
                {
                    total += file.Read(tail, total, tailLength - total);
                }
                for (int i = tail.Length - 22; i >= 0; i--)
                {
                    if (BitConverter.ToUInt32(tail, i) == 0x06054b50)
                    {
                        entries = BitConverter.ToUInt16(tail, i + 10);
                        start = BitConverter.ToUInt32(tail, i + 16);
                        size = BitConverter.ToUInt32(tail, i + 12) + (tail.Length - i);
                        return;
                    }
                }
            }
            throw new InvalidDataException("No zip end record in " + zipPath);
        }

        public static bool Lists(string manifestPath, string gamePath)
        {
            return File.Exists(manifestPath) && FindLine(File.ReadAllText(manifestPath, Encoding.UTF8), gamePath) != null;
        }

        // Rewrites only the three numbers of the zip's line; the rest of the file keeps its bytes.
        // Returns false when the manifest does not list the zip.
        public static bool Update(string manifestPath, string gamePath, string zipPath)
        {
            long start;
            long size;
            int entries;
            ReadDirectory(zipPath, out start, out size, out entries);
            byte[] original = File.ReadAllBytes(manifestPath);
            bool bom = original.Length >= 3 && original[0] == 0xEF && original[1] == 0xBB && original[2] == 0xBF;
            string text = Encoding.UTF8.GetString(original, bom ? 3 : 0, original.Length - (bom ? 3 : 0));
            Match line = FindLine(text, gamePath);
            if (line == null)
            {
                return false;
            }
            string updated = line.Value;
            updated = SetAttribute(updated, "dirstart", start.ToString());
            updated = SetAttribute(updated, "dirsize", size.ToString());
            updated = SetAttribute(updated, "direntries", entries.ToString());
            if (updated == line.Value)
            {
                return true;
            }
            text = text.Substring(0, line.Index) + updated + text.Substring(line.Index + line.Length);
            byte[] body = new UTF8Encoding(false).GetBytes(text);
            byte[] data = new byte[(bom ? 3 : 0) + body.Length];
            if (bom)
            {
                data[0] = 0xEF;
                data[1] = 0xBB;
                data[2] = 0xBF;
            }
            Buffer.BlockCopy(body, 0, data, bom ? 3 : 0, body.Length);
            string temp = manifestPath + ".tmp";
            File.WriteAllBytes(temp, data);
            File.Delete(manifestPath);
            File.Move(temp, manifestPath);
            return true;
        }

        private static Match FindLine(string text, string gamePath)
        {
            foreach (Match match in Regex.Matches(text, "<Zip\\b[^>]*>", RegexOptions.IgnoreCase))
            {
                Match path = Regex.Match(match.Value, "\\bpath=\"([^\"]*)\"", RegexOptions.IgnoreCase);
                if (path.Success && string.Equals(path.Groups[1].Value.Replace('/', '\\'), gamePath, StringComparison.OrdinalIgnoreCase))
                {
                    return match;
                }
            }
            return null;
        }

        private static string SetAttribute(string element, string name, string value)
        {
            return Regex.Replace(element, "\\b" + name + "=\"[^\"]*\"", name + "=\"" + value + "\"", RegexOptions.IgnoreCase);
        }
    }
}
