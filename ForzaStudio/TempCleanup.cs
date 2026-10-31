namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    // Everything Forza Studio writes in %TEMP% is disposable. The DLC caches (DlcSource, DlcThumbs)
    // are reused while unchanged, so they get a size cap and an age limit; trial runs and
    // work folders go after a day; loose temp files and folders go after an hour. Runs at start-up and
    // at exit, on a background thread, and never touches anything that is in use (errors are skipped).
    internal static class TempCleanup
    {
        // Total size the reusable DLC caches may keep (oldest entries go first).
        private const long CacheLimit = 3L * 1024 * 1024 * 1024;
        private static readonly TimeSpan CacheAge = TimeSpan.FromDays(3);
        private static readonly TimeSpan WorkAge = TimeSpan.FromDays(1);
        private static readonly TimeSpan JunkAge = TimeSpan.FromHours(1);

        private static readonly string[] Caches = { "DlcSource", "DlcThumbs", "SoundIndex", "SoundBanks" };

        public static void RunInBackground()
        {
            Thread thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Start();
        }

        public static void Run()
        {
            try
            {
                string temp = Path.GetTempPath();
                string root = Path.Combine(temp, "ForzaStudio");
                if (Directory.Exists(root))
                {
                    CleanRoot(root);
                }
                // Leftovers outside the ForzaStudio folder: GUID-named files and folders of the exporters.
                foreach (string pattern in new string[] { "ForzaStudio_*", "forzastudio_*", "ForzaStudioTurbo" })
                {
                    foreach (string entry in SafeEntries(temp, pattern))
                    {
                        DeleteIfOlder(entry, JunkAge);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static void CleanRoot(string root)
        {
            List<string> cacheEntries = new List<string>();
            foreach (string entry in SafeEntries(root, "*"))
            {
                string name = Path.GetFileName(entry);
                if (Array.IndexOf(Caches, name) >= 0)
                {
                    foreach (string item in SafeEntries(entry, "*"))
                    {
                        if (!DeleteIfOlder(item, CacheAge))
                        {
                            cacheEntries.Add(item);
                        }
                    }
                }
                else if (name == "DlcPackage")
                {
                    // the extracted packages of the versions before the packages were read in place
                    Delete(entry);
                }
                else if (name == "CarPort")
                {
                    foreach (string item in SafeEntries(entry, "*"))
                    {
                        DeleteIfOlder(item, WorkAge);
                    }
                }
                else if (name == "DlcEdit")
                {
                    // the open package being edited: only very old leftovers
                    foreach (string item in SafeEntries(entry, "*"))
                    {
                        DeleteIfOlder(item, TimeSpan.FromDays(14));
                    }
                }
                else
                {
                    DeleteIfOlder(entry, JunkAge);
                }
            }
            // Size cap on what is left of the caches: oldest first.
            long total = 0;
            Dictionary<string, long> sizes = new Dictionary<string, long>();
            foreach (string item in cacheEntries)
            {
                long size = SizeOf(item);
                sizes[item] = size;
                total += size;
            }
            cacheEntries.Sort(delegate (string a, string b) { return LastWrite(a).CompareTo(LastWrite(b)); });
            foreach (string item in cacheEntries)
            {
                if (total <= CacheLimit)
                {
                    break;
                }
                if (Delete(item))
                {
                    total -= sizes[item];
                }
            }
        }

        private static IEnumerable<string> SafeEntries(string folder, string pattern)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    return Directory.GetFileSystemEntries(folder, pattern);
                }
            }
            catch (Exception)
            {
            }
            return new string[0];
        }

        private static DateTime LastWrite(string path)
        {
            try
            {
                return Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);
            }
            catch (Exception)
            {
                return DateTime.UtcNow;
            }
        }

        private static bool DeleteIfOlder(string path, TimeSpan age)
        {
            if (DateTime.UtcNow - LastWrite(path) < age)
            {
                return false;
            }
            return Delete(path);
        }

        private static bool Delete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static long SizeOf(string path)
        {
            long total = 0;
            try
            {
                if (File.Exists(path))
                {
                    return new FileInfo(path).Length;
                }
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    total += new FileInfo(file).Length;
                }
            }
            catch (Exception)
            {
            }
            return total;
        }
    }
}
