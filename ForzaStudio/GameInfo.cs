namespace ForzaStudio
{
    using System;
    using System.IO;

    public enum ForzaGame
    {
        Unknown,
        Motorsport2,
        Motorsport3,
        Motorsport4,
        Horizon1,
        Horizon2
    }

    // Which Forza a game folder holds: the title ID in default.xex (XEX2 execution info), else the
    // files that only one game has.
    public static class GameInfo
    {
        public static ForzaGame Detect(string root)
        {
            if (root == null)
            {
                return ForzaGame.Unknown;
            }
            switch (TitleId(Path.Combine(root, "default.xex")))
            {
                case 0x4D5307EA:
                    return ForzaGame.Motorsport2;
                case 0x4D530910:
                    return ForzaGame.Motorsport4;
                case 0x4D530AA4:
                    return ForzaGame.Horizon2;
            }
            string media = Path.Combine(root, "Media");
            if (File.Exists(Path.Combine(Path.Combine(Path.Combine(media, "audio"), "radio"), "RadioSystem.xml")))
            {
                return ForzaGame.Horizon2;
            }
            string music = Path.Combine(Path.Combine(media, "audio"), "Music");
            if (File.Exists(Path.Combine(music, "InDefaultUIPlaylist.xml")))
            {
                return ForzaGame.Motorsport4;
            }
            if (File.Exists(Path.Combine(music, "OutOfCareerPlaylist.xml")))
            {
                return ForzaGame.Motorsport2;
            }
            return ForzaGame.Unknown;
        }

        public static string Name(ForzaGame game)
        {
            switch (game)
            {
                case ForzaGame.Motorsport2:
                    return "Forza Motorsport 2";
                case ForzaGame.Motorsport3:
                    return "Forza Motorsport 3";
                case ForzaGame.Motorsport4:
                    return "Forza Motorsport 4";
                case ForzaGame.Horizon1:
                    return "Forza Horizon";
                case ForzaGame.Horizon2:
                    return "Forza Horizon 2";
                default:
                    return null;
            }
        }

        // Short tag for the status bar and music label.
        public static string Tag(ForzaGame game)
        {
            switch (game)
            {
                case ForzaGame.Motorsport2:
                    return "FM2";
                case ForzaGame.Motorsport3:
                    return "FM3";
                case ForzaGame.Motorsport4:
                    return "FM4";
                case ForzaGame.Horizon1:
                    return "FH";
                case ForzaGame.Horizon2:
                    return "FH2";
                default:
                    return "Forza";
            }
        }

        // XEX2 optional header 0x00040006 (execution info): title ID at +12. 0 when unreadable.
        public static uint TitleId(string xex)
        {
            try
            {
                if (!File.Exists(xex))
                {
                    return 0;
                }
                byte[] head = new byte[65536];
                int length;
                using (FileStream file = new FileStream(xex, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    length = file.Read(head, 0, head.Length);
                }
                if (length < 24 || head[0] != 'X' || head[1] != 'E' || head[2] != 'X' || head[3] != '2')
                {
                    return 0;
                }
                uint count = U32(head, 20);
                for (int i = 0; i < count && 32 + (i * 8) <= length; i++)
                {
                    if (U32(head, 24 + (i * 8)) == 0x40006)
                    {
                        int info = (int) U32(head, 28 + (i * 8));
                        return (info + 16 <= length) ? U32(head, info + 12) : 0;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return 0;
        }

        private static uint U32(byte[] data, int offset)
        {
            return (uint) ((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }
    }
}
