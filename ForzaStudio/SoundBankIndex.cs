namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    // The engine sound banks (Engines\Soundbanks\LOD1\*.fsb) a game can reach: the loose ones in its folder and the ones
    // inside the DLC packages installed in Xenia's content folder. FM4's second disc (and its car packs) ships banks only in
    // packages (the three "Model Year Car Pack" files hold 8N1_E92M3_Exh.fsb), so a folder-only check calls them missing
    // (TODO 3.4, user 2026-10-31). A thin view of SoundIndex, which holds everything else about the game's audio.
    public static class SoundBankIndex
    {
        private static bool InLod1(SoundFile file)
        {
            return file.Name.EndsWith(".fsb", StringComparison.OrdinalIgnoreCase)
                && file.Path.IndexOf("\\soundbanks\\lod1\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // The copy of the bank inside an installed package, or null.
        private static SoundFile PackagedCopy(SoundIndex index, string bank)
        {
            foreach (SoundFile copy in index.Copies(bank))
            {
                if (copy.Source.Packaged && InLod1(copy))
                {
                    return copy;
                }
            }
            return null;
        }

        // Bank file name (any case) -> display name of the package that holds it. Empty when there is no content folder.
        public static Dictionary<string, string> Packages(ForzaGame game)
        {
            Dictionary<string, string> banks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (SoundFile file in SoundIndex.Get(game, null, null).Files)
            {
                if (file.Source.Packaged && InLod1(file) && !banks.ContainsKey(file.Name))
                {
                    banks[file.Name] = file.Source.Name;
                }
            }
            return banks;
        }

        // The package that holds the bank, or null (a bank in the game folder itself is not looked for here).
        public static string PackageOf(ForzaGame game, string bank)
        {
            SoundFile copy = PackagedCopy(SoundIndex.Get(game, null, null), bank);
            return (copy != null) ? copy.Source.Name : null;
        }

        // The bank copied out of its package into the temp folder (a file the game reads from a car's own folder must not
        // depend on a package), or null when no installed package holds it.
        public static string Extract(ForzaGame game, string bank)
        {
            SoundIndex index = SoundIndex.Get(game, null, null);
            SoundFile copy = PackagedCopy(index, bank);
            return (copy != null) ? index.Extract(copy) : null;
        }

        // The state of an engine bank for a game folder: 0 = in the folder, 1 = only in an installed package
        // (package = its name), 2 = nowhere. folderBanks: the game's Engines\Soundbanks\LOD1 folder.
        public static int Where(ForzaGame game, string folderBanks, string bank, out string package)
        {
            package = null;
            if (string.Equals(bank, "NA.fsb", StringComparison.OrdinalIgnoreCase)
                || (Directory.Exists(folderBanks) && File.Exists(Path.Combine(folderBanks, bank))))
            {
                return 0;
            }
            package = PackageOf(game, bank);
            return (package != null) ? 1 : 2;
        }
    }
}
