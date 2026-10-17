namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;

    // Brings a car from Forza Horizon 2 into Forza Motorsport 4, as a new car (measured on the cars
    // both games have; see the notes on each step):
    //   model     the FH2 .carbin files converted to FM4 (CarbinModel.ConvertHorizonToMotorsport4),
    //             FH2-only materials renamed to FM4 ones; brakes (type 1) converted too (ConvertBrake);
    //   files     textures, livery masks, MAX data, animations and gauges as they are (the digital
    //             dash .fbf converted to the FM4 Anark layout, ConvertAnarkLayout); CarAttribs.xml rebuilt in the FM4 version-17
    //             form from the donor's; ShaderSettings.xml cut to the materials and parameters FM4 has;
    //   wheel     the stock wheel's zip converted the same way;
    //   database  every row of the car (FM4 and FH2 share almost all table layouts) plus its engine,
    //             drivetrain, torque curve and wheel rows; PI mapped to the FM4 scale, class from it;
    //   strings   the car's names added to Data_Car.str in every FM4 language;
    //   sound     the engine tuning and car model tuning XML (FH2 reuses FM4 engine samples);
    //   thumbnail the car's thumbnail added to the UI textures;
    //   manifest  every rewritten or new zip listed in zipmanifest.xml.
    // Every file the port replaces is backed up first (<exe>\bak\<stamp>_bak_<name>).
    //
    // Forza Motorsport 3 as the source (2026-10-16, measured on the 315 cars FM3 and FM4 share, same ids):
    //   model     CarbinModel.ConvertMotorsport3ToMotorsport4 (header, sections, 40/16/44-byte vertices);
    //             brakes are caliperlf.carbin in FM3, caliperlf_lod0.carbin in FM4;
    //   textures  colour textures marked gamma, as FM4 does (ConvertMotorsport3Texture);
    //   database  FM4 kept FM3's ids (cars, engines, wheels but two); what FM3 lacks is filled as FM4 did
    //             on the shared cars (FillMotorsport4Columns), torque curves get FM4 ids and the
    //             zero-throttle curve becomes ZeroThrottleTorqueScale (ConvertMotorsport3TorqueCurves);
    //   make      Porsche (MakeID 39), which FM4 lacks, added with its names and livery decals (PortMake);
    //   dash      FM3 keeps the digital dash outside the car: Data_Car.GaugeID -> List_DigitalGauges ->
    //             Media\DigitalGauges\<name>.zip, textures in Media\UI\Textures.zip ui4\dashboards. FM4 has
    //             GaugeID -1 on every car and reads digitalgauge\dash_<car>.* from the car zip; the port
    //             puts the dash there (ConvertMotorsport3Dash) and sets GaugeID -1 (AddMotorsport3Dash).
    public class CarPort
    {
        public string SourceRoot;       // FH2 (or FH) game folder (with media)
        public string TargetRoot;       // FM4 game folder
        public string MediaName;        // the car, e.g. MCL_P1_13
        public string DonorMediaName;   // an FM4 car close to it, e.g. MCL_MP412C_11 (brakes, templates)
        public bool Arcade = true;      // Data_Car.IsArcade
        public string OutputRoot;       // trial run: files written here (same layout) instead of TargetRoot;
                                        // a file already there (an earlier car of the same DLC pack) is
                                        // read instead of the target's, so the cars of a pack chain
        public string BackupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bak");
        public StringBuilder Report = new StringBuilder();

        // Review step: what the port found not regular for FM4, and the user's fixes for the next run.
        // A database issue is a value of a row the port wrote that points at a row FM4 lacks, on a
        // relation that has no such value in the original FM4 (GameDbSchema strict), e.g. FH-only
        // FamilyBodyID 27 on the P1. A material issue is a model material no FM4 car uses.
        public class Choice
        {
            public object Value;
            public string Label;

            public override string ToString()
            {
                return this.Label;
            }
        }

        public class Issue
        {
            public string Key;              // Fixes key: "Table.Column=value" or "material:name"
            public string Kind;             // "database" or "material"
            public string Where;            // Data_Car.FamilyBodyID, or the model sections
            public string Value;
            public string Problem;
            public List<Choice> Choices = new List<Choice>();
            public object Suggested;        // the choice to preselect, or null
        }

        public List<Issue> Issues = new List<Issue>();
        public Dictionary<string, object> Fixes = new Dictionary<string, object>();
        private readonly List<string[]> writtenRows = new List<string[]>();   // table, where of every row the port wrote
        private readonly Dictionary<string, List<string>> unknownMaterials = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private string stamp;
        private bool skipThumbnail;     // Textures.zip locked by the running emulator
        private string game = "FH2";    // the source game in the report: FH (Forza Horizon writes the same files), FH2 or FM3
        private bool motorsport3;       // the source is Forza Motorsport 3
        private SqliteDatabase source;
        private SqliteDatabase target;
        private long carId;
        private long donorId;
        private long? newMake;          // a make the port added to List_CarMake (PortMake)
        private string motorsport3Dash; // List_DigitalGauges.MediaName of an FM3 car's dash, or null
        private readonly List<string> engineMedia = new List<string>();
        private readonly List<long> wheelIds = new List<long>();
        private readonly List<long> originalWheels = new List<long>();   // wheels FM4 already had before the port
        private readonly Dictionary<string, ForzaZip> changedZips = new Dictionary<string, ForzaZip>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> newZips = new List<string>();
        private readonly List<string> checkedBanks = new List<string>();   // sound banks FM4 already has (compared once)
        private ForzaZip carZip;        // the converted car, saved last with port_log.txt in it
        private string carZipPath;
        private readonly Dictionary<string, string> newFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // loose target file -> source file
        private readonly List<string> donorContent = new List<string>();   // what the car took from the donor (DonorReport)
        private readonly List<string> donorFormat = new List<string>();    // donor files used only as a layout model

        // FH2 materials FM4 has no shader for -> FM4 materials (null: the subsection goes).
        private static readonly Dictionary<string, string> MaterialMap = BuildMaterialMap();

        private static Dictionary<string, string> BuildMaterialMap()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            map["taillight2S"] = "tail_light";
            map["taillightst"] = "tail_light";
            map["xenonhead"] = "head_light";
            map["drlwhite"] = "head_light";
            map["drlinlorange"] = "lights_amber";
            map["indicator_left"] = "lights_amber";
            map["indicator_right"] = "lights_amber";
            // Alpha-cut cockpit icons: on the cars both games have, FM4's badge holds FH2's badge plus
            // these (X5M 286 + 573 indices -> 860); gauge_emissive has no alpha and shows the quads.
            map["z_gauge_emissive_alpha"] = "badge";
            map["gauge_emissive_alpha"] = "badge";
            map["deprecated"] = null;
            return map;
        }

        // Every parameter name the FM4 cars' ShaderSettings.xml use (all 315 cars read). FH2 adds
        // ones FM4 has no constant for (TailLightColor, XenonHeadlightColor, DRLWhiteColor, ...).
        private static readonly string[] ShaderParameters = new string[]
        {
            "AmbientColor", "BrakeColor", "CarbonFiberDiffuseColor", "CarbonFiberDiffuseColor2", "CarbonFiberMipBias",
            "CarbonFiberScale", "CarbonFiberSpecularColor2", "CarbonFiberSpecularPower2", "ChromeAmbientScale",
            "ChromeDiffuseScale", "ChromeMaxDiffuseShadowAmt", "ChromeMaxReflectionShadowAmt", "ChromeUseSHLerp",
            "CockpitBumpBias", "CockpitBumpScale", "CockpitHeadlinerColor", "CockpitMetalPaintScale", "CockpitNormalScale",
            "CockpitPaintScale", "CockpitPlasticPaintScale", "CockpitSpecularAmount", "CockpitSpecularAmount2",
            "CockpitSpecularPower", "CockpitSpecularPower2", "CubeMultiplier", "CubeScale", "D3DRS_DepthBias",
            "D3DRS_SlopeScaleDepthBias", "DamageLightsSampler", "DarkValues", "DiffuseColorMultiply", "Direct",
            "DirtCylinderHeight", "DirtOffsetU", "DirtOffsetV", "DirtSpecularKillAmount", "DynamicEnvLightmapDampner",
            "FresnelBiasA", "FresnelIndex", "FresnelMax", "FresnelMin", "FresnelPower", "FresnelScalar", "FresnelScale",
            "GaugeAmbientAmt", "GaugeEmissiveScale", "Glancing", "GlassColor", "GlassMaxOpacity", "GrilleColor",
            "GrilleScale", "LightsSampler", "LightValues", "MinCockpitShadow", "MinCockpitSpecular", "MinDiffuse",
            "MinShadowValue", "MottleDark", "MottleLight", "MottleTile", "PaintColor", "PaintScale", "PristinePaintScale",
            "ReverseLightColor", "ShadowScale", "SHLowerScale", "SHTransition", "SHTransitionWidth", "SHUpperScale",
            "SpecularAmount", "SpecularAmount2", "SpecularAmount3", "SpecularColor", "SpecularCubeScalar", "SpecularPower",
            "StaticEnvLightmapDampner", "StitchColor", "UseDiffuseInsteadOfSH", "UV1Tile1",
        };

        // The most tick marks an FM4 gauge has (Firebird Trans Am Speed: 19). The FH2 P1 repeats its
        // 4 speed and 3 tach marks 12 times (48 and 36), past what FM4 reads into its fixed table.
        private const int MaxTicks = 19;

        public void Run()
        {
            this.stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            ForzaGame sourceGame = GameInfo.Detect(this.SourceRoot);
            if ((sourceGame != ForzaGame.Horizon2 && sourceGame != ForzaGame.Horizon1 && sourceGame != ForzaGame.Motorsport3) || GameInfo.Detect(this.TargetRoot) != ForzaGame.Motorsport4)
            {
                throw new InvalidOperationException("The source must be a Forza Horizon, Forza Horizon 2 or Forza Motorsport 3 folder and the target a Forza Motorsport 4 folder.");
            }
            this.game = GameInfo.Tag(sourceGame);
            this.motorsport3 = sourceGame == ForzaGame.Motorsport3;
            string sourceDbPath = Path.Combine(Path.Combine(Path.Combine(this.SourceRoot, "Media"), "db"), "gamedb.slt");
            string targetDbPath = Path.Combine(Path.Combine(Path.Combine(this.TargetRoot, "Media"), "db"), "gamedb.slt");
            this.source = SqliteDatabase.FromBytes(File.ReadAllBytes(sourceDbPath));
            this.target = SqliteDatabase.FromBytes(File.ReadAllBytes(this.In(targetDbPath)));
            try
            {
                this.carId = ToLong(this.source.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", this.MediaName), "the source database has no car " + this.MediaName);
                this.donorId = ToLong(this.target.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", this.DonorMediaName), "the target database has no car " + this.DonorMediaName);
                object existing = this.target.Scalar("SELECT MediaName FROM Data_Car WHERE Id = ?", this.carId);
                if (existing != null && !string.Equals((string) existing, this.MediaName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.Format("Car id {0} is {1} in the target database.", this.carId, existing));
                }
                this.Line("car {0}: id {1}, donor {2} (id {3})", this.MediaName, this.carId, this.DonorMediaName, this.donorId);
                if (this.OutputRoot == null)
                {
                    this.CheckWritable(targetDbPath);
                }

                this.PortDatabase();
                this.PortStrings();
                this.BuildCarZip();
                this.SteeringWheelPosition();
                this.ReviewMaterials();
                this.BuildWheelZips();
                this.PortSound();
                this.PortThumbnail();
                this.DonorReport();

                // Write everything: backups first, then files, then the manifest.
                this.Write(targetDbPath, this.target.ToBytes());
                foreach (KeyValuePair<string, ForzaZip> pair in this.changedZips)
                {
                    string output = this.Out(pair.Key);
                    this.Backup(output);
                    pair.Value.Save(output);
                    this.Line("saved {0}", this.Relative(pair.Key));
                }
                foreach (KeyValuePair<string, string> file in this.newFiles)
                {
                    File.Copy(file.Value, this.Out(file.Key), false);
                    this.Line("added {0}", this.Relative(file.Key));
                }
                // The report travels with the car (FM4 ignores files in a car zip it does not ask for).
                string log = string.Format(CultureInfo.InvariantCulture, "ForzaEditor car port, {0}\r\nsource {1} ({2})\r\ntarget {3}\r\n{4}\r\n",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), this.SourceRoot, this.game, this.TargetRoot,
                    (this.OutputRoot != null) ? "trial run into " + this.OutputRoot : "installed into the game") + "\r\n" + this.Report.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
                this.Put(this.carZip, "port_log.txt", Encoding.UTF8.GetBytes(log));
                this.SaveNew(this.carZip, this.carZipPath);
                this.Line("saved {0}", this.Relative(this.carZipPath));
                this.UpdateManifest();
            }
            finally
            {
                this.source.Dispose();
                this.target.Dispose();
            }
        }

        #region Database

        private Dictionary<string, List<string>> Schema(SqliteDatabase db)
        {
            Dictionary<string, List<string>> schema = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (object[] row in db.Query("SELECT name FROM sqlite_master WHERE type = 'table'").Rows)
            {
                List<string> columns = new List<string>();
                foreach (object[] column in db.Query("PRAGMA table_info(" + SqliteDatabase.Quote((string) row[0]) + ")").Rows)
                {
                    columns.Add((string) column[1]);
                }
                schema[(string) row[0]] = columns;
            }
            return schema;
        }

        // Car tables that belong to the source game's own career, not to the car: FH's EventParticipants
        // put the car into FH events whose ids are other FM4 events (AUD_R8GT_11: 7 rows into FM4
        // events 70, 84, 90, 208, 217, 235 with Priority 0 and ColorSequence 2-4; FM4's own rows have
        // Priority 1..800 and ColorSequence 0).
        private static readonly string[] SourceGameOnly = { "EventParticipants" };

        private void PortDatabase()
        {
            Dictionary<string, List<string>> from = this.Schema(this.source);
            Dictionary<string, List<string>> to = this.Schema(this.target);
            string[] carColumns = { "Ordinal", "CarId", "CarID", "CarOrdinal" };
            int tables = 0;
            int rows = 0;
            List<object[]> engineRows = new List<object[]>();
            foreach (KeyValuePair<string, List<string>> table in to)
            {
                List<string> sourceColumns;
                if (!from.TryGetValue(table.Key, out sourceColumns) || table.Key == "Data_Car" || Array.IndexOf(SourceGameOnly, table.Key) >= 0)
                {
                    continue;
                }
                string where = null;
                foreach (string column in carColumns)
                {
                    if (Has(table.Value, column) && Has(sourceColumns, column))
                    {
                        where = SqliteDatabase.Quote(column) + " = " + this.carId;
                        break;
                    }
                }
                if (where == null && table.Key.StartsWith("List_UpgradeCarBody", StringComparison.Ordinal) && table.Value.Count > 0 && table.Value[0] == "Id")
                {
                    where = "Id / 1000 = " + this.carId;      // body part tables: Id = car * 1000 + level
                }
                if (where == null)
                {
                    continue;
                }
                int copied = this.CopyRows(table.Key, sourceColumns, table.Value, where);
                if (copied > 0)
                {
                    tables++;
                    rows += copied;
                }
            }
            this.PortCarRow(from["Data_Car"], to["Data_Car"]);
            this.Line("database: {0} rows in {1} car tables + Data_Car", rows, tables);

            // Engine, drivetrain, torque curves, wheel: rows the car refers to that FM4 lacks. An engine or
            // drivetrain another FM4 car already uses is FM4's own and stays as it is (FER_458Spider_12's
            // Enzo engine swap, EngineID 87, is used by 13 FM4 cars; copying it replaced their rows).
            List<long> engines = new List<long>();
            foreach (long engine in this.Ids("SELECT DISTINCT EngineID FROM List_UpgradeEngine WHERE Ordinal = ?"))
            {
                object media = this.source.Scalar("SELECT MediaName FROM Data_Engine WHERE EngineID = ?", engine);
                object present = this.target.Scalar("SELECT MediaName FROM Data_Engine WHERE EngineID = ?", engine);
                if (present != null && !string.Equals((string) present, (string) media, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.Format("Engine {0} is {1} in FM4 and {2} in " + this.game + ".", engine, present, media));
                }
                if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM List_UpgradeEngine WHERE EngineID = ? AND Ordinal <> ?", engine, this.carId)) > 0)
                {
                    this.Line("  engine {0} ({1}): FM4's own, kept", engine, present);
                    continue;
                }
                engines.Add(engine);
                if (media != null)
                {
                    this.engineMedia.Add((string) media);
                }
            }
            this.CopyById(from, to, "EngineID", engines, "List_UpgradeEngine");
            List<long> drivetrains = new List<long>();
            foreach (long drivetrain in this.Ids("SELECT DISTINCT DrivetrainID FROM List_UpgradeDrivetrain WHERE Ordinal = ?"))
            {
                if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM List_UpgradeDrivetrain WHERE DrivetrainID = ? AND Ordinal <> ?", drivetrain, this.carId)) > 0)
                {
                    this.Line("  drivetrain {0}: FM4's own, kept", drivetrain);
                    continue;
                }
                drivetrains.Add(drivetrain);
            }
            this.CopyById(from, to, "DrivetrainID", drivetrains, "List_UpgradeDrivetrain");
            if (this.motorsport3)
            {
                this.ConvertMotorsport3TorqueCurves(to, engines);
            }
            else
            {
                this.CopyTorqueCurves(from, to, engines);
            }
            object wheel = this.source.Scalar("SELECT StockWheelID FROM Data_Car WHERE Id = ?", this.carId);
            if (wheel != null && this.motorsport3 && this.Motorsport3Wheel(from, to, Convert.ToInt64(wheel)))
            {
                wheel = null;   // mapped to another FM4 id (Motorsport3Wheel)
            }
            if (wheel != null)
            {
                this.wheelIds.Add(Convert.ToInt64(wheel));
                // A wheel FM4 lists for a car other than this one is FM4's own (a port before this one
                // added the car's wheel together with the car).
                if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM Data_Car WHERE StockWheelID = ? AND Id <> ?", Convert.ToInt64(wheel), this.carId)) > 0)
                {
                    this.originalWheels.Add(Convert.ToInt64(wheel));
                }
                else
                {
                    this.CopyById(from, to, "ID", this.wheelIds, null, "List_Wheels");
                    this.CopyById(from, to, "WheelID", this.wheelIds, null, "WheelExceptions");
                }
            }
            this.CopyReferences(from, to);
            this.PortMake(from, to);
            if (this.motorsport3)
            {
                this.FillMotorsport4Columns(from, to);
            }
            this.StockTires();
            this.DragTires();
            this.ReviewDatabase();
        }

        // Logo folders of UI\Textures.zip the game builds from IconPathBase (L_MAN_<base>_Logo.xds).
        private static readonly string[] LogoFolders = { "Manufacturer_Logos", "Manufacturer_Logos_I", "Manufacturer_Logos_LG", "Manufacturer_Logos_SM", "Manufacturer_Logos_SM_wtbg", "Manufacturer_Logos_kinect" };

        // A make FM4 has no row for comes from the source game: FM3's Porsche (MakeID 39, 28 FM3-only
        // cars) is the only one. FM4 still has its logos in every folder of UI\Textures.zip and its id
        // in List_PartAttribute and three body kit tables, but no List_CarMake row, names or livery
        // decals. The port adds the row, its names (PortStrings: DisplayName, Profile in
        // List_CarMake.str) and its Livery_DecalsSortOrder rows (the livery editor's decals for the
        // make; FM3's 69 Porsche rows all name decals FM4 has, same ids and files). FM4's rows have
        // IconPathBase, the logo's name part (L_MAN_<base>_Logo), and IconPath / IconPathLight in the
        // _I / _SM folders (FM3: Manufacturer_Logos, Manufacturer_Logos_SM\..._lt).
        private void PortMake(Dictionary<string, List<string>> from, Dictionary<string, List<string>> to)
        {
            object make = this.target.Scalar("SELECT MakeID FROM Data_Car WHERE Id = ?", this.carId);
            if (make == null || !from.ContainsKey("List_CarMake") || !to.ContainsKey("List_CarMake"))
            {
                return;
            }
            if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM List_CarMake WHERE ID = ?", make)) > 0)
            {
                // FM4 names a car's make with the make's own string (all 504 cars); a make an earlier
                // port added may have got another string key.
                if (Has(to["Data_Car"], "MakeName"))
                {
                    this.target.Execute("UPDATE Data_Car SET MakeName = (SELECT DisplayName FROM List_CarMake WHERE ID = ?) WHERE Id = ?", make, this.carId);
                }
                return;
            }
            string where = "ID = " + Convert.ToInt64(make).ToString(CultureInfo.InvariantCulture);
            if (this.CopyRows("List_CarMake", from["List_CarMake"], to["List_CarMake"], where) == 0)
            {
                return;     // the source has no row either: the review asks for an FM4 make
            }
            this.newMake = Convert.ToInt64(make);
            string icon = Convert.ToString(this.source.Scalar("SELECT IconPath FROM List_CarMake WHERE " + where), CultureInfo.InvariantCulture);
            Match name = Regex.Match(Path.GetFileNameWithoutExtension(icon.Replace('\\', '/')), "^L_MAN_(.+?)_Logo", RegexOptions.IgnoreCase);
            if (name.Success && Has(to["List_CarMake"], "IconPathBase") && !Has(from["List_CarMake"], "IconPathBase"))
            {
                string logos = @"GAME:\Media\UI\Textures\UI4\Logos\";
                string file = @"\L_MAN_" + name.Groups[1].Value + "_Logo.tga";
                this.target.Execute("UPDATE List_CarMake SET IconPathBase = ?, IconPath = ?, IconPathLight = ? WHERE " + where,
                    name.Groups[1].Value, logos + "Manufacturer_Logos_I" + file, logos + "Manufacturer_Logos_SM" + file);
            }
            this.Line("make {0} ({1}): not in FM4, the " + this.game + " row added", make, this.source.Scalar("SELECT ManufacturerCode FROM List_CarMake WHERE " + where));
            if (name.Success)
            {
                List<string> missing = new List<string>();
                try
                {
                    ForzaZip textures = new ForzaZip(this.In(Path.Combine(Path.Combine(Path.Combine(this.TargetRoot, "Media"), "UI"), "Textures.zip")));
                    foreach (string folder in LogoFolders)
                    {
                        if (textures.Find("UI4/Logos/" + folder + "/L_MAN_" + name.Groups[1].Value + "_Logo.xds") == null)
                        {
                            missing.Add(folder);
                        }
                    }
                }
                catch (IOException)
                {
                    missing.Add("UI\\Textures.zip unreadable");
                }
                this.Line(missing.Count == 0 ? "  logos: L_MAN_{0}_Logo in all {1} FM4 logo folders" : "  WARNING logos: L_MAN_{0}_Logo missing in " + string.Join(", ", missing.ToArray()), name.Groups[1].Value, LogoFolders.Length);
            }
            if (from.ContainsKey("Livery_DecalsSortOrder") && to.ContainsKey("Livery_DecalsSortOrder"))
            {
                string decals = "MakeID = " + Convert.ToInt64(make).ToString(CultureInfo.InvariantCulture);
                int copied = this.CopyRows("Livery_DecalsSortOrder", from["Livery_DecalsSortOrder"], to["Livery_DecalsSortOrder"], decals);
                int dropped = this.target.Execute("DELETE FROM Livery_DecalsSortOrder WHERE " + decals + " AND Livery_DecalID NOT IN (SELECT ID FROM Livery_Decals)");
                this.Line("  livery decals: {0} Livery_DecalsSortOrder rows{1}", copied - dropped, (dropped > 0) ? string.Format(CultureInfo.InvariantCulture, " ({0} name decals FM4 lacks, left out)", dropped) : "");
            }
        }

        // FM4's cockpit camera hangs on CarPartPositions PartId 9, the steering wheel point, which FM3
        // lacks (PartId 0-8): without it the cockpit view sits at 0,0,0. It is rebuilt from the
        // converted cockpit model's steering_wheel section (CarCamera).
        private void SteeringWheelPosition()
        {
            if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM CarPartPositions WHERE Ordinal = ? AND PartId = ?", this.carId, CarCamera.SteeringWheelPart)) > 0)
            {
                return;
            }
            Microsoft.Xna.Framework.Vector3 point = Microsoft.Xna.Framework.Vector3.Zero;
            bool found = false;
            foreach (ForzaZip.Entry entry in this.carZip.Entries)
            {
                if (entry.Name.EndsWith("_cockpit.carbin", StringComparison.OrdinalIgnoreCase))
                {
                    found = CarCamera.SteeringWheelFromModel(new CarbinModel(this.carZip.Read(entry)), out point);
                    break;
                }
            }
            if (!found)
            {
                this.Line("  WARNING cockpit camera: no steering_wheel part in the cockpit model and no CarPartPositions part 9; the cockpit view will sit at 0,0,0 (set it in the model view, Cockpit Camera)");
                return;
            }
            CarCamera camera = CarCamera.Load(this.target, this.MediaName);
            camera.SteeringWheel = point;
            this.Line("  cockpit camera: no CarPartPositions part 9 (steering wheel point), rebuilt from the cockpit model's steering_wheel");
            foreach (string line in camera.Save(this.target))
            {
                this.Line("    {0}", line);
            }
        }

        // FM3 torque curves -> FM4 (measured on the 941 camshaft rows of the engines both games have):
        // FM4 kept FM3's full-throttle curve (same values) under an id EngineID * 1000 + n, and FM3's
        // zero-throttle curve became the full curve's ZeroThrottleTorqueScale (= that curve's
        // TorqueScale). FM3 curves have up to 150 points, FM4's columns go to v185 (0 past the count).
        private void ConvertMotorsport3TorqueCurves(Dictionary<string, List<string>> to, List<long> engines)
        {
            List<string> columns = to["List_TorqueCurve"];
            int converted = 0;
            foreach (long engine in engines)
            {
                foreach (object[] cam in this.source.Query("SELECT Id, Level, TorqueCurveFullThrottleID, TorqueCurveZeroThrottleID FROM List_UpgradeEngineCamshaft WHERE EngineID = ?", engine).Rows)
                {
                    if (cam[2] == null)
                    {
                        continue;
                    }
                    SqliteDatabase.Result full = this.source.Query("SELECT * FROM List_TorqueCurve WHERE TorqueCurveID = ?", Convert.ToInt64(cam[2]));
                    if (full.Rows.Count == 0)
                    {
                        continue;
                    }
                    object zeroScale = (cam[3] != null) ? this.source.Scalar("SELECT TorqueScale FROM List_TorqueCurve WHERE TorqueCurveID = ?", Convert.ToInt64(cam[3])) : null;
                    long id = (engine * 1000) + Convert.ToInt64(cam[1]);
                    while (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM List_TorqueCurve WHERE TorqueCurveID = ?", id)) > 0)
                    {
                        id++;
                    }
                    object[] values = new object[columns.Count];
                    for (int c = 0; c < columns.Count; c++)
                    {
                        int at = Array.FindIndex(full.Columns, delegate(string s) { return string.Equals(s, columns[c], StringComparison.OrdinalIgnoreCase); });
                        if (string.Equals(columns[c], "TorqueCurveID", StringComparison.OrdinalIgnoreCase))
                        {
                            values[c] = id;
                        }
                        else if (string.Equals(columns[c], "ZeroThrottleTorqueScale", StringComparison.OrdinalIgnoreCase))
                        {
                            values[c] = (zeroScale != null) ? zeroScale : (object) 0.0;
                        }
                        else
                        {
                            values[c] = (at >= 0) ? full.Rows[0][at] : (object) 0.0;
                        }
                    }
                    List<string> quoted = columns.ConvertAll<string>(delegate(string s) { return SqliteDatabase.Quote(s); });
                    this.target.Execute("INSERT INTO List_TorqueCurve (" + string.Join(",", quoted.ToArray()) + ") VALUES (" + string.Join(",", columns.ConvertAll<string>(delegate(string s) { return "?"; }).ToArray()) + ")", values);
                    this.target.Execute("UPDATE List_UpgradeEngineCamshaft SET TorqueCurveFullThrottleID = ? WHERE Id = ?", id, Convert.ToInt64(cam[0]));
                    this.writtenRows.Add(new string[] { "List_TorqueCurve", "TorqueCurveID = " + id });
                    converted++;
                }
            }
            if (converted > 0)
            {
                this.Line("  List_TorqueCurve: {0} FM3 curves under FM4 ids (zero-throttle curve -> ZeroThrottleTorqueScale)", converted);
            }
        }

        // FM3 wheel ids are FM4's but for two (707 renamed, 751 HOL_2_CommodoreVE_09 is FM4's
        // HOL_88_CommodoreVE_11): an id FM4 gives another wheel is mapped to the FM4 id of the same wheel
        // name, or to a new id. True when the wheel was handled here.
        private bool Motorsport3Wheel(Dictionary<string, List<string>> from, Dictionary<string, List<string>> to, long id)
        {
            string media = this.source.Scalar("SELECT MediaName FROM List_Wheels WHERE ID = ?", id) as string;
            string present = this.target.Scalar("SELECT MediaName FROM List_Wheels WHERE ID = ?", id) as string;
            if (media == null || present == null || string.Equals(media, present, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            object same = this.target.Scalar("SELECT ID FROM List_Wheels WHERE MediaName = ? COLLATE NOCASE", media);
            long newId;
            if (same != null)
            {
                newId = Convert.ToInt64(same);
                this.originalWheels.Add(newId);
            }
            else
            {
                newId = Convert.ToInt64(this.target.Scalar("SELECT max(ID) + 1 FROM List_Wheels"));
                this.CopyRowAs("List_Wheels", from["List_Wheels"], to["List_Wheels"], "ID", id, newId);
                if (to.ContainsKey("WheelExceptions") && from.ContainsKey("WheelExceptions"))
                {
                    this.CopyRowAs("WheelExceptions", from["WheelExceptions"], to["WheelExceptions"], "WheelID", id, newId);
                }
            }
            this.wheelIds.Add(newId);
            this.target.Execute("UPDATE Data_Car SET StockWheelID = ? WHERE Id = ?", newId, this.carId);
            this.Line("  wheel {0}: FM3 id {1} is {2} in FM4, {3} id {4} used", media, id, present, (same != null) ? "its FM4" : "new", newId);
            return true;
        }

        // The source rows whose 'key' is oldId, written with newId (common columns).
        private void CopyRowAs(string table, List<string> sourceColumns, List<string> targetColumns, string key, long oldId, long newId)
        {
            List<string> columns = targetColumns.FindAll(delegate(string c) { return Has(sourceColumns, c); });
            List<string> quoted = columns.ConvertAll<string>(delegate(string c) { return SqliteDatabase.Quote(c); });
            int keyAt = columns.FindIndex(delegate(string c) { return string.Equals(c, key, StringComparison.OrdinalIgnoreCase); });
            foreach (object[] row in this.source.Query("SELECT " + string.Join(",", quoted.ToArray()) + " FROM " + SqliteDatabase.Quote(table) + " WHERE " + SqliteDatabase.Quote(key) + " = ?", oldId).Rows)
            {
                row[keyAt] = newId;
                this.target.Execute("INSERT INTO " + SqliteDatabase.Quote(table) + " (" + string.Join(",", quoted.ToArray()) + ") VALUES (" + string.Join(",", columns.ConvertAll<string>(delegate(string c) { return "?"; }).ToArray()) + ")", row);
            }
            this.writtenRows.Add(new string[] { table, SqliteDatabase.Quote(key) + " = " + newId });
        }

        // The FM4 columns FM3 lacks, in every row the port wrote, filled as FM4 filled them on the 315
        // cars both games have: stock ride heights = the car body's (452 of 503 FM4 cars),
        // CarPartPositions.ID = car * 1000 + PartId, chassis lateral and longitudinal friction = FM3's one
        // friction scale (194 rows equal, FM4 retuned the others); every other column the value FM4 uses
        // most (IsDrivable 1, MaxStretchDeltaFromRideHeight 0.5 on all 2686 rows, CVTShiftTime -1,
        // camera offsets 0...). Torque curves are written whole by ConvertMotorsport3TorqueCurves.
        private void FillMotorsport4Columns(Dictionary<string, List<string>> from, Dictionary<string, List<string>> to)
        {
            Dictionary<string, List<string>> wheres = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string[] written in this.writtenRows)
            {
                List<string> list;
                if (!wheres.TryGetValue(written[0], out list))
                {
                    list = new List<string>();
                    wheres[written[0]] = list;
                }
                if (!list.Contains(written[1]))
                {
                    list.Add(written[1]);
                }
            }
            List<string> filled = new List<string>();
            foreach (KeyValuePair<string, List<string>> table in wheres)
            {
                List<string> sourceColumns;
                List<string> targetColumns;
                if (table.Key == "List_TorqueCurve" || !from.TryGetValue(table.Key, out sourceColumns) || !to.TryGetValue(table.Key, out targetColumns))
                {
                    continue;
                }
                string where = "(" + string.Join(") OR (", table.Value.ToArray()) + ")";
                foreach (string column in targetColumns)
                {
                    if (Has(sourceColumns, column))
                    {
                        continue;
                    }
                    string t = SqliteDatabase.Quote(table.Key);
                    string c = SqliteDatabase.Quote(column);
                    string missing = " WHERE (" + where + ") AND " + c + " IS NULL";
                    if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM " + t + missing)) == 0)
                    {
                        continue;
                    }
                    if (table.Key == "Data_Car" && (column == "FrontStockRideHeight" || column == "RearStockRideHeight"))
                    {
                        string body = "Model" + column;
                        this.target.Execute("UPDATE Data_Car SET " + c + " = round((SELECT " + SqliteDatabase.Quote(body) + " FROM Data_CarBody WHERE Data_CarBody.Id = Data_Car.Id * 1000), 4)" + missing);
                        filled.Add(table.Key + "." + column + " = the car body's");
                    }
                    else if (table.Key == "CarPartPositions" && column == "ID")
                    {
                        this.target.Execute("UPDATE CarPartPositions SET ID = Ordinal * 1000 + PartId" + missing);
                        filled.Add("CarPartPositions.ID = car * 1000 + PartId");
                    }
                    else if (table.Key == "List_UpgradeCarBodyChassisStiffness" && column.EndsWith("FrictionScale", StringComparison.Ordinal) && Has(sourceColumns, column.StartsWith("Front", StringComparison.Ordinal) ? "FrontFrictionScale" : "RearFrictionScale"))
                    {
                        string old = column.StartsWith("Front", StringComparison.Ordinal) ? "FrontFrictionScale" : "RearFrictionScale";
                        foreach (object[] row in this.target.Query("SELECT Id FROM " + t + missing).Rows)
                        {
                            this.target.Execute("UPDATE " + t + " SET " + c + " = ? WHERE Id = ?", this.source.Scalar("SELECT " + old + " FROM " + t + " WHERE Id = ?", row[0]), row[0]);
                        }
                        filled.Add(table.Key + "." + column + " = FM3 " + old);
                    }
                    else
                    {
                        object common = this.target.Scalar("SELECT " + c + " FROM " + t + " WHERE " + c + " IS NOT NULL GROUP BY " + c + " ORDER BY count(*) DESC LIMIT 1");
                        if (common == null)
                        {
                            continue;
                        }
                        this.target.Execute("UPDATE " + t + " SET " + c + " = ?" + missing, common);
                        filled.Add(table.Key + "." + column + " = " + Convert.ToString(common, CultureInfo.InvariantCulture));
                    }
                }
            }
            if (filled.Count > 0)
            {
                this.Line("  FM4 columns FM3 lacks: {0}", string.Join("; ", filled.ToArray()));
            }
            object gauge = null;
            if (from.ContainsKey("Data_Car") && Has(from["Data_Car"], "GaugeID"))
            {
                gauge = this.source.Scalar("SELECT GaugeID FROM Data_Car WHERE Id = ?", this.carId);
            }
            if (gauge != null && !(gauge is DBNull) && Convert.ToInt64(gauge) != -1)
            {
                // FM4 does not use GaugeID (-1 on all 503 cars); the dash goes into the car zip.
                this.target.Execute("UPDATE Data_Car SET GaugeID = -1 WHERE Id = ?", this.carId);
                if (Convert.ToInt64(gauge) > 0 && this.source.Scalar("SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'List_DigitalGauges'") != null)
                {
                    this.motorsport3Dash = Convert.ToString(this.source.Scalar("SELECT MediaName FROM List_DigitalGauges WHERE GaugeID = ?", gauge), CultureInfo.InvariantCulture);
                }
                this.Line("  digital dash: FM3 gauge {0} ({1}); Data_Car.GaugeID = -1 as on every FM4 car", gauge, string.IsNullOrEmpty(this.motorsport3Dash) ? "not in List_DigitalGauges" : this.motorsport3Dash);
            }
        }

        // Every value of the rows the port wrote that points at a row FM4 lacks: fixed when the user
        // chose a value for it (Fixes), else listed for the review with FM4's own values to pick from.
        private void ReviewDatabase()
        {
            StringTable names = null;
            try
            {
                names = StringTable.Load(this.TargetRoot, "EN");
            }
            catch (Exception)
            {
            }
            List<string> seen = new List<string>();
            foreach (string[] written in this.writtenRows)
            {
                foreach (GameDbSchema.Relation relation in GameDbSchema.From(written[0]))
                {
                    if (!relation.Strict || relation.IsComposite || !this.TableHas(relation.Table, relation.Column) || !this.TableHas(relation.Target, relation.TargetColumn))
                    {
                        continue;
                    }
                    string column = SqliteDatabase.Quote(relation.Column);
                    string where = "(" + written[1] + ") AND " + GameDatabase.DanglingWhere(relation);
                    foreach (object[] row in this.target.Query("SELECT DISTINCT " + column + " FROM " + SqliteDatabase.Quote(relation.Table) + " WHERE " + where).Rows)
                    {
                        string value = Convert.ToString(row[0], CultureInfo.InvariantCulture);
                        string key = relation.Table + "." + relation.Column + "=" + value;
                        object fix;
                        if (this.Fixes.TryGetValue(key, out fix) && fix != null)
                        {
                            this.target.Execute("UPDATE " + SqliteDatabase.Quote(relation.Table) + " SET " + column + " = ? WHERE (" + written[1] + ") AND " + column + " = ?", fix, row[0]);
                            this.Line("  review fix: {0}.{1} {2} -> {3}", relation.Table, relation.Column, value, Convert.ToString(fix, CultureInfo.InvariantCulture));
                            continue;
                        }
                        if (seen.Contains(key))
                        {
                            continue;
                        }
                        seen.Add(key);
                        Issue issue = new Issue();
                        issue.Key = key;
                        issue.Kind = "database";
                        issue.Where = relation.Table + "." + relation.Column;
                        issue.Value = value;
                        issue.Problem = string.Format("{0} {1} has no row in FM4's {2}", relation.Column, value, relation.Target);
                        string label = this.TableHas(relation.Target, "DisplayName") ? ", DisplayName" : "";
                        foreach (object[] choice in this.target.Query("SELECT " + SqliteDatabase.Quote(relation.TargetColumn) + label + " FROM " + SqliteDatabase.Quote(relation.Target) + " ORDER BY 1").Rows)
                        {
                            Choice c = new Choice();
                            c.Value = choice[0];
                            string text = (label.Length > 0 && names != null) ? names.Lookup(relation.Target, "DisplayName", choice[1] as string) : null;
                            c.Label = Convert.ToString(choice[0], CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(text) ? "" : "  " + text);
                            issue.Choices.Add(c);
                        }
                        this.Issues.Add(issue);
                        this.Line("  REVIEW {0}", issue.Problem);
                    }
                }
            }
        }

        private bool TableHas(string table, string column)
        {
            foreach (object[] row in this.target.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")").Rows)
            {
                if (string.Equals((string) row[1], column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // The user's material fixes (Fixes "material:name" -> FM4 material) for ConvertModel.
        private Dictionary<string, string> MaterialFixes()
        {
            Dictionary<string, string> renames = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> fix in this.Fixes)
            {
                if (fix.Key.StartsWith("material:", StringComparison.Ordinal) && fix.Value is string)
                {
                    renames[fix.Key.Substring("material:".Length)] = (string) fix.Value;
                }
            }
            return renames;
        }

        // The materials no FM4 car uses that are left after the conversion, for the review.
        private void ReviewMaterials()
        {
            foreach (KeyValuePair<string, List<string>> material in this.unknownMaterials)
            {
                Issue issue = new Issue();
                issue.Key = "material:" + material.Key;
                issue.Kind = "material";
                issue.Where = string.Join(" ", material.Value.ToArray());
                issue.Value = material.Key;
                issue.Problem = string.Format("material {0} is used by no FM4 car (renders black)", material.Key);
                foreach (string name in Fm4Materials(2))
                {
                    Choice c = new Choice();
                    c.Value = name;
                    c.Label = name;
                    issue.Choices.Add(c);
                }
                issue.Suggested = SuggestMaterial(material.Key, this.MediaName + ".carbin");
                this.Issues.Add(issue);
            }
            if (this.unknownMaterials.Count > 0)
            {
                this.Line("  REVIEW {0} materials no FM4 car uses", this.unknownMaterials.Count);
            }
        }

        // FH2 gives a few road cars (CarTypeID 1) DOT or slick tyres as stock (P1, LaFerrari, Veneno: 8
        // RacingSlick at level 3; Veyron SS, R8 Plus...: 7 DOTCompetition). FM4 keeps those for track
        // cars (Diablo GTR, Viper Competition Coupe, Leon Supercup); its road hypercars (Enzo, F50,
        // MC12, One-77, LFA) have 6 MaxPerformance at level 1 and 7, 8, 9 as upgrades. Such a car gets
        // the Enzo's rows (ids car*1000 + 0, 2, 3, 4).
        private void StockTires()
        {
            object type = this.target.Scalar("SELECT CarTypeID FROM Data_Car WHERE Id = ?", this.carId);
            object stock = this.target.Scalar("SELECT TireCompoundID FROM List_UpgradeTireCompound WHERE Ordinal = ? AND IsStock = 1", this.carId);
            if (type == null || Convert.ToInt64(type) != 1 || stock == null || Convert.ToInt64(stock) < 7)
            {
                return;
            }
            this.target.Execute("DELETE FROM List_UpgradeTireCompound WHERE Ordinal = ?", this.carId);
            long[][] rows = { new long[] { 0, 1, 1, 6, 0 }, new long[] { 2, 2, 0, 7, 5000 }, new long[] { 3, 3, 0, 8, 10000 }, new long[] { 4, 4, 0, 9, 10000 } };
            foreach (long[] row in rows)
            {
                this.target.Execute("INSERT INTO List_UpgradeTireCompound (Id, Ordinal, Level, IsStock, TireCompoundID, ManufacturerID, Price, MassDiff, DragScale, WindInstabilityScale) VALUES (?, ?, ?, ?, ?, 0, ?, 0, 1, 1)",
                    (this.carId * 1000) + row[0], this.carId, row[1], row[2], row[3], row[4]);
            }
            this.Line("  tyres: " + this.game + " stock compound {0} on a road car -> FM4's 6 MaxPerformance stock, 7/8/9 as upgrades", stock);
        }

        // FM4 sells drag tyres (9 DragTire) to 502 of its 503 cars, always the same row: level 4, price
        // 10000, no manufacturer. FM3 has no compound 9, and FH1/FH2 have it but give it to no car, so
        // every ported car gets that row (the next free level if 4 is taken).
        private void DragTires()
        {
            if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM List_UpgradeTireCompound WHERE Ordinal = ?", this.carId)) == 0
                || Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM List_UpgradeTireCompound WHERE Ordinal = ? AND TireCompoundID = 9", this.carId)) > 0)
            {
                return;
            }
            long level = Math.Max(4, Convert.ToInt64(this.target.Scalar("SELECT max(Level) FROM List_UpgradeTireCompound WHERE Ordinal = ?", this.carId)) + 1);
            long id = (this.carId * 1000) + level;
            while (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM List_UpgradeTireCompound WHERE Id = ?", id)) > 0)
            {
                id++;
            }
            this.target.Execute("INSERT INTO List_UpgradeTireCompound (Id, Ordinal, Level, IsStock, TireCompoundID, ManufacturerID, Price, MassDiff, DragScale, WindInstabilityScale) VALUES (?, ?, ?, 0, 9, 0, 10000, 0, 1, 1)",
                id, this.carId, level);
            this.Line("  tyres: no drag tyres in " + this.game + " -> FM4's 9 DragTire added as upgrade level {0}", level);
        }

        // Every "...ID" value in the rows of the car (and of its engine and drivetrain) that names
        // the key of a List_/Data_ table (first column of that name) must exist in FM4: the missing
        // rows come from FH2, and their own references after them (the P1's bumpers and wing point
        // at List_AeroPhysics rows FM4 does not have).
        private void CopyReferences(Dictionary<string, List<string>> from, Dictionary<string, List<string>> to)
        {
            // Reference column -> table: the key column's own name (AeroPhysicsID -> List_AeroPhysics),
            // or for a key named just Id, the table's name + ID (CarBodyID -> Data_CarBody: wheelbase,
            // tracks, bounding box; without it the physics runs on zeros and goes NaN).
            Dictionary<string, string> keyed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> keyColumn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<string>> table in to)
            {
                List<string> sourceColumns;
                if (table.Value.Count == 0 || !(table.Key.StartsWith("List_") || table.Key.StartsWith("Data_")) || !from.TryGetValue(table.Key, out sourceColumns)
                    || sourceColumns.Count == 0 || !string.Equals(sourceColumns[0], table.Value[0], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string reference = null;
                if (string.Equals(table.Value[0], "Id", StringComparison.OrdinalIgnoreCase))
                {
                    reference = table.Key.Substring(5) + "ID";
                }
                else if (table.Value[0].Length > 2 && table.Value[0].EndsWith("ID", StringComparison.OrdinalIgnoreCase))
                {
                    reference = table.Value[0];
                }
                if (reference != null && !keyed.ContainsKey(reference))
                {
                    keyed[reference] = table.Key;
                    keyColumn[reference] = table.Value[0];
                }
            }
            // Rows to scan: the car's, then whatever gets copied.
            List<string[]> pending = new List<string[]>();
            foreach (KeyValuePair<string, List<string>> table in to)
            {
                foreach (string column in new string[] { "Ordinal", "CarId", "CarID" })
                {
                    if (Has(table.Value, column))
                    {
                        pending.Add(new string[] { table.Key, SqliteDatabase.Quote(column) + " = " + this.carId });
                        break;
                    }
                }
                if (table.Key.StartsWith("List_UpgradeCarBody", StringComparison.Ordinal) && table.Value.Count > 0 && table.Value[0] == "Id")
                {
                    pending.Add(new string[] { table.Key, "Id / 1000 = " + this.carId });
                }
                if (table.Key == "Data_Car")
                {
                    pending.Add(new string[] { table.Key, "Id = " + this.carId });
                }
            }
            foreach (string column in new string[] { "EngineID", "DrivetrainID" })
            {
                foreach (KeyValuePair<string, List<string>> table in to)
                {
                    if (Has(table.Value, column))
                    {
                        pending.Add(new string[] { table.Key, SqliteDatabase.Quote(column) + " IN (SELECT " + column + " FROM " + ((column == "EngineID") ? "List_UpgradeEngine" : "List_UpgradeDrivetrain") + " WHERE Ordinal = " + this.carId + ")" });
                    }
                }
            }
            int copied = 0;
            for (int i = 0; i < pending.Count && i < 5000; i++)
            {
                SqliteDatabase.Result rows = this.target.Query("SELECT * FROM " + SqliteDatabase.Quote(pending[i][0]) + " WHERE " + pending[i][1]);
                for (int c = 0; c < rows.Columns.Length; c++)
                {
                    string column = rows.Columns[c];
                    string table;
                    if (!keyed.TryGetValue(column, out table) || table == pending[i][0])
                    {
                        continue;
                    }
                    foreach (object[] row in rows.Rows)
                    {
                        if (row[c] == null || !(row[c] is long) || (long) row[c] < 0)
                        {
                            continue;
                        }
                        string where = SqliteDatabase.Quote(keyColumn[column]) + " = " + (long) row[c];
                        if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM " + SqliteDatabase.Quote(table) + " WHERE " + where)) > 0)
                        {
                            continue;
                        }
                        int added = this.CopyRows(table, from[table], to[table], where);
                        if (added > 0)
                        {
                            copied += added;
                            this.Line("  {0}: {1} rows ({2}, referenced by {3})", table, added, where.Replace("[", "").Replace("]", ""), pending[i][0]);
                            pending.Add(new string[] { table, where });
                        }
                    }
                }
            }
            this.Line("references: {0} more rows copied", copied);
        }

        // Column names are compared as SQLite does, ignoring case (List_UpgradeDrivetrainDriveline has DrivetrainId).
        private static bool Has(List<string> columns, string name)
        {
            return columns.Exists(delegate(string c) { return string.Equals(c, name, StringComparison.OrdinalIgnoreCase); });
        }

        private List<long> Ids(string sql)
        {
            List<long> ids = new List<long>();
            foreach (object[] row in this.source.Query(sql, this.carId).Rows)
            {
                if (row[0] != null)
                {
                    ids.Add(Convert.ToInt64(row[0]));
                }
            }
            return ids;
        }

        // Rows whose 'column' is one of the ids, in every table having that column (or only in
        // 'onlyTable'), when FM4 does not have them yet.
        private void CopyById(Dictionary<string, List<string>> from, Dictionary<string, List<string>> to, string column, List<long> ids, string skipTable, params string[] onlyTable)
        {
            if (ids.Count == 0)
            {
                return;
            }
            List<string> list = new List<string>();
            foreach (long id in ids)
            {
                list.Add(id.ToString(CultureInfo.InvariantCulture));
            }
            string where = SqliteDatabase.Quote(column) + " IN (" + string.Join(",", list.ToArray()) + ")";
            foreach (KeyValuePair<string, List<string>> table in to)
            {
                List<string> sourceColumns;
                if (table.Key == skipTable || !from.TryGetValue(table.Key, out sourceColumns) || !Has(table.Value, column) || !Has(sourceColumns, column))
                {
                    continue;
                }
                if (onlyTable.Length > 0 && Array.IndexOf(onlyTable, table.Key) < 0)
                {
                    continue;
                }
                int copied = this.CopyRows(table.Key, sourceColumns, table.Value, where);
                if (copied > 0)
                {
                    this.Line("  {0}: {1} rows ({2} {3})", table.Key, copied, column, string.Join(",", list.ToArray()));
                }
            }
        }

        private void CopyTorqueCurves(Dictionary<string, List<string>> from, Dictionary<string, List<string>> to, List<long> engines)
        {
            List<long> curves = new List<long>();
            foreach (KeyValuePair<string, List<string>> table in from)
            {
                if (!table.Value.Contains("EngineID") || engines.Count == 0)
                {
                    continue;
                }
                foreach (string column in table.Value)
                {
                    if (column.IndexOf("TorqueCurve", StringComparison.OrdinalIgnoreCase) < 0 || !column.EndsWith("ID", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    foreach (long engine in engines)
                    {
                        foreach (object[] row in this.source.Query("SELECT " + SqliteDatabase.Quote(column) + " FROM " + SqliteDatabase.Quote(table.Key) + " WHERE EngineID = ?", engine).Rows)
                        {
                            if (row[0] != null && !curves.Contains(Convert.ToInt64(row[0])))
                            {
                                curves.Add(Convert.ToInt64(row[0]));
                            }
                        }
                    }
                }
            }
            this.CopyById(from, to, "TorqueCurveID", curves, null, "List_TorqueCurve");
        }

        // Copies the rows matching 'where' (common columns); rows already there are replaced.
        // A row whose primary key FM4 already uses for another row: a key nothing refers to (a plain
        // counter, Combo_Colors_Exceptions.ID: FH2's 670 is car 2016's, FM4's car 315's) gets the next
        // free value; a key other tables refer to stops the port before anything is written.
        private int CopyRows(string table, List<string> sourceColumns, List<string> targetColumns, string where)
        {
            List<string> columns = new List<string>();
            foreach (string column in targetColumns)
            {
                if (Has(sourceColumns, column))
                {
                    columns.Add(column);
                }
            }
            if (columns.Count == 0)
            {
                return 0;
            }
            List<string> quoted = columns.ConvertAll<string>(delegate(string c) { return SqliteDatabase.Quote(c); });
            SqliteDatabase.Result rows = this.source.Query("SELECT " + string.Join(",", quoted.ToArray()) + " FROM " + SqliteDatabase.Quote(table) + " WHERE " + where);
            if (rows.Rows.Count == 0)
            {
                return 0;
            }
            this.writtenRows.Add(new string[] { table, where });
            this.target.Execute("DELETE FROM " + SqliteDatabase.Quote(table) + " WHERE " + where);
            string marks = string.Join(",", columns.ConvertAll<string>(delegate(string c) { return "?"; }).ToArray());
            string insert = "INSERT INTO " + SqliteDatabase.Quote(table) + " (" + string.Join(",", quoted.ToArray()) + ") VALUES (" + marks + ")";
            List<int> key = new List<int>();
            foreach (string column in this.PrimaryKey(table))
            {
                int index = columns.FindIndex(delegate(string c) { return string.Equals(c, column, StringComparison.OrdinalIgnoreCase); });
                if (index < 0)
                {
                    key.Clear();
                    break;
                }
                key.Add(index);
            }
            string keyWhere = string.Join(" AND ", key.ConvertAll<string>(delegate(int i) { return quoted[i] + " = ?"; }).ToArray());
            int renumbered = 0;
            foreach (object[] row in rows.Rows)
            {
                if (key.Count > 0)
                {
                    object[] values = key.ConvertAll<object>(delegate(int i) { return row[i]; }).ToArray();
                    if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM " + SqliteDatabase.Quote(table) + " WHERE " + keyWhere, values)) > 0)
                    {
                        if (key.Count != 1 || !(row[key[0]] is long) || this.IsReferenced(table, columns[key[0]]))
                        {
                            throw new InvalidOperationException(string.Format("{0}: the key {1} = {2} of the " + this.game + " row is another row's in FM4, and other tables refer to it.",
                                table, string.Join(",", key.ConvertAll<string>(delegate(int i) { return columns[i]; }).ToArray()),
                                string.Join(",", Array.ConvertAll<object, string>(values, delegate(object v) { return Convert.ToString(v, CultureInfo.InvariantCulture); }))));
                        }
                        row[key[0]] = Convert.ToInt64(this.target.Scalar("SELECT ifnull(max(" + quoted[key[0]] + "), 0) + 1 FROM " + SqliteDatabase.Quote(table)));
                        renumbered++;
                    }
                }
                this.target.Execute(insert, row);
            }
            if (renumbered > 0)
            {
                this.Line("  {0}: {1} of {2} rows got a new {3} (FM4 uses theirs)", table, renumbered, rows.Rows.Count, columns[key[0]]);
            }
            return rows.Rows.Count;
        }

        private readonly Dictionary<string, List<string>> primaryKeys = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // The target table's primary key columns, in key order (PRAGMA table_info pk).
        private List<string> PrimaryKey(string table)
        {
            List<string> key;
            if (!this.primaryKeys.TryGetValue(table, out key))
            {
                SortedList<long, string> ordered = new SortedList<long, string>();
                foreach (object[] column in this.target.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")").Rows)
                {
                    if (Convert.ToInt64(column[5]) > 0)
                    {
                        ordered.Add(Convert.ToInt64(column[5]), (string) column[1]);
                    }
                }
                key = new List<string>(ordered.Values);
                this.primaryKeys[table] = key;
            }
            return key;
        }

        // Whether another FM4 table has a column naming this key, as CopyReferences reads them:
        // the key's own name (AeroPhysicsID), or for a key called Id/ID the table's name without
        // its prefix + ID (Data_CarBody -> CarBodyID).
        private bool IsReferenced(string table, string keyColumn)
        {
            List<string> names = new List<string>();
            if (!string.Equals(keyColumn, "Id", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(keyColumn);
            }
            int prefix = table.IndexOf('_');
            names.Add(((prefix >= 0) ? table.Substring(prefix + 1) : table) + "ID");
            foreach (object[] other in this.target.Query("SELECT name FROM sqlite_master WHERE type = 'table'").Rows)
            {
                if (string.Equals((string) other[0], table, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (object[] column in this.target.Query("PRAGMA table_info(" + SqliteDatabase.Quote((string) other[0]) + ")").Rows)
                {
                    foreach (string name in names)
                    {
                        if (string.Equals((string) column[1], name, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // Data_Car: the FH2 row in FM4's columns; PI on the FM4 scale (MapPerformanceIndex), class
        // from FM4's PI ranges, base-game content.
        private void PortCarRow(List<string> sourceColumns, List<string> targetColumns)
        {
            this.CopyRows("Data_Car", sourceColumns, targetColumns, "Id = " + this.carId);
            double pi = Convert.ToDouble(this.source.Scalar("SELECT PerformanceIndex FROM Data_Car WHERE Id = ?", this.carId), CultureInfo.InvariantCulture);
            double fm4Pi = MapPerformanceIndex(PerformanceIndexPairs(this.source, this.target, this.carId, this.SourceRoot, this.TargetRoot, this.OutputRoot), pi);
            object classId = ClassFor(this.target, fm4Pi, this.carId);
            this.target.Execute("UPDATE Data_Car SET PerformanceIndex = ?, ClassID = ?, ContentId = 0, IsArcade = ?, IsPurchased = 1, IsInstalled = 1 WHERE Id = ?",
                fm4Pi, (classId != null) ? Convert.ToInt64(classId) : 9L, this.Arcade ? 1L : 0L, this.carId);
            this.Line("Data_Car {0}: PI {1:0.000} (" + this.game + ") -> {2:0.000} (FM4), class {3}, IsArcade {4}", this.carId, pi, fm4Pi, ClassName(this.target, classId), this.Arcade ? 1 : 0);
        }

        // (FH2 PI, FM4 PI) of the cars both games have (same MediaName), sorted; the car being
        // ported and the cars ported earlier (their FM4 PI came from this mapping) left out.
        public static List<double[]> PerformanceIndexPairs(SqliteDatabase source, SqliteDatabase target, long exclude, string sourceRoot, string targetRoot)
        {
            return PerformanceIndexPairs(source, target, exclude, sourceRoot, targetRoot, null);
        }

        // outputRoot: a trial folder whose cars (earlier cars of the same DLC pack) are ported ones too.
        public static List<double[]> PerformanceIndexPairs(SqliteDatabase source, SqliteDatabase target, long exclude, string sourceRoot, string targetRoot, string outputRoot)
        {
            Dictionary<string, double> horizon = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (object[] row in source.Query("SELECT MediaName, PerformanceIndex FROM Data_Car").Rows)
            {
                if (row[0] is string && row[1] != null)
                {
                    horizon[(string) row[0]] = Convert.ToDouble(row[1], CultureInfo.InvariantCulture);
                }
            }
            List<double[]> pairs = new List<double[]>();
            foreach (object[] row in target.Query("SELECT Id, MediaName, PerformanceIndex FROM Data_Car").Rows)
            {
                double pi;
                if (Convert.ToInt64(row[0]) != exclude && row[1] is string && row[2] != null && horizon.TryGetValue((string) row[1], out pi)
                    && !IsPorted(sourceRoot, targetRoot, (string) row[1]) && (outputRoot == null || !IsPorted(sourceRoot, outputRoot, (string) row[1])))
                {
                    pairs.Add(new double[] { pi, Convert.ToDouble(row[2], CultureInfo.InvariantCulture) });
                }
            }
            pairs.Sort(delegate(double[] a, double[] b) { return a[0].CompareTo(b[0]); });
            return pairs;
        }

        // FM4 PI for an FH2 PI: a line through the 15 shared cars nearest in FH2 PI. On the 138 cars
        // both games have (leave one out) the error is 0.013, against 0.027 for the single line
        // FM4 = 1.025 FH2 - 0.261 fit on PI > 0.7, which puts slow cars a class too high
        // (Ford Ka 0.331: 0.078 for 0.001). Outside the shared cars' range (above the MC12's 0.851,
        // where FH2's hypercars are) it goes on from the edge with that line's slope: the edge fit
        // is steeper and would push road cars into R1.
        public static double MapPerformanceIndex(List<double[]> pairs, double pi)
        {
            const int Near = 15;
            const double Slope = 1.025;
            if (pairs.Count < Near)
            {
                return Math.Max(0.001, Math.Min(0.999, (Slope * pi) - 0.261));
            }
            double inside = Math.Max(pairs[0][0], Math.Min(pairs[pairs.Count - 1][0], pi));
            List<double[]> near = new List<double[]>(pairs);
            near.Sort(delegate(double[] a, double[] b) { return Math.Abs(a[0] - inside).CompareTo(Math.Abs(b[0] - inside)); });
            near.RemoveRange(Near, near.Count - Near);
            double mx = 0;
            double my = 0;
            foreach (double[] p in near)
            {
                mx += p[0] / Near;
                my += p[1] / Near;
            }
            double sxx = 0;
            double sxy = 0;
            foreach (double[] p in near)
            {
                sxx += (p[0] - mx) * (p[0] - mx);
                sxy += (p[0] - mx) * (p[1] - my);
            }
            double slope = (sxx > 1e-12) ? sxy / sxx : Slope;
            return Math.Max(0.001, Math.Min(0.999, my + (slope * (inside - mx)) + (Slope * (pi - inside))));
        }

        // The FM4 class of a PI: the first in CarClasses whose MaxPerformanceIndex reaches it (F 0.157,
        // E 0.2224 ... R1 0.938, U 1); without that table, the class of the car with the highest PI
        // not above it.
        public static object ClassFor(SqliteDatabase target, double pi, long exclude)
        {
            if (Convert.ToInt64(target.Scalar("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'CarClasses'")) == 1)
            {
                object id = target.Scalar("SELECT Id FROM CarClasses WHERE MaxPerformanceIndex >= ? ORDER BY MaxPerformanceIndex LIMIT 1", pi);
                if (id != null)
                {
                    return id;
                }
            }
            return target.Scalar("SELECT ClassID FROM Data_Car WHERE Id <> ? AND PerformanceIndex <= ? GROUP BY ClassID ORDER BY max(PerformanceIndex) DESC LIMIT 1", exclude, pi);
        }

        // "R2" for class 8 (CarClasses.BadgeTexturePathPrefix CLASS_R2), the number otherwise.
        public static string ClassName(SqliteDatabase target, object classId)
        {
            if (classId == null)
            {
                return "?";
            }
            try
            {
                string prefix = target.Scalar("SELECT BadgeTexturePathPrefix FROM CarClasses WHERE Id = ?", classId) as string;
                if (prefix != null && prefix.StartsWith("CLASS_", StringComparison.OrdinalIgnoreCase))
                {
                    return prefix.Substring(6);
                }
            }
            catch (InvalidOperationException)
            {
            }
            return Convert.ToString(classId, CultureInfo.InvariantCulture);
        }

        // True when the target's car zip is the source's, ported earlier: both carry the same
        // buildnumber.txt (FM4's own cars have Turn 10 builds, e.g. 452838.FER_F40_87.RTMGSLTURNTEN27
        // against FH2's 214061.FER_F40_87.PG-LEAM-BS09).
        public static bool IsPorted(string sourceRoot, string targetRoot, string mediaName)
        {
            try
            {
                string from = Path.Combine(Path.Combine(Path.Combine(sourceRoot, "Media"), "cars"), mediaName + ".zip");
                string to = Path.Combine(Path.Combine(Path.Combine(targetRoot, "Media"), "cars"), mediaName + ".zip");
                if (!File.Exists(from) || !File.Exists(to))
                {
                    return false;
                }
                ForzaZip a = new ForzaZip(from);
                ForzaZip b = new ForzaZip(to);
                ForzaZip.Entry x = a.Find("buildnumber.txt");
                ForzaZip.Entry y = b.Find("buildnumber.txt");
                if (x == null)
                {
                    // FM3 car zips have no build number: a ported car carries the port's log.
                    return b.Find("port_log.txt") != null;
                }
                return x != null && y != null && Encoding.ASCII.GetString(a.Read(x)).Trim() == Encoding.ASCII.GetString(b.Read(y)).Trim();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static long ToLong(object value, string error)
        {
            if (value == null)
            {
                throw new InvalidOperationException(error);
            }
            return Convert.ToInt64(value);
        }

        #endregion

        #region Strings

        // DisplayName / ModelShort are "_&<id>": the low 16 bits are the key in Data_Car.str. FH2 and
        // FM4 numbered their strings apart, so an FH2 key can be another car's in FM4 (14 of the 73
        // FH2 cars FM4 lacks: FOR_TransitSSV_11's DDD8 is IDS_DisplayName_1076 there). Such a string
        // gets a key free in every language, and the database value follows it (same high bits); a
        // car ported before finds its string again by name (IDS_...).
        private void PortStrings()
        {
            this.PortStrings("Data_Car", "Id", this.carId, "DisplayName", "ModelShort");
            if (this.newMake.HasValue)
            {
                this.PortStrings("List_CarMake", "ID", this.newMake.Value, "DisplayName", "Profile");
            }
        }

        // The strings of one row (table.keyColumn = id) into <table>.str of every FM4 language; a
        // language the source lacks gets the source's EN text (FM3 has no BR, GB, NL).
        private void PortStrings(string table, string keyColumn, long id, params string[] columns)
        {
            string targetTables = Path.Combine(Path.Combine(this.TargetRoot, "Media"), "StringTables");
            string sourceTables = Path.Combine(Path.Combine(this.SourceRoot, "Media"), "StringTables");
            string strName = table + ".str";
            List<string> zips = new List<string>();
            Dictionary<string, StrFile> sourceFiles = new Dictionary<string, StrFile>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, StrFile> targetFiles = new Dictionary<string, StrFile>(StringComparer.OrdinalIgnoreCase);
            foreach (string targetZip in Directory.GetFiles(targetTables, "*.zip"))
            {
                string sourceZip = Path.Combine(sourceTables, Path.GetFileName(targetZip));
                if (!File.Exists(sourceZip))
                {
                    sourceZip = Path.Combine(sourceTables, "EN.zip");
                }
                ForzaZip sourceArchive = new ForzaZip(sourceZip);
                ForzaZip.Entry sourceEntry = sourceArchive.Find(strName);
                ForzaZip archive = this.OpenChanged(targetZip);
                ForzaZip.Entry entry = archive.Find(strName);
                if (sourceEntry == null || entry == null)
                {
                    continue;
                }
                zips.Add(targetZip);
                sourceFiles[targetZip] = new StrFile(sourceArchive.Read(sourceEntry));
                targetFiles[targetZip] = new StrFile(archive.Read(entry));
            }

            int keys = 0;
            string rowWhere = SqliteDatabase.Quote(keyColumn) + " = ?";
            foreach (string column in columns)
            {
                object value = this.source.Scalar("SELECT " + column + " FROM " + table + " WHERE " + rowWhere, id);
                uint stringId;
                if (!(value is string) || !((string) value).StartsWith("_&") || !uint.TryParse(((string) value).Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out stringId))
                {
                    continue;
                }
                int key = (int) (stringId & 0xFFFF);
                string name = null;
                foreach (string zip in zips)
                {
                    StrFile.Entry text = Find(sourceFiles[zip], key);
                    if (text != null && text.Name != null)
                    {
                        name = text.Name;
                        break;
                    }
                }
                int newKey = this.StringKey(targetFiles, key, name);
                if (newKey != key)
                {
                    string newValue = "_&" + ((stringId & 0xFFFF0000u) | (uint) newKey).ToString(CultureInfo.InvariantCulture);
                    this.target.Execute("UPDATE " + table + " SET " + SqliteDatabase.Quote(column) + " = ? WHERE " + rowWhere, newValue, id);
                    if (table == "List_CarMake" && column == "DisplayName")
                    {
                        // Data_Car.MakeName is the make's own string.
                        this.target.Execute("UPDATE Data_Car SET MakeName = ? WHERE MakeID = ?", newValue, id);
                    }
                    this.Line("  {0}.{1}: string key {2:X4} is another row's in FM4, {3:X4} used ({4})", table, column, key, newKey, newValue);
                }
                foreach (string zip in zips)
                {
                    StrFile.Entry text = Find(sourceFiles[zip], key);
                    if (text != null)
                    {
                        targetFiles[zip].Set(newKey, text.Text, text.Name);
                    }
                }
                keys++;
            }
            foreach (string zip in zips)
            {
                ForzaZip archive = this.OpenChanged(zip);
                archive.Replace(archive.Find(strName), targetFiles[zip].ToBytes());
            }
            this.Line("strings: {0} keys in {1} of {2} languages", keys, strName, zips.Count);
        }

        private static StrFile.Entry Find(StrFile file, int key)
        {
            foreach (StrFile.Entry entry in file.Entries)
            {
                if (entry.Key == key)
                {
                    return entry;
                }
            }
            return null;
        }

        // The FM4 key for an FH2 string: its own when free in every language or already holding this
        // string (same name), the key a port before gave the name, else the highest key free everywhere
        // (FFFF ends the key list).
        private int StringKey(Dictionary<string, StrFile> files, int key, string name)
        {
            bool free = true;
            foreach (StrFile file in files.Values)
            {
                StrFile.Entry existing = Find(file, key);
                if (existing != null && (name == null || existing.Name != name))
                {
                    free = false;
                }
            }
            if (free)
            {
                return key;
            }
            if (name != null)
            {
                foreach (StrFile file in files.Values)
                {
                    foreach (StrFile.Entry entry in file.Entries)
                    {
                        if (entry.Name == name)
                        {
                            return entry.Key;
                        }
                    }
                }
            }
            for (int candidate = 0xFFFE; candidate > 0; candidate--)
            {
                bool used = false;
                foreach (StrFile file in files.Values)
                {
                    used = used || file.Contains(candidate);
                }
                if (!used)
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException("The string table has no free string key.");
        }

        #endregion

        #region Car and wheel zips

        private void BuildCarZip()
        {
            string sourcePath = GamePaths.FindCar(this.SourceRoot, this.MediaName);
            string donorPath = GamePaths.FindCar(this.TargetRoot, this.DonorMediaName);
            if (sourcePath == null || donorPath == null || !sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Car zips not found (" + this.MediaName + " in " + this.game + ", " + this.DonorMediaName + " in FM4).");
            }
            ForzaZip from = new ForzaZip(sourcePath);
            ForzaZip donor = new ForzaZip(donorPath);
            ForzaZip car = ForzaZip.CreateLike(donor);
            this.donorFormat.Add("car zip: entry headers (zip versions, extra field, compression) of " + Path.GetFileName(donorPath));
            this.donorFormat.Add("zipmanifest.xml: the car zip's line modelled on the donor's");
            string prefix = this.MediaName.ToLowerInvariant();
            string donorPrefix = this.DonorMediaName.ToLowerInvariant();
            int models = 0;
            List<string> materials = new List<string>();
            byte[] shaderSettings = null;
            // Granny animations: FM4 reads little-endian .gr2 files, FH2 writes big-endian ones (file
            // version 7 at 0x20 as 00000007, measured on the MP4-12C both games have). Without a
            // converter the car goes without them, as FM4 cars without moving parts do.
            bool animations = true;
            int grannyFiles = 0;
            foreach (ForzaZip.Entry entry in from.Entries)
            {
                if (entry.Name.EndsWith(".gr2", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] granny = from.Read(entry);
                    animations &= granny.Length > 0x24 && granny[0x20] == 7 && granny[0x23] == 0;
                    grannyFiles++;
                }
            }
            if (grannyFiles == 0)
            {
                animations = false;
                this.Line("  animations: none in " + this.game + " (no .gr2 files), the donor's left out");
            }
            else if (!animations)
            {
                this.Line("  animations: " + this.game + " .gr2 files are big-endian, left out (no moving parts)");
            }
            foreach (ForzaZip.Entry entry in from.Entries)
            {
                string name = entry.Name.ToLowerInvariant();
                if (name.StartsWith("stripped_") || name == "physicsdefinition.bin" || name == "manufacturercolors.xml")
                {
                    continue;   // FH2 only
                }
                if (!animations && (name.EndsWith(".gr2") || name == "caranimationevents.xml"))
                {
                    continue;
                }
                byte[] data = from.Read(entry);
                if (name.EndsWith(".fbf"))
                {
                    int layers;
                    data = ConvertAnarkLayout(data, out layers);
                    this.Line("  dash {0}: {1} text layers to the FM4 layout", Path.GetFileName(name), layers);
                }
                if (name.EndsWith(".carbin"))
                {
                    if ((name.Contains("_caliper") || name.Contains("_rotor")) && !name.EndsWith("_lod0.carbin") && this.motorsport3)
                    {
                        name = name.Substring(0, name.Length - ".carbin".Length) + "_lod0.carbin";   // FM3 caliperlf.carbin
                    }
                    if (name.Contains("_caliper") || name.Contains("_rotor"))
                    {
                        // Brakes (type 1): converted; the donor's, renamed, if that fails.
                        try
                        {
                            data = ConvertBrake(data, Path.GetFileName(name), this.Report);
                        }
                        catch (Exception ex)
                        {
                            ForzaZip.Entry brake = donor.Find(donorPrefix + name.Substring(prefix.Length));
                            this.Line("  brake {0}: {1}; {2}", name, ex.Message, (brake != null) ? "the donor's is used" : "left out");
                            if (brake == null)
                            {
                                continue;
                            }
                            data = donor.Read(brake);
                            this.donorContent.Add("brake " + name + ": the donor's model " + brake.Name + " (conversion failed: " + ex.Message + ")");
                        }
                    }
                    else
                    {
                        data = ConvertModel(data, name.EndsWith("_lod0.carbin"), this.Report, materials, this.MaterialFixes(), this.unknownMaterials);
                        models++;
                    }
                }
                else if (name == "carattribs.xml")
                {
                    List<string> kept = new List<string>();
                    data = ConvertCarAttributes(Encoding.UTF8.GetString(data), Encoding.UTF8.GetString(donor.Read(donor.Find("carattribs.xml"))), animations, this.Report, kept);
                    foreach (string element in kept)
                    {
                        this.donorContent.Add("carattribs.xml " + element);
                    }
                }
                else if (name == "shadersettings.xml")
                {
                    shaderSettings = data;   // needs the materials of every model
                    continue;
                }
                else if (name.EndsWith(".xds") && this.motorsport3)
                {
                    data = ConvertMotorsport3Texture(data, name);
                }
                car.Add(name, data);
            }
            if (shaderSettings != null)
            {
                car.Add("shadersettings.xml", ConvertShaderSettings(Encoding.UTF8.GetString(shaderSettings), materials, this.Report));
            }
            if (this.motorsport3 && !string.IsNullOrEmpty(this.motorsport3Dash))
            {
                this.AddMotorsport3Dash(car);
            }
            ForzaZip.Entry version = donor.Find("versiondata.xml");
            if (version != null && car.Find("versiondata.xml") == null)
            {
                car.Add("versiondata.xml", donor.Read(version));
                this.donorContent.Add("car zip versiondata.xml: the donor's (" + this.game + " has none)");
            }
            string path = Path.Combine(Path.Combine(Path.Combine(this.TargetRoot, "Media"), "cars"), this.MediaName + ".zip");
            // Saved with the other files at the end, with the report as port_log.txt.
            this.carZip = car;
            this.carZipPath = path;
            this.Line("car zip: {0} entries + port_log.txt, {1} models converted -> {2}", car.Entries.Count, models, this.Relative(path));
        }

        // Anark digital dash layout (.fbf), FH2 version 0x3F0 -> FM4 0x3EF. A text layer is
        //   u32 length + name, 6 bytes, u16 length + default text, 14 bytes, u16 length + font, 33 bytes,
        //   [FH2 only: a float], ...
        // FM4 keeps the default text empty and has no float. The FM4 game reads an FH2 file wrongly past the
        // first text layer (the P1 crashed while loading the race: a 5014-byte copy over its stack).
        // On the 38 dashes both games have, 29 come out byte for byte as FM4's. The others differ in content FH2
        // changed (fonts horizon_dg*, a few values). .bgf/.bsg are the same in both games.
        // Rewritten on AnarkFbf (2026-10-15): the old byte-pattern version assumed an empty tag and an
        // untagged text layer, and broke 9 FH and 6 FH2 dashes (tag "NLOC", 458 Spider, V12 Zagato, RS5...),
        // which FM4 would then read as garbage. Each text record keeps its tag; the 4 bytes dropped are
        // tail bytes 33..36 (the old converter's choice, byte-identical to FM4 on 29 of 38 shared dashes).
        public static byte[] ConvertAnarkLayout(byte[] fh2, out int layers)
        {
            layers = 0;
            if (fh2.Length < 4 || AnarkFbf.ReadU32(fh2, 0) != AnarkFbf.VersionFH2)
            {
                return fh2;
            }
            AnarkFbf fbf = AnarkFbf.Read(fh2);
            fbf.Version = AnarkFbf.VersionFM4;
            foreach (AnarkFbf.Record record in fbf.Records)
            {
                if (record.Type != AnarkFbf.RecordType.Text)
                {
                    continue;
                }
                byte[] tail = new byte[34];
                Array.Copy(record.TextTail, 0, tail, 0, 33);
                tail[33] = record.TextTail[37];
                record.TextTail = tail;
                record.Text = "";
                layers++;
            }
            return fbf.Write();
        }

        // The FM3 car's digital dash into the car zip, as FM4 keeps it: digitalgauge/dash_<car>.fbf/.bgf/.bsg
        // and the textures it shows as digitalgauge/<name>.xds (from FM3's Media\UI\Textures.zip, colour
        // textures to FM4's gamma form). A gauge zip can hold more than one dash (Dash_AUD_8_R10_06 has 3):
        // the one named after the car, else after the zip, else the only one.
        private void AddMotorsport3Dash(ForzaZip car)
        {
            string zipPath = Path.Combine(Path.Combine(Path.Combine(this.SourceRoot, "Media"), "DigitalGauges"), this.motorsport3Dash + ".zip");
            if (!File.Exists(zipPath))
            {
                this.Line("  WARNING digital dash: {0} not found, the car goes without it", this.Relative(zipPath));
                return;
            }
            ForzaZip gauges = new ForzaZip(zipPath);
            List<string> stems = new List<string>();
            foreach (ForzaZip.Entry entry in gauges.Entries)
            {
                if (entry.Name.EndsWith(".fbf", StringComparison.OrdinalIgnoreCase))
                {
                    stems.Add(entry.Name.Substring(0, entry.Name.Length - 4));
                }
            }
            string stem = null;
            foreach (string wanted in new string[] { "dash_" + this.MediaName, this.motorsport3Dash })
            {
                foreach (string candidate in stems)
                {
                    if (stem == null && string.Equals(Path.GetFileName(candidate), wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        stem = candidate;
                    }
                }
            }
            if (stem == null && stems.Count == 1)
            {
                stem = stems[0];
            }
            ForzaZip.Entry bgf = (stem != null) ? gauges.Find(stem + ".bgf") : null;
            ForzaZip.Entry bsg = (stem != null) ? gauges.Find(stem + ".bsg") : null;
            if (bgf == null || bsg == null)
            {
                this.Line("  WARNING digital dash: no complete dash in {0} ({1} .fbf files), the car goes without it", Path.GetFileName(zipPath), stems.Count);
                return;
            }
            string texturesPath = Path.Combine(Path.Combine(Path.Combine(this.SourceRoot, "Media"), "UI"), "Textures.zip");
            ForzaZip textures = File.Exists(texturesPath) ? new ForzaZip(texturesPath) : null;
            Dictionary<string, byte[]> images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            List<string> missing = new List<string>();
            byte[] fbf = ConvertMotorsport3Dash(gauges.Read(gauges.Find(stem + ".fbf")), this.MediaName, delegate(string path)
            {
                // GAME:\MEDIA\UI\TEXTURES\<rest>.TGA -> Textures.zip <rest>.xds
                const string ui = @"GAME:\MEDIA\UI\TEXTURES\";
                if (!path.StartsWith(ui, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                string rest = path.Substring(ui.Length).Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(rest);
                if (images.ContainsKey(name))
                {
                    return true;
                }
                ForzaZip.Entry texture = (textures != null) ? textures.Find(Path.ChangeExtension(rest, ".xds")) : null;
                if (texture == null)
                {
                    missing.Add(path);
                    return false;
                }
                images[name] = ConvertMotorsport3Texture(textures.Read(texture), texture.Name);
                return true;
            });
            string prefix = "digitalgauge/dash_" + this.MediaName.ToLowerInvariant();
            car.Add(prefix + ".fbf", fbf);
            car.Add(prefix + ".bgf", gauges.Read(bgf));
            car.Add(prefix + ".bsg", gauges.Read(bsg));
            foreach (KeyValuePair<string, byte[]> image in images)
            {
                car.Add("digitalgauge/" + image.Key.ToLowerInvariant() + ".xds", image.Value);
            }
            this.Line("  digital dash: {0}/{1} -> {2}.fbf/.bgf/.bsg, {3} textures from Media\\UI\\Textures.zip", Path.GetFileName(zipPath), Path.GetFileName(stem), prefix, images.Count);
            if (missing.Count > 0)
            {
                this.Line("  WARNING digital dash: {0} images not found, left pointing at FM3's path: {1}", missing.Count, string.Join(", ", missing.ToArray()));
            }
        }

        // FM3 dash scene (.fbf 0x3EE) -> FM4 (0x3EF): text tails get FM4's 34th byte (0 on every FM4
        // file), the default text is emptied as in FM4's files (the .bgf holds it), and every image the
        // takeTexture callback accepts points at the car's own digitalgauge folder,
        // GAME:\MEDIA\CARS\<CAR>\DIGITALGAUGE\<NAME>.TGA. .bgf and .bsg are the same format in both
        // games. Checked on the 59 dashes both games have: everything else in the records as FM4's
        // (they differ only where FM4 re-authored the dash or moved its textures).
        public static byte[] ConvertMotorsport3Dash(byte[] data, string mediaName, Predicate<string> takeTexture)
        {
            AnarkFbf fbf = AnarkFbf.Read(data);
            if (!fbf.IsFM3)
            {
                return data;
            }
            fbf.Version = AnarkFbf.VersionFM4;
            foreach (AnarkFbf.Record record in fbf.Records)
            {
                if (record.Type == AnarkFbf.RecordType.Text)
                {
                    byte[] tail = new byte[34];
                    Array.Copy(record.TextTail, 0, tail, 0, 33);
                    record.TextTail = tail;
                    record.Text = "";
                }
                else if (record.Type == AnarkFbf.RecordType.Image && takeTexture != null && takeTexture(record.Path))
                {
                    record.Path = @"GAME:\MEDIA\CARS\" + mediaName.ToUpperInvariant() + @"\DIGITALGAUGE\" + Path.GetFileNameWithoutExtension(record.Path.Replace('\\', '/')).ToUpperInvariant() + ".TGA";
                }
            }
            return fbf.Write();
        }

        // One FH2 model file in FM4 form: converted, FH2-only materials renamed (lod0 lights to the
        // _lod0 variants FM4 uses there), the 'deprecated' material removed.
        // Calipers and rotors (carbin type 1): the same section layout as the car models, a fixed
        // 0x354-byte header whose only FH2/FM4 difference is the format word. 1838 FH2 files convert
        // and re-read as FM4; of the 870 both games have, 630 come out as FM4's but for the vertex
        // tails (SNORM8 -> SNORM16 rounding), the others differ in content FH2 changed (renamed
        // materials such as rotor -> brake, which FM4 also uses, and remodelled parts).
        // Brake materials are not in ShaderSettings.xml; a name no FM4 brake uses is reported.
        public static byte[] ConvertBrake(byte[] data, string name, StringBuilder report)
        {
            CarbinModel model = new CarbinModel(data, true);
            if (model.Type != 1)
            {
                throw new NotSupportedException("not a type 1 carbin (type " + model.Type + ")");
            }
            ConvertToMotorsport4(model);
            List<string> unknown = new List<string>();
            foreach (CarbinModel.Section section in model.Sections)
            {
                foreach (CarbinModel.SubSection sub in section.SubSections)
                {
                    if (Array.IndexOf(BrakeMaterials, sub.Name) < 0 && !unknown.Contains(sub.Name))
                    {
                        unknown.Add(sub.Name);
                    }
                }
            }
            if (unknown.Count > 0 && report != null)
            {
                report.AppendFormat("  brake {0}: material {1} is not used by any FM4 brake", name, string.Join(", ", unknown.ToArray())).AppendLine();
            }
            return model.ToBytes();
        }

        // An FM3 texture in FM4 form. FM3 stores every texture as linear (fetch dword 0 low byte 0x02)
        // in plain DXT1/DXT5 (0x52/0x54); FM4 marks the colour textures gamma (0xFE: sign x/y/z = 3) in the
        // AS_16 formats (0x73/0x75) and leaves only the normal maps linear (4224 FM3, 11775 FM4 car and
        // wheel textures, 2026-10-16). The pixels are the same: on the Supra (in both games) the
        // re-encoded FM4 blocks decode to FM3's values. Left linear, FM4 skips the gamma decode and the
        // car looks washed out (BMW X5 in game). FM3's DXT1 normal maps (FM4: DXN) are kept as they are.
        public static byte[] ConvertMotorsport3Texture(byte[] data, string name)
        {
            string file = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
            if (data.Length < XdsTexture.HeaderSize || file.Contains("_nrm") || file.Contains("normal"))
            {
                return data;
            }
            byte[] result = (byte[]) data.Clone();
            result[0x1F] = (byte) ((result[0x1F] & 0x03) | 0xFC);   // fetch 0: sign x, y, z = gamma
            int format = result[0x23] & 0x3F;
            if (format == 0x12 || format == 0x14)
            {
                result[0x23] = (byte) ((result[0x23] & 0xC0) | (format + 0x21));   // DXT1/DXT4_5 -> their AS_16 forms
            }
            return result;
        }

        // A source model (read with the FM3 opt-in) in FM4 form: FM3 through its own converter, FH (and
        // FM4 files, which it leaves as they are) through ConvertHorizonToMotorsport4.
        private static void ConvertToMotorsport4(CarbinModel model)
        {
            if (model.IsMotorsport3)
            {
                model.ConvertMotorsport3ToMotorsport4();
            }
            else
            {
                model.ConvertHorizonToMotorsport4();
            }
        }

        // Every material name of the 2664 FM4 type-1 carbins (car zips and Media\brakes).
        private static readonly string[] BrakeMaterials = new string[]
        {
            "back", "badge", "black_noOcclude", "brake", "brake_bump_metal", "brake_bump_metal2", "brake_bump_metal3",
            "brake_bump_metal4", "brake_chrome", "brake_smooth_metal", "caliper", "caliper_", "caliper_noOcclude",
            "caliperLF_LOD0", "caliperLR_LOD0", "caliperRF_LOD0", "caliperRR_LOD0", "chrome", "chrome_2", "chrome_noOcclude",
            "detail_glass_clear", "drum", "drum_", "front", "hub", "hub_", "left_side", "Material__2", "Material__4",
            "Material__5", "matte_colors", "matte_colors_NoOcclude_NoShadow", "matte_colors_NoOcclude_NoShadows", "misc",
            "misc_noOcclude", "misc_NoOcclude_NoShadow", "outer_rim", "right_side", "rotor", "rotor_", "top", "undercarriage",
        };

        // True when some FM4 car uses the material in a model of this carbin type (1 brakes, else models).
        public static bool IsFm4Material(string name, int type)
        {
            return Array.IndexOf((type == 1) ? BrakeMaterials : ModelMaterials, name) >= 0;
        }

        // The FM4 material names offered for a carbin type, sorted, for picking a replacement (models: the
        // common ones, CommonMaterials).
        public static string[] Fm4Materials(int type)
        {
            string[] names = (string[]) ((type == 1) ? BrakeMaterials : CommonMaterials).Clone();
            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            return names;
        }

        // The FM4 name MaterialMap gives an FH2 material, or the FM4 name differing only in case; null if none.
        public static string SuggestMaterial(string name, string fileName)
        {
            string to;
            if (MaterialMap.TryGetValue(name, out to) && to != null)
            {
                return LodVariant(to, fileName);
            }
            foreach (string known in ModelMaterials)
            {
                if (string.Equals(known, name, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }
            return null;
        }

        // The name a light material has in a given model file of an FM4 car: tail_light_lod0 in _lod0 and
        // _cockpit (FM4 cockpits never use tail_light), reverse_light_lod0 in _lod0, the plain name in the main model.
        public static string LodVariant(string material, string fileName)
        {
            string plain = (material == "tail_light_lod0" || material == "reverse_light_lod0") ? material.Substring(0, material.Length - 5) : material;
            bool lod0 = fileName.EndsWith("_lod0.carbin", StringComparison.OrdinalIgnoreCase);
            bool cockpit = fileName.EndsWith("_cockpit.carbin", StringComparison.OrdinalIgnoreCase);
            if ((plain == "tail_light" && (lod0 || cockpit)) || (plain == "reverse_light" && lod0))
            {
                return plain + "_lod0";
            }
            return plain;
        }

        // Every material name of the original FM4 car models (.carbin, _lod0, _cockpit of the 317 car zips of
        // the untouched game, 943 models, 150 names; fm4dev\DEV_DLC\tools\matcount, 2026-10-16). An FH material
        // outside this list (after MaterialMap) has no FM4 shader and renders black. The list measured before
        // (on a game holding ported cars) also had 25 FH names (lights_gls_*, fogred, oldhead...) that were
        // then taken for FM4 ones and never reported.
        private static readonly string[] ModelMaterials = new string[]
        {
            "01___Default", "4_MER_SLR_05_damage", "ADV_RGII_Wheel_LOD0", "badge", "badge_chrome", "badge2", "black",
            "black_glass", "black_glass_breakable", "blur_lip", "blur_rim", "body", "body_2", "body_3", "bottom",
            "brake", "brushed_metal", "bump_leather", "bump_leather2", "bump_leather3", "bump_metal", "bump_metal2",
            "bump_metal3", "bump_metal4", "bump_plastic", "bump_plastic2", "bump_plastic3", "bump_plastic4",
            "bump_plastic5", "bump_stitching", "bump_stitchingA", "bumper_frame", "bumperFa_LOD1", "carbon_fiber",
            "carbon_fiber_no_env", "carbon_fiber2", "carbon_fiber3", "chrome", "chrome_2", "chrome_3",
            "chrome_blur_lip", "chrome_blur_rim", "chrome_bump", "chrome_rim", "chrome2", "cloth", "defrost_lines",
            "detail_glass_amber", "detail_glass_clear", "detail_glass_red", "digital_gauge", "drum", "emblem",
            "embossed_metal", "embossed_plastic", "frame", "gauge_emissive", "gauge_needle", "glass", "grille1",
            "grille1_alpha", "grille2", "grille2_alpha", "grille3", "head_light", "headlight_paint", "headliner",
            "headliner2", "inner_rim", "interior", "leather", "lights_amber", "lights_chrome", "lights_glass",
            "lights_metal", "lights_plastic", "liveryBadge", "Material__0", "Material__1", "Material__11",
            "Material__12", "Material__14", "Material__15", "Material__16", "Material__3", "Material__6",
            "matt_colors", "matt_colors2", "matte_colors", "metal", "metal2", "mirrorLeft", "mirrorMiddle",
            "mirrorRight", "misc", "misc2", "mottled", "mottled2", "outer_rim", "plastic", "Plastic2", "plastic2",
            "race_kit_red", "red_glass", "reflector", "reverse_light", "reverse_light_lod0", "rim", "rim_black",
            "rim_bump_metal", "rim_chrome", "rim_chrome_blur_lip", "rim_inner_rim", "rim_outer_rim",
            "rim_wheel_emblem", "rubber", "rubber_trim", "smooth_metal", "smooth_plastic", "solid", "stitching",
            "stripe_badge", "suede", "tail_light", "tail_light_lod0", "Tail_light_lod0", "tail_light_on",
            "tail_light2", "taillightL_LOD1", "taillightL_LOD5", "taillightR_LOD1", "taillightR_LOD5",
            "textured_reflector", "tiling_cloth", "tiling_cloth2", "tire", "undercarriage", "unicolor_carbon_fiber",
            "weatherstrip", "wheel_black", "wheel_emblem", "white_brake", "window", "wingz_LOD0", "wingz_LOD1",
            "wingz_LOD2", "wingz_LOD3", "wingz_LOD4", "wingz_LOD5", "wood",
        };

        // The materials offered as fixes: those of at least 50 of the 317 FM4 cars (67 names). The rare ones
        // (one-off names such as Material__11 or wingz_LOD3) stay valid but are not offered.
        private static readonly string[] CommonMaterials = new string[]
        {
            "badge", "black", "black_glass", "blur_lip", "blur_rim", "body", "body_2", "bottom", "bump_leather",
            "bump_leather2", "bump_plastic", "bump_plastic2", "bump_stitching", "bumper_frame", "carbon_fiber",
            "chrome", "chrome_2", "chrome_rim", "cloth", "defrost_lines", "detail_glass_amber", "detail_glass_clear",
            "detail_glass_red", "digital_gauge", "emblem", "frame", "gauge_emissive", "gauge_needle", "glass",
            "grille1", "grille1_alpha", "grille2", "grille2_alpha", "head_light", "headliner", "inner_rim",
            "interior", "leather", "lights_glass", "matte_colors", "metal", "mirrorLeft", "mirrorMiddle",
            "mirrorRight", "misc", "mottled", "outer_rim", "plastic", "plastic2", "race_kit_red", "reflector",
            "reverse_light", "reverse_light_lod0", "rim", "rubber", "rubber_trim", "solid", "tail_light",
            "tail_light_lod0", "textured_reflector", "tire", "undercarriage", "weatherstrip", "wheel_black",
            "wheel_emblem", "window", "wood",
        };

        public static byte[] ConvertModel(byte[] data, bool lod0, StringBuilder report)
        {
            return ConvertModel(data, lod0, report, null);
        }

        public static byte[] ConvertModel(byte[] data, bool lod0, StringBuilder report, List<string> materials)
        {
            return ConvertModel(data, lod0, report, materials, null, null);
        }

        // 'materials' (when given) collects the material names the converted model uses; 'renames' are
        // the user's fixes from the review (FH material -> FM4 material, before MaterialMap); 'unknown'
        // collects the materials no FM4 car uses that are left, with the sections they sit on.
        public static byte[] ConvertModel(byte[] data, bool lod0, StringBuilder report, List<string> materials, Dictionary<string, string> renames, Dictionary<string, List<string>> unknownOut)
        {
            CarbinModel model = new CarbinModel(data, true);
            ConvertToMotorsport4(model);
            Dictionary<string, int> renamed = new Dictionary<string, int>();
            List<string[]> remove = new List<string[]>();
            foreach (CarbinModel.Section section in model.Sections)
            {
                foreach (CarbinModel.SubSection sub in section.SubSections)
                {
                    string to;
                    if (renames != null && renames.TryGetValue(sub.Name, out to))
                    {
                        to = (lod0 && to == "tail_light") ? "tail_light_lod0" : to;
                    }
                    else if (!MaterialMap.TryGetValue(sub.Name, out to))
                    {
                        continue;
                    }
                    if (to == null)
                    {
                        remove.Add(new string[] { section.Name, sub.Name });
                        continue;
                    }
                    if (lod0 && to == "tail_light")
                    {
                        to = "tail_light_lod0";
                    }
                    int count;
                    renamed.TryGetValue(sub.Name + " -> " + to, out count);
                    renamed[sub.Name + " -> " + to] = count + 1;
                    sub.Name = to;
                }
            }
            foreach (string[] pair in remove)
            {
                CarbinModel.Section owner = model.Find(pair[0]);
                if (owner.SubSections.Count > 1)
                {
                    model.RemoveSubSections(pair[0], pair[1]);
                }
                else
                {
                    owner.SubSections[0].Name = "black";
                }
            }
            if (materials != null)
            {
                foreach (CarbinModel.Section section in model.Sections)
                {
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        if (!materials.Contains(sub.Name))
                        {
                            materials.Add(sub.Name);
                        }
                    }
                }
            }
            if (report != null && renamed.Count > 0)
            {
                List<string> text = new List<string>();
                foreach (KeyValuePair<string, int> pair in renamed)
                {
                    text.Add(pair.Key + " (" + pair.Value + ")");
                }
                report.AppendLine("  materials: " + string.Join(", ", text.ToArray()) + ((remove.Count > 0) ? string.Format(", {0} 'deprecated' removed", remove.Count) : ""));
            }
            if (report != null)
            {
                // Material -> the sections it sits on.
                Dictionary<string, List<string>> unknown = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                List<string> order = new List<string>();
                foreach (CarbinModel.Section section in model.Sections)
                {
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        if (Array.IndexOf(ModelMaterials, sub.Name) >= 0)
                        {
                            continue;
                        }
                        List<string> sections;
                        if (!unknown.TryGetValue(sub.Name, out sections))
                        {
                            sections = new List<string>();
                            unknown[sub.Name] = sections;
                            order.Add(sub.Name);
                        }
                        if (!sections.Contains(section.Name))
                        {
                            sections.Add(section.Name);
                        }
                    }
                }
                if (order.Count > 0)
                {
                    List<string> text = new List<string>();
                    foreach (string name in order)
                    {
                        text.Add(name + " (" + string.Join(" ", unknown[name].ToArray()) + ")");
                        if (unknownOut != null)
                        {
                            List<string> all;
                            if (!unknownOut.TryGetValue(name, out all))
                            {
                                all = new List<string>();
                                unknownOut[name] = all;
                            }
                            foreach (string section in unknown[name])
                            {
                                if (!all.Contains(section))
                                {
                                    all.Add(section);
                                }
                            }
                        }
                    }
                    report.AppendLine("  WARNING materials no FM4 car uses, may render black: " + string.Join(", ", text.ToArray()));
                }
            }
            return model.ToBytes();
        }

        // FM4 CarAttribs version 17: the donor's file with the car's own gauges, driver placement,
        // part drops and animations (FH2 version 21 writes <animation Version="2" ... inrace="1"/>,
        // FM4 <Animation ... inrace="true" />; FH2 repeats each one per LOD). donorKept (may be null)
        // gets the donor's elements that stay and differ from the car's own (DonorElements).
        public static byte[] ConvertCarAttributes(string fh2, string fm4Donor, bool animations, StringBuilder report, List<string> donorKept)
        {
            byte[] converted = ConvertCarAttributes(fh2, fm4Donor, animations, report);
            if (donorKept != null)
            {
                string text = Encoding.UTF8.GetString(converted);
                donorKept.AddRange(DonorElements(text, fh2));
                if (!fh2.Contains("<PartDropValues>") && text.Contains("<PartDropValues>"))
                {
                    donorKept.Add("Damage PartDropValues: the donor's (the car's file has none)");
                }
            }
            return converted;
        }

        // The top-level elements of the converted CarAttribs (but Animations, always the car's own or
        // none, and Damage and CarDebris, the car's or FM4's standard, ConvertDamage) that the car's
        // own file lacks or has with other values: what stays the donor's. Gauges the car has count as
        // its own (their repeated tick marks are cut). Compared as parsed XML, so only whitespace is
        // ignored: "0.0" against "0.000000" counts as different.
        private static List<string> DonorElements(string converted, string source)
        {
            List<string> kept = new List<string>();
            XmlDocument result = new XmlDocument();
            XmlDocument own = new XmlDocument();
            try
            {
                result.LoadXml(converted.Trim());
                own.LoadXml(source.Trim());
            }
            catch (XmlException ex)
            {
                kept.Add("compared not: " + ex.Message + " (the donor's file is the base, only gauges, driver, part drops and animations are the car's)");
                return kept;
            }
            foreach (XmlNode node in result.DocumentElement.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null || element.Name == "Animations" || element.Name == "Damage" || element.Name == "CarDebris")
                {
                    continue;
                }
                XmlElement mine = own.DocumentElement[element.Name];
                if (mine == null)
                {
                    kept.Add(element.Name + ": the donor's (the car's file has none)");
                }
                else if (element.Name != "Gauges" && mine.OuterXml != element.OuterXml)
                {
                    kept.Add(element.Name + ": the donor's (the car's differs)");
                }
            }
            return kept;
        }

        public static byte[] ConvertCarAttributes(string fh2, string fm4Donor, bool animations, StringBuilder report)
        {
            fh2 = Regex.Replace(fh2, "<(\\w+) NumTicks=\"\\d+\">(.*?)</\\1>", delegate(Match gauge)
            {
                // Repeated marks kept once (in order), at most MaxTicks.
                List<string> ticks = new List<string>();
                MatchCollection marks = Regex.Matches(gauge.Groups[2].Value, "<TickMark\\b[^>]*/>");
                foreach (Match mark in marks)
                {
                    if (!ticks.Contains(mark.Value) && ticks.Count < MaxTicks)
                    {
                        ticks.Add(mark.Value);
                    }
                }
                if (ticks.Count == marks.Count)
                {
                    return gauge.Value;
                }
                if (report != null)
                {
                    report.AppendLine(string.Format("  carattribs: {0} gauge {1} -> {2} tick marks", gauge.Groups[1].Value, marks.Count, ticks.Count));
                }
                Match indent = Regex.Match(gauge.Groups[2].Value, "\\r?\\n([ \\t]*)<TickMark");
                string line = indent.Success ? indent.Value.Substring(0, indent.Value.Length - "<TickMark".Length) : "\r\n      ";
                Match close = Regex.Match(gauge.Groups[2].Value, "\\r?\\n[ \\t]*$");
                return "<" + gauge.Groups[1].Value + " NumTicks=\"" + ticks.Count + "\">" + line + string.Join(line, ticks.ToArray()) + (close.Success ? close.Value : "") + "</" + gauge.Groups[1].Value + ">";
            }, RegexOptions.Singleline);
            string result = fm4Donor;
            foreach (string element in new string[] { "DriverHandsSteeringWheelOverride", "DriverHandsOffset", "SteeringWheelAngleOverride", "Driver" })
            {
                Match from = Regex.Match(fh2, "<" + element + "\\b[^>]*/>");
                Match to = Regex.Match(result, "<" + element + "\\b[^>]*/>");
                if (from.Success && to.Success)
                {
                    result = result.Substring(0, to.Index) + from.Value + result.Substring(to.Index + to.Length);
                }
            }
            foreach (string block in new string[] { "Gauges", "PartDropValues" })
            {
                Match from = Regex.Match(fh2, "<" + block + ">.*?</" + block + ">", RegexOptions.Singleline);
                Match to = Regex.Match(result, "<" + block + ">.*?</" + block + ">", RegexOptions.Singleline);
                if (from.Success && to.Success)
                {
                    result = result.Substring(0, to.Index) + from.Value + result.Substring(to.Index + to.Length);
                }
            }
            result = ConvertDamage(result, fh2, report);
            Match target = Regex.Match(result, "[ \t]*<Animations>.*?</Animations>\r?\n?", RegexOptions.Singleline);
            if (!animations)
            {
                // No animation files: no <Animations> element (as on FM4 cars without moving parts).
                return Encoding.UTF8.GetBytes(target.Success ? result.Remove(target.Index, target.Length) : result);
            }
            Match source = Regex.Match(fh2, "<Animations>.*?</Animations>", RegexOptions.Singleline);
            if (!source.Success || !Regex.IsMatch(source.Value, "<animation\\b", RegexOptions.IgnoreCase))
            {
                // The car has no animations of its own: the donor's would point at bones and .gr2 files
                // the car lacks (AUD_R8GT_11 got the R8 V10's 12, doors and active wing, and crashed
                // the game while loading its model in the showroom).
                return Encoding.UTF8.GetBytes(target.Success ? result.Remove(target.Index, target.Length) : result);
            }
            target = Regex.Match(result, "<Animations>.*?</Animations>", RegexOptions.Singleline);
            if (target.Success)
            {
                StringBuilder list = new StringBuilder("<Animations>\r\n");
                List<string> seen = new List<string>();
                foreach (Match animation in Regex.Matches(source.Value, "<animation\\b([^>]*)/>", RegexOptions.IgnoreCase))
                {
                    string attributes = Regex.Replace(animation.Groups[1].Value, "\\s*Version=\"[^\"]*\"", "");
                    attributes = Regex.Replace(attributes, "inrace=\"1\"", "inrace=\"true\"");
                    attributes = attributes.Trim();
                    if (seen.Contains(attributes))
                    {
                        continue;
                    }
                    seen.Add(attributes);
                    list.Append("    <Animation " + attributes + " />\r\n");
                }
                list.Append("  </Animations>");
                result = result.Substring(0, target.Index) + list.ToString() + result.Substring(target.Index + target.Length);
            }
            return Encoding.UTF8.GetBytes(result);
        }

        // Damage tuning (2026-10-17, CarAttribs of all FM4/FH2/FH1/FM3 cars read, fm4dev\DonorLog\dmg_*.tsv).
        // FM4's standard, on 247 of 315 cars: DamageScalars at 5/35/98 (speed, m/s by the 98 top: 353
        // km/h) giving damage 0.5/1.5/2.5, PartDropGlobalScalar 1, DamageScrape from 0.01 to 0.3 with
        // ScrapeDamage 0.08, CarDebris with its four piece kinds. FH1 and FH2 have a soft preset no FM4
        // car uses on ~138 cars each (the P1): damage 1.0 only at 200 m/s, PartDropGlobalScalar 0.15,
        // scrape from 500 (off), CarDebris empty: the car stays whole after a crash at 200 km/h. The
        // car's own values are kept, each part that is softer than FM4's goes to FM4's standard:
        //   scalars   damage at 200 km/h (55.6 m/s, linear between the points) under 1.7; the common
        //             FM4 curves give 1.83 and 1.71, FH's soft one 0.30, the 60/120/180 one 0.2, the
        //             zeroed blocks (uninitialised, 18 FM4 and 37 FM3 cars) 0;
        //   value     the Damage multiplier outside 1..10 (zeroed blocks have 1.7e29);
        //   drops     PartDropGlobalScalar under 1 (every FM4 car with a real block has 1);
        //   scrape    starting over 0.3 or ScrapeDamage under 0.08, or missing;
        //   debris    CarDebris empty or missing.
        // The car's PartDropValues stay (the donor's only when the car has none).
        private const double DamageSpeed = 200 / 3.6;
        private const double MinimumDamage = 1.7;
        private static readonly double[][] StandardScalars = { new double[] { 5, 0.3, 0.5 }, new double[] { 35, 1.3, 1.5 }, new double[] { 98, 3.2, 2.5 } };
        private const string StandardScrape = "<DamageScrape ScrapeMinSpeed=\"0.010000\" ScrapeMaxSpeed=\"0.300000\" ScrapeRadius=\"1.300000\" ScrapeDamage=\"0.080000\" DamageBias=\"0.750000\" ScrapeBias=\"0.700000\" GlassDamageCausingBreak=\"0.005000\" />";
        private static readonly string[] StandardDebris = { "Mechanical", "Fiber", "Frame", "Crumb" };

        private static string ConvertDamage(string result, string source, StringBuilder report)
        {
            string newline = result.Contains("\r\n") ? "\r\n" : "\n";
            List<string> notes = new List<string>();

            // CarDebris
            Match sourceDebris = Regex.Match(source, "<CarDebris\\s*/>|<CarDebris>.*?</CarDebris>", RegexOptions.Singleline);
            Match debris = Regex.Match(result, "([ \\t]*)(<CarDebris\\s*/>|<CarDebris>.*?</CarDebris>)", RegexOptions.Singleline);
            if (debris.Success)
            {
                string indent = debris.Groups[1].Value;
                string text;
                if (sourceDebris.Success && Regex.IsMatch(sourceDebris.Value, "<\\w+\\b[^>]*MaxDropCount"))
                {
                    text = sourceDebris.Value;
                }
                else
                {
                    StringBuilder pieces = new StringBuilder("<CarDebris>" + newline);
                    foreach (string piece in StandardDebris)
                    {
                        pieces.Append(indent + "  <" + piece + " MaxDropCount_DamageCosmetic=\"1000\" MaxDropCount_DamageLimited=\"1000\" MaxDropCount_DamageFull=\"1000\" MinDropForce=\"1.000000\" MaxDropForce=\"100.000000\" Area=\"\" />" + newline);
                    }
                    text = pieces.Append(indent + "</CarDebris>").ToString();
                    notes.Add("debris FM4 standard (the car's " + (sourceDebris.Success ? "is empty" : "file has none") + ")");
                }
                result = result.Substring(0, debris.Groups[2].Index) + text + result.Substring(debris.Groups[2].Index + debris.Groups[2].Length);
            }

            // Damage
            Match sourceDamage = Regex.Match(source, "<Damage\\b[^>]*?(/>|>.*?</Damage>)", RegexOptions.Singleline);
            Match damage = Regex.Match(result, "([ \\t]*)(<Damage\\b[^>]*?(/>|>.*?</Damage>))", RegexOptions.Singleline);
            string own = sourceDamage.Success ? sourceDamage.Value : "";
            string ownTag = Regex.Match(own, "^<Damage\\b[^>]*>").Value;

            double value = Number(Attribute(ownTag, "value"));
            string valueText = (value >= 1 && value <= 10) ? Attribute(ownTag, "value") : "1.000000";
            if (valueText != Attribute(ownTag, "value"))
            {
                notes.Add("multiplier 1 (the car's " + (Attribute(ownTag, "value") ?? "none") + ")");
            }

            List<double[]> points = new List<double[]>();
            List<string> scalarLines = new List<string>();
            foreach (Match scalar in Regex.Matches(own, "<DamageScalars\\b[^>]*/>"))
            {
                points.Add(new double[] { Number(Attribute(scalar.Value, "speed")), Number(Attribute(scalar.Value, "radius")), Number(Attribute(scalar.Value, "damage")) });
                scalarLines.Add(scalar.Value);
            }
            double at = DamageAt(points, DamageSpeed);
            if (!(at >= MinimumDamage))
            {
                scalarLines.Clear();
                foreach (double[] point in StandardScalars)
                {
                    scalarLines.Add(string.Format(CultureInfo.InvariantCulture, "<DamageScalars speed=\"{0:0.000000}\" radius=\"{1:0.000000}\" damage=\"{2:0.000000}\" />", point[0], point[1], point[2]));
                }
                notes.Add(string.Format(CultureInfo.InvariantCulture, "scalars FM4 standard (the car's: {0} at 200 km/h, under {1})", (points.Count > 0) ? at.ToString("0.00", CultureInfo.InvariantCulture) : "none", MinimumDamage));
            }

            Match drops = Regex.Match(damage.Success ? damage.Value : "", "<PartDropValues>.*?</PartDropValues>", RegexOptions.Singleline);
            if (!drops.Success)
            {
                drops = Regex.Match(own, "<PartDropValues>.*?</PartDropValues>", RegexOptions.Singleline);
            }

            Match global = Regex.Match(own, "<PartDropGlobalScalar\\b[^>]*/>");
            string globalLine = global.Value;
            if (!(Number(Attribute(global.Value, "value")) >= 1 && Number(Attribute(global.Value, "value")) <= 10))
            {
                globalLine = "<PartDropGlobalScalar value=\"1.000000\" />";
                notes.Add("part drop scale 1 (the car's " + (Attribute(global.Value, "value") ?? "none") + ")");
            }

            Match scrape = Regex.Match(own, "<DamageScrape\\b[^>]*/>");
            string scrapeLine = scrape.Value;
            double scrapeFrom = Number(Attribute(scrape.Value, "ScrapeMinSpeed"));
            double scrapeDamage = Number(Attribute(scrape.Value, "ScrapeDamage"));
            double scrapeRadius = Number(Attribute(scrape.Value, "ScrapeRadius"));
            if (!(scrapeFrom >= 0 && scrapeFrom <= 0.3 && scrapeDamage >= 0.08 && scrapeRadius > 0 && scrapeRadius <= 10))
            {
                scrapeLine = StandardScrape;
                notes.Add(scrape.Success ? string.Format(CultureInfo.InvariantCulture, "scrape FM4 standard (the car's from {0}, damage {1})", Attribute(scrape.Value, "ScrapeMinSpeed"), Attribute(scrape.Value, "ScrapeDamage")) : "scrape FM4 standard (the car's file has none)");
            }

            string at0 = damage.Success ? damage.Groups[1].Value : "  ";
            string inner = at0 + "  ";
            StringBuilder block = new StringBuilder("<Damage Version=\"3\" value=\"" + valueText + "\">" + newline);
            foreach (string line in scalarLines)
            {
                block.Append(inner + line + newline);
            }
            if (drops.Success)
            {
                block.Append(inner + drops.Value + newline);
            }
            block.Append(inner + globalLine + newline);
            block.Append(inner + scrapeLine + newline);
            block.Append(at0 + "</Damage>");
            if (damage.Success)
            {
                result = result.Substring(0, damage.Groups[2].Index) + block.ToString() + result.Substring(damage.Groups[2].Index + damage.Groups[2].Length);
            }
            else
            {
                Match before = Regex.Match(result, "[ \\t]*<Tires\\b|</CarAttributes>");
                if (before.Success)
                {
                    result = result.Substring(0, before.Index) + at0 + block.ToString() + newline + result.Substring(before.Index);
                }
            }

            if (report != null)
            {
                report.AppendLine("  carattribs damage: " + ((notes.Count == 0) ? "the car's own" : "the car's, but " + string.Join("; ", notes.ToArray())));
            }
            return result;
        }

        // Damage at a speed on the DamageScalars curve: linear between the points, the end values past them.
        private static double DamageAt(List<double[]> points, double speed)
        {
            if (points.Count == 0)
            {
                return 0;
            }
            List<double[]> sorted = new List<double[]>(points);
            sorted.Sort(delegate(double[] a, double[] b) { return a[0].CompareTo(b[0]); });
            if (speed <= sorted[0][0])
            {
                return sorted[0][2];
            }
            for (int i = 1; i < sorted.Count; i++)
            {
                if (speed <= sorted[i][0])
                {
                    double[] a = sorted[i - 1];
                    double[] b = sorted[i];
                    return (b[0] > a[0]) ? a[2] + (b[2] - a[2]) * (speed - a[0]) / (b[0] - a[0]) : b[2];
                }
            }
            return sorted[sorted.Count - 1][2];
        }

        private static string Attribute(string element, string name)
        {
            Match match = Regex.Match(element ?? "", "\\b" + Regex.Escape(name) + "=\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static double Number(string text)
        {
            double number;
            return (text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) ? number : double.NaN;
        }

        // FM4 ShaderSettings.xml: only the materials the converted models use (FH2-only ones under
        // their FM4 names, tail lights also for the _lod0 variant) and only the parameters FM4 has.
        // FM4 cars list at most 24 materials; the FH2 P1 lists 33, most for materials it renamed.
        public static byte[] ConvertShaderSettings(string fh2, List<string> materials, StringBuilder report)
        {
            List<string> order = new List<string>();
            Dictionary<string, List<string[]>> settings = new Dictionary<string, List<string[]>>(StringComparer.Ordinal);
            List<string> dropped = new List<string>();
            List<string> parameters = new List<string>(ShaderParameters);
            int read = 0;
            Match root = Regex.Match(fh2, "<ShaderSettings>(.*)</ShaderSettings>", RegexOptions.Singleline);
            foreach (Match section in Regex.Matches(root.Success ? root.Groups[1].Value : fh2, "<(\\w+)>(.*?)</\\1>", RegexOptions.Singleline))
            {
                read++;
                // "cockpit_<material>" sets <material> of the cockpit model (FM4 MP4-12C: cockpit_bump_leather2).
                string prefix = section.Groups[1].Value.StartsWith("cockpit_") ? "cockpit_" : "";
                string name = section.Groups[1].Value.Substring(prefix.Length);
                string to;
                List<string> targets = new List<string>();
                if (!MaterialMap.TryGetValue(name, out to))
                {
                    targets.Add(name);
                }
                else if (to != null)
                {
                    targets.Add(to);
                    if (to == "tail_light" && prefix.Length == 0)
                    {
                        targets.Add("tail_light_lod0");
                    }
                }
                foreach (string material in targets)
                {
                    if (materials != null && !materials.Contains(material))
                    {
                        continue;
                    }
                    string target = prefix + material;
                    List<string[]> values;
                    if (!settings.TryGetValue(target, out values))
                    {
                        values = new List<string[]>();
                        settings[target] = values;
                        order.Add(target);
                    }
                    foreach (Match value in Regex.Matches(section.Groups[2].Value, "<(\\w+)\\b[^>]*/>"))
                    {
                        string parameter = value.Groups[1].Value;
                        bool known = parameters.Exists(delegate(string p) { return string.Equals(p, parameter, StringComparison.OrdinalIgnoreCase); });
                        if (!known)
                        {
                            if (!dropped.Contains(parameter))
                            {
                                dropped.Add(parameter);
                            }
                            continue;
                        }
                        if (!values.Exists(delegate(string[] v) { return v[0] == parameter; }))
                        {
                            values.Add(new string[] { parameter, value.Value });
                        }
                    }
                }
            }
            StringBuilder xml = new StringBuilder("<ShaderSettings>\r\n");
            int written = 0;
            foreach (string name in order)
            {
                if (settings[name].Count == 0)
                {
                    continue;
                }
                written++;
                xml.Append("  <" + name + ">\r\n");
                foreach (string[] value in settings[name])
                {
                    xml.Append("    " + value[1] + "\r\n");
                }
                xml.Append("  </" + name + ">\r\n");
            }
            xml.Append("</ShaderSettings>\r\n");
            if (report != null)
            {
                report.AppendLine(string.Format("  shadersettings: {0} -> {1} materials{2}", read, written, (dropped.Count > 0) ? "; parameters FM4 lacks left out: " + string.Join(", ", dropped.ToArray()) : ""));
            }
            return Encoding.UTF8.GetBytes(xml.ToString());
        }

        private void BuildWheelZips()
        {
            string donorWheelMedia = this.target.Scalar("SELECT w.MediaName FROM List_Wheels w JOIN Data_Car c ON c.StockWheelID = w.ID WHERE c.Id = ?", this.donorId) as string;
            foreach (long id in this.wheelIds)
            {
                string media = this.source.Scalar("SELECT MediaName FROM List_Wheels WHERE ID = ?", id) as string;
                if (media == null && this.motorsport3)
                {
                    media = this.target.Scalar("SELECT MediaName FROM List_Wheels WHERE ID = ?", id) as string;   // a new id (Motorsport3Wheel)
                }
                string sourcePath = GamePaths.FindMedia(this.SourceRoot, "wheels", media);
                string donorPath = (donorWheelMedia != null) ? GamePaths.FindMedia(this.TargetRoot, "wheels", donorWheelMedia) : null;
                if (sourcePath == null || donorPath == null)
                {
                    this.Line("wheel {0}: zip not found, skipped", media);
                    continue;
                }
                string path = Path.Combine(Path.Combine(Path.Combine(this.TargetRoot, "Media"), "wheels"), media + ".zip");
                object original = this.originalWheels.Contains(id) ? (object) true : null;
                if (File.Exists(path) && original != null)
                {
                    this.Line("wheel {0}: an FM4 wheel, kept", media);
                    continue;
                }
                ForzaZip from = new ForzaZip(sourcePath);
                ForzaZip donor = new ForzaZip(donorPath);
                ForzaZip wheel = ForzaZip.CreateLike(donor);
                foreach (ForzaZip.Entry entry in from.Entries)
                {
                    string name = entry.Name.ToLowerInvariant();
                    byte[] data = from.Read(entry);
                    if (name.EndsWith(".carbin"))
                    {
                        CarbinModel model = new CarbinModel(data, true);
                        ConvertToMotorsport4(model);
                        data = model.ToBytes();
                    }
                    else if (name.EndsWith(".xds") && this.motorsport3)
                    {
                        data = ConvertMotorsport3Texture(data, name);
                    }
                    wheel.Add(name, data);
                }
                ForzaZip.Entry version = donor.Find("versiondata.xml");
                if (version != null && wheel.Find("versiondata.xml") == null)
                {
                    wheel.Add("versiondata.xml", donor.Read(version));
                    this.donorContent.Add("wheel " + media + " versiondata.xml: the donor wheel's (" + this.game + " has none)");
                }
                this.donorFormat.Add("wheel " + media + " zip: entry headers and zipmanifest.xml line of the donor wheel " + donorWheelMedia);
                this.SaveNew(wheel, path);
                this.Line("wheel zip: {0} ({1} entries)", this.Relative(path), wheel.Entries.Count);
            }
        }

        #endregion

        #region Sound and thumbnail

        // FH2's engine tuning (<engine media>_ET.xml) and car model tuning (<car>_CMT.xml) have the FM4
        // layout and point at harmonic tunings by name. A harmonic tuning FM4 lacks comes from FH2
        // with the sound bank it names (<Soundbank name="2N1_Ducati998_EngAmb.fsb">, a loose FSB4 file
        // in Engines\soundbanks\lod1): the 353 banks both games have are byte-identical and the
        // tunings they share too. Only when FH2 lacks one of them is the donor's engine tuning used.
        private void PortSound()
        {
            string sourceAudio = Path.Combine(Path.Combine(Path.Combine(this.SourceRoot, "Media"), "audio"), "cars");
            string targetAudio = Path.Combine(Path.Combine(Path.Combine(this.TargetRoot, "Media"), "audio"), "Cars");
            ForzaZip sourceTuning = new ForzaZip(Path.Combine(Path.Combine(sourceAudio, "Engines"), "EngineTuning.zip"));
            ForzaZip sourceHarmonic = new ForzaZip(Path.Combine(Path.Combine(sourceAudio, "Engines"), "HarmonicTuning.zip"));
            string sourceBanks = Path.Combine(Path.Combine(Path.Combine(sourceAudio, "Engines"), "Soundbanks"), "LOD1");
            string targetBanks = Path.Combine(Path.Combine(Path.Combine(targetAudio, "Engines"), "soundbanks"), "lod1");
            string harmonicPath = Path.Combine(Path.Combine(targetAudio, "Engines"), "harmonictuning.zip");
            ForzaZip harmonic = new ForzaZip(this.In(harmonicPath));
            List<string> imported = new List<string>();
            List<string> checkedHarmonics = new List<string>();   // in FM4 with all their sound banks
            ForzaZip engineTuning = this.OpenChanged(Path.Combine(Path.Combine(targetAudio, "Engines"), "enginetuning.zip"));
            string donorEngine = this.target.Scalar("SELECT e.MediaName FROM Data_Engine e JOIN List_UpgradeEngine u ON u.EngineID = e.EngineID WHERE u.Ordinal = ? AND u.Level = 0", this.donorId) as string;
            foreach (string media in this.engineMedia)
            {
                ForzaZip.Entry entry = sourceTuning.Find(media + "_ET.xml");
                byte[] data = (entry != null) ? sourceTuning.Read(entry) : null;
                List<string> missing = new List<string>();
                if (data != null)
                {
                    foreach (Match reference in Regex.Matches(Encoding.UTF8.GetString(data), "\"([^\"]+_HT\\.xml)\"", RegexOptions.IgnoreCase))
                    {
                        string name = reference.Groups[1].Value;
                        if (imported.Contains(name) || missing.Contains(name) || checkedHarmonics.Contains(name))
                        {
                            continue;
                        }
                        int result = this.ImportHarmonic(harmonic, sourceHarmonic, harmonicPath, name, sourceBanks, targetBanks);
                        if (result > 0)
                        {
                            imported.Add(name);
                        }
                        else if (result < 0)
                        {
                            missing.Add(name);
                        }
                        else
                        {
                            checkedHarmonics.Add(name);
                        }
                    }
                }
                if (data == null || missing.Count > 0)
                {
                    ForzaZip.Entry fallback = (donorEngine != null) ? engineTuning.Find(donorEngine + "_ET.xml") : null;
                    if (fallback == null)
                    {
                        this.Line("engine sound {0}: no tuning", media);
                        continue;
                    }
                    data = engineTuning.Read(fallback);
                    this.donorContent.Add(string.Format("engine sound {0}: the donor's tuning {1}_ET.xml ({2} lacks {3})", media, donorEngine, this.game, (missing.Count > 0) ? string.Join(", ", missing.ToArray()) : "its tuning"));
                    this.Line("engine sound {0}: the donor's ({1}; " + this.game + " lacks {2})", media, donorEngine, (missing.Count > 0) ? string.Join(", ", missing.ToArray()) : "the " + this.game + " tuning");
                    // The donor's tunings are FM4's, but their banks may be missing from this FM4 too: they
                    // come from the source game like the car's own.
                    foreach (Match reference in Regex.Matches(Encoding.UTF8.GetString(data), "\"([^\"]+_HT\\.xml)\"", RegexOptions.IgnoreCase))
                    {
                        string name = reference.Groups[1].Value;
                        if (!checkedHarmonics.Contains(name) && !imported.Contains(name))
                        {
                            checkedHarmonics.Add(name);
                            if (this.ImportHarmonic(harmonic, sourceHarmonic, harmonicPath, name, sourceBanks, targetBanks) < 0)
                            {
                                this.Line("  WARNING donor harmonic tuning {0}: a sound bank is in neither game", name);
                            }
                        }
                    }
                }
                else
                {
                    this.Line("engine sound {0}: " + this.game + " tuning, its harmonic tunings are all in FM4{1}", media, (imported.Count > 0) ? " (" + imported.Count + " brought from " + this.game + ")" : "");
                }
                this.Put(engineTuning, media + "_ET.xml", data);
            }

            ForzaZip sourceModels = new ForzaZip(Path.Combine(sourceAudio, "CarModelTuning.zip"));
            ForzaZip carModels = this.OpenChanged(Path.Combine(targetAudio, "CarModelTuning.zip"));
            ForzaZip.Entry modelTuning = sourceModels.Find(this.MediaName + "_CMT.xml");
            if (modelTuning != null)
            {
                this.Put(carModels, this.MediaName + "_CMT.xml", sourceModels.Read(modelTuning));
                this.Line("car model tuning {0}_CMT.xml added", this.MediaName);
            }
        }

        // A harmonic tuning the engine tuning names, made complete in FM4: the tuning itself from the
        // source game when FM4's harmonictuning.zip lacks it, and EVERY sound bank it names
        // (<Soundbank name="12T1_Bently_Exh.fsb">, loose FSB4 files in Engines\soundbanks\lod1, written
        // lower-case like FM4's) that FM4 lacks, even when FM4 has the tuning: the extracted FM4 disc
        // holds 356 banks against FH1's 937, and its own tunings name banks it does not have
        // (MER_S65AMG_12's exhaust 12T1_Bently_Exh: "Disc Read Error" while loading the race).
        // 1 = something imported, 0 = all already in FM4, -1 = the source game lacks a piece.
        private int ImportHarmonic(ForzaZip targetHarmonic, ForzaZip sourceHarmonic, string harmonicPath, string name, string sourceBanks, string targetBanks)
        {
            ForzaZip.Entry own = targetHarmonic.Find(name);
            ForzaZip.Entry entry = (own != null) ? own : sourceHarmonic.Find(name);
            if (entry == null)
            {
                return -1;
            }
            byte[] data = ((own != null) ? targetHarmonic : sourceHarmonic).Read(entry);
            MatchCollection banks = Regex.Matches(Encoding.UTF8.GetString(data), "<Soundbank\\s+name=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (banks.Count == 0 && own == null)
            {
                return -1;
            }
            Dictionary<string, string> copies = new Dictionary<string, string>();
            foreach (Match bank in banks)
            {
                string target = Path.Combine(targetBanks, bank.Groups[1].Value.ToLowerInvariant());
                if (this.newFiles.ContainsKey(target) || copies.ContainsKey(target) || this.checkedBanks.Contains(target))
                {
                    continue;
                }
                string source = null;
                if (Directory.Exists(sourceBanks))
                {
                    foreach (string file in Directory.GetFiles(sourceBanks))
                    {
                        if (string.Equals(Path.GetFileName(file), bank.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                        {
                            source = file;
                        }
                    }
                }
                string existing = FindFile(targetBanks, bank.Groups[1].Value) ?? this.InTrial(Path.Combine(targetBanks, bank.Groups[1].Value));
                if (existing != null)
                {
                    // FM4 has a bank of that name: identical bytes are the usual case (all 353 banks FH2
                    // and FM4 share). A different one stays FM4's, since FM4 cars use it, and is reported.
                    this.checkedBanks.Add(target);
                    if (source != null && !SameFile(source, existing))
                    {
                        this.Line("  WARNING sound bank {0}: FM4's differs from the {1} one, FM4's kept", Path.GetFileName(existing), this.game);
                    }
                    continue;
                }
                if (source == null)
                {
                    return -1;
                }
                copies[target] = source;
            }
            foreach (KeyValuePair<string, string> copy in copies)
            {
                this.newFiles[copy.Key] = copy.Value;
                this.Line("  sound bank {0} from " + this.game + "{1}", Path.GetFileName(copy.Key), (own != null) ? " (FM4 has the tuning, not the bank)" : "");
            }
            if (own == null)
            {
                this.Put(this.OpenChanged(harmonicPath), name, data);
                this.Line("  harmonic tuning {0} from " + this.game, name);
            }
            return (own == null || copies.Count > 0) ? 1 : 0;
        }

        // The file of that name in a folder, any case; null if none.
        private static string FindFile(string folder, string name)
        {
            string path = Path.Combine(folder, name);
            return File.Exists(path) ? path : null;
        }

        private static bool SameFile(string a, string b)
        {
            if (new FileInfo(a).Length != new FileInfo(b).Length)
            {
                return false;
            }
            byte[] x = File.ReadAllBytes(a);
            byte[] y = File.ReadAllBytes(b);
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }
            return true;
        }

        private void PortThumbnail()
        {
            if (this.skipThumbnail)
            {
                this.Line("thumbnail: skipped, Media\\UI\\Textures.zip is in use (the emulator is running)");
                return;
            }
            string thumbnail = this.source.Scalar("SELECT Thumbnail FROM Data_Car WHERE Id = ?", this.carId) as string;
            string donorThumbnail = this.target.Scalar("SELECT Thumbnail FROM Data_Car WHERE Id = ?", this.donorId) as string;
            GamePaths.GameFile from = GamePaths.Locate(this.SourceRoot, thumbnail);
            GamePaths.GameFile like = GamePaths.Locate(this.TargetRoot, donorThumbnail);
            if (from == null || like == null || !like.InZip)
            {
                this.Line("thumbnail: not found, skipped");
                return;
            }
            ForzaZip textures = this.OpenChanged(like.ZipPath);
            string name = like.EntryName.Substring(0, like.EntryName.LastIndexOf('/') + 1) + Path.GetFileName(thumbnail.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));
            this.Put(textures, name, GamePaths.Read(from));
            this.donorFormat.Add("thumbnail: put in the folder of the donor's (" + like.EntryName.Substring(0, like.EntryName.LastIndexOf('/') + 1) + ")");
            this.Line("thumbnail: {0} in {1}", name, this.Relative(like.ZipPath));
        }

        #endregion

        #region Files

        private ForzaZip OpenChanged(string path)
        {
            ForzaZip archive;
            if (!this.changedZips.TryGetValue(path, out archive))
            {
                archive = new ForzaZip(this.In(path));
                this.changedZips[path] = archive;
            }
            return archive;
        }

        private void Put(ForzaZip archive, string name, byte[] data)
        {
            ForzaZip.Entry entry = archive.Find(name);
            if (entry != null)
            {
                archive.Replace(entry, data);
            }
            else
            {
                archive.Add(name, data);
            }
        }

        private void SaveNew(ForzaZip archive, string path)
        {
            string output = this.Out(path);
            if (File.Exists(output))
            {
                this.Backup(output);
            }
            archive.Save(output);
            this.newZips.Add(path);
        }

        // Where a target file is read: its copy under OutputRoot when an earlier car of the same trial
        // folder (a DLC pack) wrote one, else itself.
        private string In(string path)
        {
            return this.InTrial(path) ?? path;
        }

        // The copy of a target file under OutputRoot, or null.
        private string InTrial(string path)
        {
            if (this.OutputRoot == null)
            {
                return null;
            }
            string copy = Path.Combine(this.OutputRoot, this.Relative(path));
            return File.Exists(copy) ? copy : null;
        }

        // Where a target file is written: itself, or its place under OutputRoot on a trial run.
        private string Out(string path)
        {
            if (this.OutputRoot == null)
            {
                return path;
            }
            string output = Path.Combine(this.OutputRoot, this.Relative(path));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            return output;
        }

        // Before anything is written: every game file the port may replace must be free (the game
        // running in Xenia keeps UI\Textures.zip open; failing halfway would leave rewritten zips
        // without their manifest lines).
        private void CheckWritable(string databasePath)
        {
            string media = Path.Combine(this.TargetRoot, "Media");
            List<string> files = new List<string>();
            files.Add(databasePath);
            files.Add(ZipManifest.PathFor(this.TargetRoot));
            files.Add(Path.Combine(Path.Combine(media, "UI"), "Textures.zip"));
            files.Add(Path.Combine(Path.Combine(Path.Combine(media, "audio"), "Cars"), "CarModelTuning.zip"));
            files.Add(Path.Combine(Path.Combine(Path.Combine(Path.Combine(media, "audio"), "Cars"), "Engines"), "enginetuning.zip"));
            files.Add(Path.Combine(Path.Combine(Path.Combine(Path.Combine(media, "audio"), "Cars"), "Engines"), "harmonictuning.zip"));
            files.Add(Path.Combine(Path.Combine(media, "cars"), this.MediaName + ".zip"));
            files.Add(Path.Combine(Path.Combine(media, "wheels"), this.MediaName + ".zip"));
            string strings = Path.Combine(media, "StringTables");
            if (Directory.Exists(strings))
            {
                files.AddRange(Directory.GetFiles(strings, "*.zip"));
            }
            List<string> busy = new List<string>();
            foreach (string file in files)
            {
                if (!File.Exists(file))
                {
                    continue;
                }
                try
                {
                    using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                    }
                }
                catch (IOException)
                {
                    busy.Add(this.Relative(file));
                }
                catch (UnauthorizedAccessException)
                {
                    busy.Add(this.Relative(file));
                }
            }
            // Xenia keeps Media\UI\Textures.zip open while it runs: the port goes on without the thumbnail
            // (a later port of the same car with the emulator closed adds it).
            string textures = this.Relative(Path.Combine(Path.Combine(media, "UI"), "Textures.zip"));
            if (busy.Remove(textures))
            {
                this.skipThumbnail = true;
            }
            if (busy.Count > 0)
            {
                throw new IOException("Files in use (close the game or the emulator first): " + string.Join(", ", busy.ToArray()));
            }
        }

        private void Write(string path, byte[] data)
        {
            path = this.Out(path);
            if (File.Exists(path))
            {
                this.Backup(path);
            }
            string temp = path + ".tmp";
            File.WriteAllBytes(temp, data);
            File.Delete(path);
            File.Move(temp, path);
            this.Line("saved {0}", this.Relative(path));
        }

        private void Backup(string path)
        {
            // Trial files are not the game's (and a pack's second car would copy UI\Textures.zip again).
            if (!File.Exists(path) || this.OutputRoot != null)
            {
                return;
            }
            string folder = this.BackupFolder;
            Directory.CreateDirectory(folder);
            string copy = Path.Combine(folder, this.stamp + "_bak_" + Path.GetFileName(path));
            for (int i = 2; File.Exists(copy); i++)
            {
                copy = Path.Combine(folder, this.stamp + "_bak" + i + "_" + Path.GetFileName(path));
            }
            File.Copy(path, copy);
        }

        private void UpdateManifest()
        {
            string manifest = this.Out(ZipManifest.PathFor(this.TargetRoot));
            if (this.OutputRoot != null && !File.Exists(manifest))
            {
                File.Copy(ZipManifest.PathFor(this.TargetRoot), manifest);
            }
            this.Backup(manifest);
            foreach (string path in this.changedZips.Keys)
            {
                ZipManifest.Update(manifest, ZipManifest.GamePath(this.TargetRoot, path), this.Out(path));
            }
            foreach (string path in this.newZips)
            {
                string folder = Path.GetFileName(Path.GetDirectoryName(path)).ToLowerInvariant();
                string like = "game:\\media\\" + folder + "\\" + ((folder == "cars") ? this.DonorMediaName : this.DonorWheel()).ToLowerInvariant() + ".zip";
                if (!ZipManifest.Add(manifest, ZipManifest.GamePath(this.TargetRoot, path), this.Out(path), like))
                {
                    throw new InvalidOperationException("zipmanifest.xml has no line like " + like + ".");
                }
            }
            this.Line("zipmanifest.xml: {0} lines updated, {1} added", this.changedZips.Count, this.newZips.Count);
        }

        // What the car took from the donor, at the end of the report: content (files or values the
        // car uses as its own) apart from donor files used only as a layout model.
        private void DonorReport()
        {
            this.Line("");
            this.Line("donor {0}, used for:", this.DonorMediaName);
            this.Line("  content{0}", (this.donorContent.Count == 0) ? ": nothing" : "");
            foreach (string use in this.donorContent)
            {
                this.Line("    " + use.Replace("{", "{{").Replace("}", "}}"));
            }
            this.Line("  layout only{0}", (this.donorFormat.Count == 0) ? ": nothing" : "");
            foreach (string use in this.donorFormat)
            {
                this.Line("    " + use.Replace("{", "{{").Replace("}", "}}"));
            }
            this.Line("");
        }

        private string DonorWheel()
        {
            return (this.target.Scalar("SELECT w.MediaName FROM List_Wheels w JOIN Data_Car c ON c.StockWheelID = w.ID WHERE c.Id = ?", this.donorId) as string) ?? this.DonorMediaName;
        }

        private string Relative(string path)
        {
            return path.StartsWith(this.TargetRoot, StringComparison.OrdinalIgnoreCase) ? path.Substring(this.TargetRoot.TrimEnd('\\').Length + 1) : path;
        }

        private void Line(string format, params object[] args)
        {
            this.Report.AppendLine(string.Format(CultureInfo.InvariantCulture, format, args));
        }

        #endregion
    }
}
