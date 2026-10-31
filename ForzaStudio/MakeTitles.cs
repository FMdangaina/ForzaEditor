namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // The titles a car make earns: "<Make> Fan / Enthusiast / Aficionado" at affinity levels 1, 10 and 25, as
    // FM4 has them for every make. Three things make them (docs/MAKE_TITLES.md):
    //   List_BadgeAndTitleUnlocks   three rows <Base>_Affinity_1..3 (category by the alphabet: 4 A-C, 5 D-J,
    //                               6 K-M, 7 N-R, 8 S-W), the badge is the make's logo (_SM and _SM_wtbg);
    //   List_BadgeAndTitleUnlocks.str   their description and title, two keys each, in every language;
    //   a rule Drive_<Base> in the badge rules: Manufacturer id = List_CarMake.ID, testValue AFFINITY, the
    //                               unlock ids at 1 / 10 / 25.
    // The rule is XML. The game reads BadgeAndTitleRules.xml (a file it loads by name: a file of another name
    // in its folder is not read), so an installation in the game adds the block to that file (backed up). A DLC
    // pack carries its own file in the same folder, named by the offer (the official offer 31: 30.xml holds
    // Drive_Ultima), so a trial folder gets one <Base>.xml per make and the pack build joins them
    // (DlcPackage.CopyPortedFiles).
    public static class MakeTitles
    {
        public const string Table = "List_BadgeAndTitleUnlocks";
        public const string RulesFile = "BadgeAndTitleRules.xml";
        public static readonly int[] Levels = { 1, 10, 25 };
        private static readonly string[] Words = { "Fan", "Enthusiast", "Aficionado" };
        private const string TemplateMake = "Ferrari";      // its three rows give each language's wording
        private const string Close = "</BadgeAndTitleRuleList>";

        // Media\GameTunableSettings\BadgesAndTitles under a game or trial root.
        public static string RulesFolder(string root)
        {
            return Path.Combine(Path.Combine(Path.Combine(root, "Media"), "GameTunableSettings"), "BadgesAndTitles");
        }

        public static string GameRules(string root)
        {
            return Path.Combine(RulesFolder(root), RulesFile);
        }

        // True when the database has the unlock table with the columns this writes.
        public static bool Supported(SqliteDatabase db)
        {
            return DbIds.HasTable(db, Table) && DbIds.Schema(db)[Table].Contains("UnlockTitleString");
        }

        // Adds the three rows and their strings. zips: the string table zips (path -> archive) that get the
        // strings; each is changed in memory, the caller saves it. Returns the XML block for the rule, or null
        // when the database has no unlock table or the make has titles already.
        public static string Add(SqliteDatabase db, long makeId, string name, string logoBase, Dictionary<string, ForzaZip> zips, StringBuilder report)
        {
            if (!Supported(db))
            {
                report.AppendLine("  titles: the database has no " + Table + ", none added");
                return null;
            }
            string prefix = logoBase + "_Affinity_";
            if (Convert.ToInt64(db.Scalar("SELECT count(*) FROM " + Table + " WHERE UnlockName LIKE ? ESCAPE '\\'", prefix.Replace("_", "\\_") + "%")) > 0)
            {
                report.AppendLine("  titles: " + logoBase + " has its affinity unlocks already, none added");
                return null;
            }
            object reference = db.Scalar("SELECT UnlockDescription FROM " + Table + " WHERE UnlockDescription LIKE '\\_&%' ESCAPE '\\' LIMIT 1");
            uint sample;
            if (!(reference is string) || !uint.TryParse(((string) reference).Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out sample))
            {
                throw new InvalidOperationException(Table + " has no string id to model the new ones on.");
            }
            uint high = sample & 0xFFFF0000u;

            // The language tables that hold the unlock strings.
            Dictionary<string, StrFile> files = new Dictionary<string, StrFile>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ForzaZip> pair in zips)
            {
                ForzaZip.Entry entry = pair.Value.Find(Table + ".str");
                if (entry != null)
                {
                    files[pair.Key] = new StrFile(pair.Value.Read(entry));
                }
            }
            if (files.Count == 0)
            {
                throw new InvalidOperationException("No string table has " + Table + ".str.");
            }

            // Six keys free in every language (from FFFE down, as the make's names), the Ids after every
            // unlock of FM4, its DLC and the installed packs.
            int[] keys = new int[6];
            int found = 0;
            for (int candidate = 0xFFFE; candidate > 0 && found < keys.Length; candidate--)
            {
                bool used = false;
                foreach (StrFile file in files.Values)
                {
                    used = used || file.Contains(candidate);
                }
                if (!used)
                {
                    keys[found++] = candidate;
                }
            }
            if (found < keys.Length)
            {
                throw new InvalidOperationException(Table + ".str has no free keys.");
            }
            long first = DbIds.Next(db, Table, "UnlockId");

            // Category and place in the list: after the make that comes before it in the alphabet.
            long category = CategoryOf(db, logoBase);
            long sort = SortAfter(db, category, logoBase);

            // The wording of each language, from the template make's rows.
            string[,] pattern = new string[files.Count, 6];
            int language = 0;
            Dictionary<string, int> order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, StrFile> pair in files)
            {
                order[pair.Key] = language;
                for (int level = 0; level < 3; level++)
                {
                    pattern[language, level] = TemplateText(db, pair.Value, "UnlockDescription", level, string.Format(CultureInfo.InvariantCulture, "Reach {0} Affinity Level {1}.", TemplateMake, Levels[level]));
                    pattern[language, 3 + level] = TemplateText(db, pair.Value, "UnlockTitleString", level, TemplateMake + " " + Words[level]);
                }
                language++;
            }

            string logos = @"GAME:\Media\UI\Textures\UI4\Logos\";
            string badge = logos + @"Manufacturer_Logos_SM\L_MAN_" + logoBase + "_Logo.tga";
            string selection = logos + @"Manufacturer_Logos_SM_wtbg\L_MAN_" + logoBase + "_Logo.tga";
            StringBuilder rule = new StringBuilder();
            rule.Append("  <RuleDefinition id=\"Drive_").Append(logoBase).Append("\">\r\n");
            rule.Append("    <Preconditions>\r\n      <Manufacturer id=\"").Append(makeId.ToString(CultureInfo.InvariantCulture)).Append("\" />\r\n    </Preconditions>\r\n");
            rule.Append("    <TriggerLevels testValue=\"AFFINITY\">\r\n");
            for (int level = 0; level < 3; level++)
            {
                long id = first + level;
                string description = "_&" + (high | (uint) keys[level * 2]).ToString(CultureInfo.InvariantCulture);
                string title = "_&" + (high | (uint) keys[(level * 2) + 1]).ToString(CultureInfo.InvariantCulture);
                foreach (KeyValuePair<string, StrFile> pair in files)
                {
                    int at = order[pair.Key];
                    pair.Value.Set(keys[level * 2], pattern[at, level].Replace(TemplateMake, name), "IDS_UnlockDescription_" + id.ToString(CultureInfo.InvariantCulture));
                    pair.Value.Set(keys[(level * 2) + 1], pattern[at, 3 + level].Replace(TemplateMake, name), "IDS_UnlockTitleString_" + id.ToString(CultureInfo.InvariantCulture));
                }
                db.Execute("INSERT INTO " + Table + " (UnlockId, UnlockCategory, UnlockLevel, UnlockName, UnlockDescription, UnlockBadgeTexture, BadgeSelectionTexture, UnlockTitleString, HasBadge, HasTitle, SortOrder, IsSecret) VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, 1, ?, 0)",
                    id, category, level + 1, prefix + (level + 1).ToString(CultureInfo.InvariantCulture), description, badge, selection, title, sort + level);
                rule.Append("      <Trigger value=\"").Append(Levels[level].ToString(CultureInfo.InvariantCulture)).Append("\" unlockId=\"").Append(id.ToString(CultureInfo.InvariantCulture)).Append("\" />\r\n");
            }
            rule.Append("    </TriggerLevels>\r\n  </RuleDefinition>\r\n");

            foreach (KeyValuePair<string, StrFile> pair in files)
            {
                ForzaZip zip = zips[pair.Key];
                zip.Replace(zip.Find(Table + ".str"), pair.Value.ToBytes());
            }
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  titles: {0} {1}/{2}/{3} for make {4} (unlocks {5}-{6}, category {7}, order {8}), 6 strings in {9} languages",
                name, Words[0], Words[1], Words[2], makeId, first, first + 2, category, sort, files.Count));
            return rule.ToString();
        }

        // The text of the template make's unlock in one language, "Ferrari" in it for the make's name; the
        // English wording when the language's text lacks the name (a translation that changes the word).
        private static string TemplateText(SqliteDatabase db, StrFile file, string column, int level, string english)
        {
            object value = db.Scalar("SELECT " + column + " FROM " + Table + " WHERE UnlockName = ?", TemplateMake + "_Affinity_" + (level + 1).ToString(CultureInfo.InvariantCulture));
            uint id;
            if (value is string && ((string) value).StartsWith("_&") && uint.TryParse(((string) value).Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out id))
            {
                foreach (StrFile.Entry entry in file.Entries)
                {
                    if (entry.Key == (int) (id & 0xFFFF) && entry.Text != null && entry.Text.IndexOf(TemplateMake, StringComparison.Ordinal) >= 0)
                    {
                        return entry.Text;
                    }
                }
            }
            return english;
        }

        // The affinity category (4..8, A-C / D-J / K-M / N-R / S-W) whose names the new one falls in.
        private static long CategoryOf(SqliteDatabase db, string logoBase)
        {
            long category = 4;
            long last = 4;
            foreach (object[] row in db.Query("SELECT UnlockCategory, min(UnlockName) FROM " + Table + " WHERE UnlockName LIKE '%\\_Affinity\\_%' ESCAPE '\\' GROUP BY UnlockCategory ORDER BY UnlockCategory").Rows)
            {
                last = Convert.ToInt64(row[0], CultureInfo.InvariantCulture);
                if (string.Compare(logoBase, (string) row[1], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    category = last;
                }
            }
            return Math.Max(category, 4);
        }

        // A SortOrder just after the make before it in the category (10 on, free in the table), the first
        // of the category's when none comes before.
        private static long SortAfter(SqliteDatabase db, long category, string logoBase)
        {
            long sort = -1;
            string before = null;
            foreach (object[] row in db.Query("SELECT UnlockName, SortOrder FROM " + Table + " WHERE UnlockCategory = ? AND UnlockName LIKE '%\\_Affinity\\_1' ESCAPE '\\' ORDER BY SortOrder", category).Rows)
            {
                string name = (string) row[0];
                if (string.Compare(name, logoBase + "_Affinity_1", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    before = name;
                    sort = Convert.ToInt64(row[1], CultureInfo.InvariantCulture);
                }
            }
            if (before == null)
            {
                object min = db.Scalar("SELECT min(SortOrder) FROM " + Table + " WHERE UnlockCategory = ?", category);
                return (min == null || min == DBNull.Value) ? 100 : Math.Max(0, Convert.ToInt64(min, CultureInfo.InvariantCulture) - 10);
            }
            sort += 10;
            while (Convert.ToInt64(db.Scalar("SELECT count(*) FROM " + Table + " WHERE SortOrder BETWEEN ? AND ?", sort, sort + 2)) > 0)
            {
                sort += 10;
            }
            return sort;
        }

        // The game's rules text with the block added before the closing tag (line ends as the file has them).
        public static string AddRule(string rules, string block)
        {
            int end = rules.LastIndexOf(Close, StringComparison.Ordinal);
            if (end < 0)
            {
                throw new InvalidDataException("The badge rules have no " + Close + ".");
            }
            return rules.Substring(0, end) + block + rules.Substring(end);
        }

        // The rules text without the make's Drive_ block, or null when it has none.
        public static string RemoveRule(string rules, string logoBase)
        {
            int at = rules.IndexOf("<RuleDefinition id=\"Drive_" + logoBase + "\"", StringComparison.Ordinal);
            int end = (at < 0) ? -1 : rules.IndexOf("</RuleDefinition>", at, StringComparison.Ordinal);
            if (end < 0)
            {
                return null;
            }
            int start = rules.LastIndexOf('\n', at) + 1;
            end += "</RuleDefinition>".Length;
            if (end < rules.Length && rules[end] == '\r')
            {
                end++;
            }
            if (end < rules.Length && rules[end] == '\n')
            {
                end++;
            }
            return rules.Substring(0, start) + rules.Substring(end);
        }

        // A rules file of its own (a DLC trial folder's, a pack's) holding the blocks.
        public static string Document(IEnumerable<string> blocks)
        {
            StringBuilder xml = new StringBuilder("<BadgeAndTitleRuleList>\r\n\r\n");
            foreach (string block in blocks)
            {
                xml.Append(block).Append("\r\n");
            }
            return xml.Append(Close).ToString();
        }

        // The rule blocks (<RuleDefinition ...> to </RuleDefinition>) of a rules file's text.
        public static List<string> Blocks(string rules)
        {
            List<string> blocks = new List<string>();
            int at = 0;
            while ((at = rules.IndexOf("<RuleDefinition", at, StringComparison.Ordinal)) >= 0)
            {
                int end = rules.IndexOf("</RuleDefinition>", at, StringComparison.Ordinal);
                if (end < 0)
                {
                    break;
                }
                end += "</RuleDefinition>".Length;
                blocks.Add("  " + rules.Substring(at, end - at) + "\r\n");
                at = end;
            }
            return blocks;
        }
    }
}
