namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // Generic upgrades for a ported car that has none (ROADMAP 14.1f). FH traffic cars come with one
    // stock row per part, most at level 3: FM4 reads that as a race part already fitted, with nothing
    // to buy (FM4 does the same on purpose for its race cars and a few supercars). A part table whose
    // only row for the car is the stock part gets FM4's Street / Sport / Race levels from the rules in
    // Upgrades.ini, next to the exe (written with the defaults below when missing). FM4 builds its own
    // upgrades from fixed rules: the defaults are the medians of its 503 cars (fm4dev\Traffic\tools\
    // upgstats). Gear ratios are never changed; engine swaps are made by hand (gamedb or the DLC merge).
    // Drive type conversions follow the user's rule (2026-10-20): AWD -> RWD, FWD -> AWD, RWD none.
    internal sealed class CarUpgrades
    {
        public const string FileName = "Upgrades.ini";

        public const string DefaultIni =
@"; Forza Studio - generic upgrades for ported cars (ROADMAP 14.1f)
;
; A ported car whose part table holds only the stock part gets FM4's Street / Sport / Race levels
; from these rules. FH traffic cars come that way: one stock row per part, most at level 3, which
; FM4 reads as a race part with nothing to buy. FM4 builds its own upgrades from fixed rules; the
; values below are the medians of its 503 cars. Edit them freely; delete this file to get the
; defaults back. Gear ratios are never changed. Engine swaps are not made here: edit the gamedb
; (or the DLC merge file) by hand.
;
; One section per FM4 table. StockLevel = the level the stock part takes (FM4's most common one).
; Each column lists one value per level, from StockLevel up to 3:
;   stock          on the stock level the source value, above it the stock level's value
;   x1.5           1.5 times that value
;   +10            that value plus 10
;   0.2            a fixed value
;   -0.15%         a percent of the car's curb weight in kg (masses)
;   =Column x0.5   the same level's value of another column of the row (x optional)
;   toward 1000 0.45   that value moved 45% of the way to 1000
; A list shorter than the levels goes on by the step between its last two values.
; Levels = n sets how many levels go above the stock one; Levels = upto Column x1.3 adds levels
; while that column stays within 1.3 times its stock value. Columns not listed keep the stock
; value. Physics. / Front. / Rear. / Curve. = the linked row (anti-roll bar physics, front / rear
; spring-damper physics, camshaft torque curve: also stretched to the new redline).

[General]
; traffic = only FH traffic cars (placeholder close-up models)
; missing = every ported car, in each table that holds only its stock part
; off     = never
Cars = traffic
; Part maker of the upgrade rows (List_PartManufacturer; 0 = none, as many FM4 rows)
ManufacturerID = 0

[Drivetrain swap]
; Stock drive type = the type it can be converted to (FWD, RWD, AWD; empty = no conversion).
; Only for a car without a conversion of its own. FM4's conversions: to AWD +155 kg and weight
; distribution -0.05, to RWD -110 kg and -0.02, 6800 Cr most often; FM4 has none to FWD (guess).
FWD = AWD
RWD =
AWD = RWD
AWD.MassDiff = 155
AWD.WeightDistDiff = -0.05
AWD.Price = 6800
AWD.RearToqueSplit = 0.5
RWD.MassDiff = -110
RWD.WeightDistDiff = -0.02
RWD.Price = 6800
FWD.MassDiff = -110
FWD.WeightDistDiff = 0.02
FWD.Price = 6800

[Aspiration]
; Turbo / supercharger conversions (levels 1-3) for an engine without any (no TurboSingle /
; TurboTwin / TurboQuad / CSC / DSC row, so a naturally aspirated one): the types listed here, each
; from its [Aspiration.<type>] section; the intercooler comes with them. FM4's own conversions are
; these fixed values (10 FM4 engines sampled, 2026-10-20); WeightDistDiff follows MassDiff (+0.3 of
; it over the curb weight for a front engine, -0.3 otherwise). One value = every level.
Types = TurboSingle, TurboTwin, CSC, DSC
; Twin turbo only from this many cylinders (user 2026-10-20: none under 6; Data_Engine.CylinderID ->
; List_Cylinders.Number, rotors counted as cylinders)
TwinTurboMinCylinders = 6

[Aspiration.TurboSingle]
MassDiff = 7, 8.5, 11
Price = 2700, 5500, 12300
MomentInertia = 25, 31, 35
MaxScale = 1.25, 1.4, 1.5
PowerMaxScale = 100, 130, 150
MinScale = 0.95, 0.935, 0.925
PowerMinScale = 10
RobScale = 0.95, 0.9286, 0.9167
TorqueDropOffRPM0 = 9000
TorqueDropOffRPM1 = 15000
TorqueDropOffScale0 = 1
TorqueDropOffScale1 = 0.75

[Aspiration.TurboTwin]
MassDiff = 11, 12.5, 13
Price = 2700, 5500, 12300
MomentInertia = 15, 18, 20
MaxScale = 1.25, 1.4, 1.5
PowerMaxScale = 90, 114, 130
MinScale = 0.9, 0.885, 0.875
PowerMinScale = 10
RobScale = 0.95, 0.9286, 0.9167
TorqueDropOffRPM0 = 9000
TorqueDropOffRPM1 = 15000
TorqueDropOffScale0 = 1
TorqueDropOffScale1 = 0.75

[Aspiration.CSC]
MassDiff = 20, 20, 21
Price = 1900, 3800, 8600
ZeroRPMScale = 0.8924, 0.8878, 0.8847
RedlineRPMScale = 1.153, 1.2448, 1.306
RobScale = 0.9668, 0.9508, 0.9414
DragScale = 1
WindInstabilityScale = 1
PartsStringID = 0
TorqueDropOffRPM0 = 9000
TorqueDropOffRPM1 = 15000
TorqueDropOffScale0 = 1
TorqueDropOffScale1 = 0.75

[Aspiration.DSC]
MassDiff = 20, 20, 21
Price = 2200, 4400, 9900
ZeroRPMScale = 1.153, 1.2448, 1.306
RedlineRPMScale = 1.153, 1.2448, 1.306
RobScale = 0.9668, 0.9508, 0.9414
DragScale = 1
WindInstabilityScale = 1
PartsStringID = 0
TorqueDropOffRPM0 = 9000
TorqueDropOffRPM1 = 15000
TorqueDropOffScale0 = 1
TorqueDropOffScale1 = 0.75

[Aspiration.Intercooler]
MassDiff = 20, 27, 35
Price = 800, 1600, 3700
MaxScaleScale = 1.02, 1.05, 1.09
DragScale = 1
WindInstabilityScale = 1
PartsStringID = 0

[List_UpgradeAntiSwayFront]
StockLevel = 1
MassDiff = 0, -0.052%, -0.153%
Price = 0, 1100, 2500
Physics.DefSwaybarStiffness = stock, x2.313, x3.845
Physics.MinSwaybarStiffness = =DefSwaybarStiffness, =DefSwaybarStiffness, 1
Physics.MaxSwaybarStiffness = =DefSwaybarStiffness, =DefSwaybarStiffness, 40

[List_UpgradeAntiSwayRear]
StockLevel = 1
MassDiff = 0, -0.056%, -0.167%
Price = 0, 1100, 2500
Physics.DefSwaybarStiffness = stock, x2.671, x4.621
Physics.MinSwaybarStiffness = =DefSwaybarStiffness, =DefSwaybarStiffness, 1
Physics.MaxSwaybarStiffness = =DefSwaybarStiffness, =DefSwaybarStiffness, 40

[List_UpgradeSpringDamper]
StockLevel = 1
MassDiff = 0, -0.043%, -0.12%
Price = 0, 2700, 6200
Front.DefRideHeight = stock, x0.89, x0.775
Front.MinRideHeight = =DefRideHeight, =DefRideHeight, x0.72
Front.MaxRideHeight = =DefRideHeight, =DefRideHeight, x1
Front.DefSpringRate = stock, x1.737, x2.228
Front.MinSpringRate = =DefSpringRate, =DefSpringRate, 20
Front.MaxSpringRate = =DefSpringRate, =DefSpringRate, 150
Front.DefDampenBumpRate = stock, x1.446, x1.973
Front.MinDampenBumpRate = =DefDampenBumpRate, =DefDampenBumpRate, 1
Front.MaxDampenBumpRate = =DefDampenBumpRate, =DefDampenBumpRate, 12
Front.DefDampenReboundRate = stock, x1.437, x1.728
Front.MinDampenReboundRate = =DefDampenReboundRate, =DefDampenReboundRate, 1
Front.MaxDampenReboundRate = =DefDampenReboundRate, =DefDampenReboundRate, 12
Rear.DefRideHeight = stock, x0.89, x0.775
Rear.MinRideHeight = =DefRideHeight, =DefRideHeight, x0.72
Rear.MaxRideHeight = =DefRideHeight, =DefRideHeight, x1
Rear.DefSpringRate = stock, x1.935, x2.559
Rear.MinSpringRate = =DefSpringRate, =DefSpringRate, 20
Rear.MaxSpringRate = =DefSpringRate, =DefSpringRate, 150
Rear.DefDampenBumpRate = stock, x1.559, x2.197
Rear.MinDampenBumpRate = =DefDampenBumpRate, =DefDampenBumpRate, 1
Rear.MaxDampenBumpRate = =DefDampenBumpRate, =DefDampenBumpRate, 12
Rear.DefDampenReboundRate = stock, x1.549, x1.914
Rear.MinDampenReboundRate = =DefDampenReboundRate, =DefDampenReboundRate, 1
Rear.MaxDampenReboundRate = =DefDampenReboundRate, =DefDampenReboundRate, 12

[List_UpgradeBrakes]
StockLevel = 0
MassDiff = 0, -2, -4, -5
Price = 0, 1600, 3300, 7400
FrontBrakeSize = stock, +0, +1, +2
FrontBrakeTypeID = stock, stock, 4, 5
FrontCaliperPistons = stock, stock, 4, 6
RearBrakeSize = stock, +0, +1, +2
RearBrakeTypeID = stock, stock, 4, 5
RearCaliperPistons = stock, stock, stock, 4
GameFrictionScaleBraking = 1, 1.01, 1.03, 1.05
ReleasePointABSRearFullSteer = stock, 1, 1, 1

[List_UpgradeCarBodyChassisStiffness]
StockLevel = 0
MassDiff = 0, 1.1%, 2.42%, 4.4%
WeightDistDiff = 0, -0.0035, -0.007, -0.0105
Price = 0, 500, 1100, 2500
FrontLatFrictionScale = 0.98, 0.986, 0.992, 1
RearLatFrictionScale = 0.99, 0.993, 0.996, 1
FrontLongFrictionScale = 0.98, 0.986, 0.992, 1
RearLongFrictionScale = 0.99, 0.993, 0.996, 1

[List_UpgradeCarBodyWeight]
StockLevel = 0
Mass = stock, x0.97622, x0.91676, x0.82162
Price = 0, 500, 1100, 2500
CMHeight = stock, x0.98398, x0.96796, x0.95193
CMBackFront = stock, toward 0.5 0.125, toward 0.5 0.25, toward 0.5 0.375

[List_UpgradeCarBodyTireWidthFront]
; FM4: 10 mm steps; here up to 30% over the stock width
StockLevel = 0
Levels = upto FrontTireWidth x1.3
FrontTireWidth = stock, +10, +20
Price = 0, 2100, 2300
MassDiff = 0, 0.54, 1.08

[List_UpgradeCarBodyTireWidthRear]
StockLevel = 0
Levels = upto RearTireWidth x1.3
RearTireWidth = stock, +10, +20
Price = 0, 2400, 2600
MassDiff = 0, 0.54, 1.08

[List_UpgradeRimSizeFront]
StockLevel = 0
Levels = 2
FrontWheelDiameter = stock, +1, +2
Price = 0, 2000, 2200
MassDiff = 0, 1.36, 2.72

[List_UpgradeRimSizeRear]
StockLevel = 0
Levels = 2
RearWheelDiameter = stock, +1, +2
Price = 0, 2100, 2300
MassDiff = 0, 1.36, 2.72

[List_UpgradeEngineCamshaft]
StockLevel = 0
Price = 0, 2500, 4900, 11100
RedlineRPM = stock, x1.04615, x1.1, x1.14286
Curve.TorqueScale = stock, x1.02305, x1.05072, x1.08069

[List_UpgradeEngineDisplacement]
StockLevel = 0
Price = 0, 1900, 3800, 8600
TorqueScale = stock, x1.03804, x1.08453, x1.13525
Disp = stock, x1.02859, x1.06432, x1.14351

[List_UpgradeEngineExhaust]
StockLevel = 0
MassDiff = 0, -0.326%, -0.977%, -2.17%
Price = 0, 1600, 3300, 7400
TorqueScale = stock, x1.03135, x1.06717, x1.10748

[List_UpgradeEngineFlywheel]
StockLevel = 0
MassDiff = 0, -0.061%, -0.153%, -0.305%
Price = 0, 1100, 2200, 4900
MomentInertia = stock, x0.9, x0.75, x0.5

[List_UpgradeEngineFuelSystem]
StockLevel = 0
Price = 0, 1600, 3300, 7400
TorqueScale = stock, x1.02887, x1.06124, x1.09623

[List_UpgradeEngineIgnition]
StockLevel = 1
Price = 0, 2200, 4900
TorqueScale = stock, x1.03547, x1.05601

[List_UpgradeEngineIntake]
StockLevel = 0
MassDiff = 0, 0, -0.067%, -0.135%
Price = 0, 800, 1600, 3700
TorqueScale = stock, x1.02116, x1.04321, x1.06614

[List_UpgradeEngineIntercooler]
StockLevel = 1
MassDiff = 0, 7, 15
WeightDistDiff = 0, 0.0014, 0.003
Price = 0, 1600, 3700
MaxScaleScale = stock, x1.025, x1.06

[List_UpgradeEngineManifold]
StockLevel = 1
MassDiff = 0, 0, -0.437%
Price = 0, 2700, 6200
TorqueScale = stock, x1.03406, x1.05406

[List_UpgradeEngineOilCooling]
StockLevel = 0
MassDiff = 0, 0.088%, 0.439%, 0.853%
WeightDistDiff = 0, 0.00024, 0.0012, 0.00236
Price = 0, 300, 500, 1200
TorqueScale = stock, x1.01251, x1.02669, x1.04171

[List_UpgradeEnginePistonsCompression]
StockLevel = 1
MassDiff = 0, -0.033%, -0.089%
Price = 0, 4400, 9900
TorqueScale = stock, x1.04192, x1.06669

[List_UpgradeEngineValves]
StockLevel = 0
MassDiff = 0, -0.04, -0.2, -0.4
Price = 0, 1100, 2200, 4900
TorqueScale = stock, x1.02205, x1.04674, x1.07496

[List_UpgradeDrivetrainClutch]
StockLevel = 0
MassDiff = 0, -0.04%, -0.167%, -0.397%
Price = 0, 700, 1400, 3100
ClutchInTime = 0.15, x0.9, x0.8, x0.75
ClutchOutTime = 0.3, x0.9, x0.8, x0.75

[List_UpgradeDrivetrainTransmission]
; shift time only: gear ratios and final drive stay the stock ones
StockLevel = 0
MassDiff = 0, 0, -0.08%, -0.2%
Price = 0, 1400, 2700, 6200
MomentInertia = stock, x0.9, x0.8, x0.7
GearShiftTime = 0.2, x0.8, x0.55, x0.25

[List_UpgradeDrivetrainDifferential]
StockLevel = 1
MassDiff = 0, 0.2%, 0.2%
Price = 0, 2400, 5400
FrontLimitedSlipTorqueAccel = stock, toward 1000 0.45, 1000
RearLimitedSlipTorqueAccel = stock, toward 1500 0.45, 1500
RearLimitedSlipTorqueDecel = stock, =RearLimitedSlipTorqueAccel x0.25, =RearLimitedSlipTorqueAccel x0.5
CenterLimitedSlipTorqueAccel = stock, x1.514, x2.143

[List_UpgradeDrivetrainDriveline]
StockLevel = 0
MassDiff = 0, -0.071%, -0.259%, -0.471%
Price = 0, 400, 800, 1900
MomentInertia = stock, x0.9, x0.8, x0.7
";

        // How each table reaches the car, and the rows it links to (prefix in the ini, link column,
        // linked table and its key, id offset of the linked rows: rear = +100, torque curves = +1).
        private sealed class Link
        {
            public string Prefix;
            public string Column;
            public string Table;
            public string Key;
            public long Offset;

            public Link(string prefix, string column, string table, string key, long offset)
            {
                this.Prefix = prefix;
                this.Column = column;
                this.Table = table;
                this.Key = key;
                this.Offset = offset;
            }
        }

        private static Link[] LinksOf(string table)
        {
            switch (table)
            {
                case "List_UpgradeAntiSwayFront":
                    return new Link[] { new Link("Physics.", "AntiSwayPhysicsID", "List_AntiSwayPhysics", "AntiSwayPhysicsID", 0) };
                case "List_UpgradeAntiSwayRear":
                    return new Link[] { new Link("Physics.", "AntiSwayPhysicsID", "List_AntiSwayPhysics", "AntiSwayPhysicsID", 100) };
                case "List_UpgradeSpringDamper":
                    return new Link[]
                    {
                        new Link("Front.", "FrontSpringDamperPhysicsID", "List_SpringDamperPhysics", "SpringDamperPhysicsID", 0),
                        new Link("Rear.", "RearSpringDamperPhysicsID", "List_SpringDamperPhysics", "SpringDamperPhysicsID", 100),
                    };
                case "List_UpgradeEngineCamshaft":
                    return new Link[] { new Link("Curve.", "TorqueCurveFullThrottleID", "List_TorqueCurve", "TorqueCurveID", 1) };
                default:
                    return new Link[0];
            }
        }

        private readonly SqliteDatabase db;
        private readonly long carId;
        private readonly Dictionary<string, Dictionary<string, string>> ini;
        private readonly List<string> lines = new List<string>();
        private double curbKg;

        public CarUpgrades(SqliteDatabase db, long carId, Dictionary<string, Dictionary<string, string>> ini)
        {
            this.db = db;
            this.carId = carId;
            this.ini = ini;
        }

        public List<string> Lines
        {
            get { return this.lines; }
        }

        // The conversion the user picked for this car in the port window (AWD / RWD / FWD / none),
        // instead of the ini's rule; null = the rule.
        public string DriveOverride;

        // Only the drive type conversion: a car the Cars rule leaves out, with a conversion picked by hand.
        public void ApplyDrivetrainOnly()
        {
            object drivetrain = this.db.Scalar("SELECT DrivetrainID FROM List_UpgradeDrivetrain WHERE Ordinal = ? AND Level = 0", this.carId);
            if (drivetrain == null)
            {
                return;
            }
            if (Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM List_UpgradeDrivetrain WHERE DrivetrainID = ? AND Ordinal <> ?", drivetrain, this.carId)) > 0)
            {
                this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  drivetrain {0}: shared with other cars, no conversion", drivetrain));
                return;
            }
            this.DrivetrainSwap(Convert.ToInt64(drivetrain));
        }

        public static string IniPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName); }
        }

        // Upgrades.ini next to the exe; written with the defaults when missing (a read-only folder
        // just uses them).
        public static Dictionary<string, Dictionary<string, string>> LoadIni(List<string> notes)
        {
            string path = IniPath;
            string text = DefaultIni;
            if (File.Exists(path))
            {
                text = File.ReadAllText(path);
            }
            else
            {
                try
                {
                    File.WriteAllText(path, DefaultIni.Replace("\r\n", "\n").Replace("\n", "\r\n"), Encoding.UTF8);
                    notes.Add("upgrades: " + FileName + " written with the defaults (" + path + ")");
                }
                catch (IOException)
                {
                    notes.Add("upgrades: " + FileName + " could not be written, defaults used");
                }
                catch (UnauthorizedAccessException)
                {
                    notes.Add("upgrades: " + FileName + " could not be written, defaults used");
                }
            }
            // Sections an older file lacks (added to the defaults later) come from the defaults.
            Dictionary<string, Dictionary<string, string>> sections = ParseIni(text);
            foreach (KeyValuePair<string, Dictionary<string, string>> section in ParseIni(DefaultIni))
            {
                if (!sections.ContainsKey(section.Key))
                {
                    sections[section.Key] = section.Value;
                }
            }
            return sections;
        }

        public static Dictionary<string, Dictionary<string, string>> ParseIni(string text)
        {
            Dictionary<string, Dictionary<string, string>> sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> section = null;
            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw;
                int comment = line.IndexOf(';');
                if (comment >= 0)
                {
                    line = line.Substring(0, comment);
                }
                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    if (!sections.TryGetValue(name, out section))
                    {
                        section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        sections[name] = section;
                    }
                    continue;
                }
                int equals = line.IndexOf('=');
                if (equals > 0 && section != null)
                {
                    section[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }
            }
            return sections;
        }

        private string Setting(string section, string key, string fallback)
        {
            Dictionary<string, string> values;
            string value;
            return (this.ini.TryGetValue(section, out values) && values.TryGetValue(key, out value)) ? value : fallback;
        }

        // Whether the rules apply to this car: Cars = traffic (the port's placeholder close-up test),
        // missing (every car) or off.
        public bool AppliesTo(bool traffic)
        {
            string cars = this.Setting("General", "Cars", "traffic").ToLowerInvariant();
            return cars == "missing" || (cars == "traffic" && traffic);
        }

        public void Apply()
        {
            this.curbKg = Convert.ToDouble(this.db.Scalar("SELECT CurbWeight FROM Data_Car WHERE Id = ?", this.carId) ?? 0.0, CultureInfo.InvariantCulture) * 100;
            object engine = this.db.Scalar("SELECT EngineID FROM List_UpgradeEngine WHERE Ordinal = ? AND Level = 0", this.carId);
            object drivetrain = this.db.Scalar("SELECT DrivetrainID FROM List_UpgradeDrivetrain WHERE Ordinal = ? AND Level = 0", this.carId);
            // An engine or drivetrain another car also uses is FM4's own: its rows are left alone.
            bool ownEngine = engine != null && Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM List_UpgradeEngine WHERE EngineID = ? AND Ordinal <> ?", engine, this.carId)) == 0;
            bool ownDrivetrain = drivetrain != null && Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM List_UpgradeDrivetrain WHERE DrivetrainID = ? AND Ordinal <> ?", drivetrain, this.carId)) == 0;
            if (engine != null && !ownEngine)
            {
                this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  engine {0}: shared with other cars, its upgrades left as they are", engine));
            }
            if (drivetrain != null && !ownDrivetrain)
            {
                this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  drivetrain {0}: shared with other cars, its upgrades left as they are", drivetrain));
            }

            List<string> made = new List<string>();
            foreach (string table in this.ini.Keys)
            {
                if (!table.StartsWith("List_Upgrade", StringComparison.Ordinal) || !this.TableExists(table))
                {
                    continue;
                }
                List<string> columns = this.Columns(table);
                string parent;
                long parentId;
                if (Has(columns, "Ordinal"))
                {
                    parent = "Ordinal";
                    parentId = this.carId;
                }
                else if (Has(columns, "EngineID"))
                {
                    if (!ownEngine)
                    {
                        continue;
                    }
                    parent = "EngineID";
                    parentId = Convert.ToInt64(engine);
                }
                else if (Has(columns, "DrivetrainID"))
                {
                    if (!ownDrivetrain)
                    {
                        continue;
                    }
                    parent = "DrivetrainID";
                    parentId = Convert.ToInt64(drivetrain);
                }
                else if (Has(columns, "CarBodyID"))
                {
                    parent = "CarBodyID";
                    parentId = this.carId * 1000;
                }
                else
                {
                    continue;
                }
                string result = this.Generate(table, parent, parentId);
                if (result != null)
                {
                    made.Add(result);
                }
            }
            if (made.Count > 0)
            {
                this.lines.Insert(0, "upgrades: generic levels (" + FileName + ") for " + made.Count + " parts that had only the stock one: " + string.Join(", ", made.ToArray()));
            }
            else
            {
                this.lines.Insert(0, "upgrades: every part already has its upgrades, none generated");
            }
            if (ownEngine)
            {
                this.Aspiration(Convert.ToInt64(engine));
            }
            if (ownDrivetrain)
            {
                this.DrivetrainSwap(Convert.ToInt64(drivetrain));
            }
        }

        // Turbo / supercharger conversions for an engine that has none (user 2026-10-20: the FH1 Focus
        // had no aspiration swap). Rows at levels 1-3, Id = EngineID * 1000 + level (intercooler: + level
        // - 1, as FM4's), the values of [Aspiration.<type>].
        private void Aspiration(long engine)
        {
            foreach (string table in new string[] { "List_UpgradeEngineTurboSingle", "List_UpgradeEngineTurboTwin", "List_UpgradeEngineTurboQuad", "List_UpgradeEngineCSC", "List_UpgradeEngineDSC" })
            {
                if (this.TableExists(table) && Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM " + table + " WHERE EngineID = ?", engine)) > 0)
                {
                    this.lines.Add("  aspiration: the engine has its own turbo / supercharger rows, none added");
                    return;
                }
            }
            List<string> made = new List<string>();
            List<string> types = new List<string>();
            foreach (string type in this.Setting("Aspiration", "Types", "").Split(','))
            {
                if (type.Trim().Length > 0)
                {
                    types.Add(type.Trim());
                }
            }
            object cylinders = this.db.Scalar("SELECT c.Number FROM Data_Engine e JOIN List_Cylinders c ON c.CylinderID = e.CylinderID WHERE e.EngineID = ?", engine);
            long minimum = ParseLong(this.Setting("Aspiration", "TwinTurboMinCylinders", "6"));
            if (cylinders != null && Convert.ToInt64(cylinders) < minimum && types.Exists(delegate(string t) { return string.Equals(t, "TurboTwin", StringComparison.OrdinalIgnoreCase); }))
            {
                types.RemoveAll(delegate(string t) { return string.Equals(t, "TurboTwin", StringComparison.OrdinalIgnoreCase); });
                this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  aspiration: {0} cylinders, no twin turbo (under {1})", cylinders, minimum));
            }
            if (types.Count == 0)
            {
                return;
            }
            if (!this.TableExists("List_UpgradeEngineIntercooler") || Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM List_UpgradeEngineIntercooler WHERE EngineID = ?", engine)) == 0)
            {
                types.Add("Intercooler");
            }
            object placement = this.db.Scalar("SELECT EnginePlacementID FROM Data_Car WHERE Id = ?", this.carId);
            double side = (placement != null && Convert.ToInt64(placement) == 1) ? 0.3 : -0.3;
            string maker = this.Setting("General", "ManufacturerID", "0");
            foreach (string type in types)
            {
                string table = "List_UpgradeEngine" + type;
                Dictionary<string, string> values;
                if (!this.TableExists(table) || !this.ini.TryGetValue("Aspiration." + type, out values))
                {
                    continue;
                }
                List<string> columns = this.Columns(table);
                List<string> integer = this.IntegerColumns(table);
                long offset = (type == "Intercooler") ? -1 : 0;
                for (long level = 1; level <= 3; level++)
                {
                    object[] row = new object[columns.Count];
                    for (int c = 0; c < columns.Count; c++)
                    {
                        string column = columns[c];
                        string spec;
                        switch (column.ToLowerInvariant())
                        {
                            case "id": row[c] = (engine * 1000) + level + offset; break;
                            case "engineid": row[c] = engine; break;
                            case "level": row[c] = level; break;
                            case "isstock": row[c] = 0L; break;
                            case "manufacturerid": row[c] = ParseLong(values.ContainsKey(column) ? values[column] : maker); break;
                            default:
                                if (values.TryGetValue(column, out spec))
                                {
                                    string[] parts = spec.Split(',');
                                    row[c] = Store(ParseDouble(parts[Math.Min((int) level - 1, parts.Length - 1)]), integer.Contains(column.ToLowerInvariant()));
                                }
                                else
                                {
                                    row[c] = 0L;
                                }
                                break;
                        }
                    }
                    int massIndex = columns.FindIndex(delegate(string c) { return string.Equals(c, "MassDiff", StringComparison.OrdinalIgnoreCase); });
                    int distIndex = columns.FindIndex(delegate(string c) { return string.Equals(c, "WeightDistDiff", StringComparison.OrdinalIgnoreCase); });
                    if (massIndex >= 0 && distIndex >= 0 && this.curbKg > 0)
                    {
                        row[distIndex] = Math.Round(ToDouble(row[massIndex]) * side / this.curbKg, 6);
                    }
                    if (this.KeyExists(table, columns[0], Convert.ToInt64(row[0])))
                    {
                        this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  aspiration: {0} id {1} already taken, {2} left out", table, row[0], type));
                        break;
                    }
                    this.Insert(table, columns.ToArray(), row);
                }
                made.Add(type);
            }
            if (made.Count > 0)
            {
                this.lines.Add("  aspiration: naturally aspirated engine " + engine + ", conversions added (levels 1-3): " + string.Join(", ", made.ToArray()));
            }
        }

        // One table: only when its single row for the car is the stock part. The stock row moves to
        // StockLevel, the levels above it are copies of it with the ini's values.
        private string Generate(string table, string parent, long parentId)
        {
            Dictionary<string, string> rules = this.ini[table];
            List<string> columns = this.Columns(table);
            string where = SqliteDatabase.Quote(parent) + " = " + parentId.ToString(CultureInfo.InvariantCulture);
            SqliteDatabase.Result rows = this.db.Query("SELECT * FROM " + SqliteDatabase.Quote(table) + " WHERE " + where);
            if (rows.Rows.Count != 1 || !Has(columns, "Level") || !Has(columns, "IsStock") || Convert.ToInt64(Get(rows.Columns, rows.Rows[0], "IsStock")) != 1)
            {
                return null;
            }
            object[] stock = rows.Rows[0];
            string[] names = rows.Columns;
            long stockLevel = ParseLong(this.Setting(table, "StockLevel", "0"));
            long sourceLevel = Convert.ToInt64(Get(names, stock, "Level"));
            long stockId = Convert.ToInt64(stock[0]);
            long baseId = stockId - (stockId % 1000);

            // The levels: StockLevel .. 3, or "Levels = n" / "Levels = upto Column xK".
            string levelsRule = this.Setting(table, "Levels", null);
            int count = (int) (3 - stockLevel);
            string uptoColumn = null;
            double uptoFactor = 0;
            if (levelsRule != null)
            {
                string[] parts = levelsRule.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3 && string.Equals(parts[0], "upto", StringComparison.OrdinalIgnoreCase) && parts[2].StartsWith("x", StringComparison.OrdinalIgnoreCase))
                {
                    uptoColumn = parts[1];
                    uptoFactor = ParseDouble(parts[2].Substring(1));
                    count = 10;
                }
                else
                {
                    count = (int) ParseLong(levelsRule);
                }
            }

            // Values of the main row and of each linked row, level by level (index 0 = stock level).
            Link[] links = LinksOf(table);
            List<object[]> mainRows = this.BuildLevels(table, names, stock, rules, "", count + 1);
            List<List<object[]>> linkedRows = new List<List<object[]>>();
            List<object[]> linkedStock = new List<object[]>();
            List<string[]> linkedNames = new List<string[]>();
            foreach (Link link in links)
            {
                object id = Get(names, stock, link.Column);
                SqliteDatabase.Result linked = (id == null) ? null : this.db.Query("SELECT * FROM " + SqliteDatabase.Quote(link.Table) + " WHERE " + SqliteDatabase.Quote(link.Key) + " = ?", id);
                if (linked == null || linked.Rows.Count != 1)
                {
                    this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  {0}: its {1} row {2} is missing, left as it is", table, link.Table, id));
                    return null;
                }
                linkedNames.Add(linked.Columns);
                linkedStock.Add(linked.Rows[0]);
                linkedRows.Add(this.BuildLevels(link.Table, linked.Columns, linked.Rows[0], rules, link.Prefix, count + 1));
            }

            // Levels stop where the "upto" column passes its limit.
            int levels = count;
            if (uptoColumn != null)
            {
                double limit = ToDouble(Get(names, mainRows[0], uptoColumn)) * uptoFactor;
                levels = 0;
                while (levels < count && ToDouble(Get(names, mainRows[levels + 1], uptoColumn)) <= limit + 1e-9)
                {
                    levels++;
                }
            }
            if (levels <= 0)
            {
                return null;
            }

            // Ids: the stock row keeps its own; level n = parent * 1000 + n (FM4's rule).
            for (int n = 1; n <= levels; n++)
            {
                long id = baseId + stockLevel + n;
                if (Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM " + SqliteDatabase.Quote(table) + " WHERE " + SqliteDatabase.Quote(names[0]) + " = ?", id)) > 0)
                {
                    this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  {0}: id {1} already taken, left as it is", table, id));
                    return null;
                }
            }

            // Linked rows: stock one renumbered to its level if that id is free (FM4: car*1000 + level,
            // rear +100, curves EngineID*1000 + level + 1), new ones after it.
            for (int k = 0; k < links.Length; k++)
            {
                Link link = links[k];
                long oldId = Convert.ToInt64(linkedStock[k][0]);
                long linkBase = oldId - (oldId % 1000) + link.Offset;
                List<long> ids = new List<long>();
                long stockLinkId = oldId;
                long wanted = (link.Offset == 1) ? oldId : linkBase + stockLevel;
                if (wanted != oldId && !this.LinkTaken(link, wanted))
                {
                    stockLinkId = wanted;
                }
                ids.Add(stockLinkId);
                for (int n = 1; n <= levels; n++)
                {
                    long id = linkBase + stockLevel + n;
                    while ((id != stockLinkId && id != oldId && this.LinkTaken(link, id)) || ids.Contains(id) || (id == oldId && stockLinkId == oldId))
                    {
                        id += 10;
                    }
                    ids.Add(id);
                }
                for (int n = 0; n <= levels; n++)
                {
                    Set(linkedNames[k], linkedRows[k][n], link.Key, ids[n]);
                    Set(names, mainRows[n], link.Column, ids[n]);
                }
                if (link.Table == "List_TorqueCurve")
                {
                    double redline = ToDouble(Get(names, mainRows[0], "RedlineRPM"));
                    for (int n = 1; n <= levels; n++)
                    {
                        double ratio = (redline > 0) ? ToDouble(Get(names, mainRows[n], "RedlineRPM")) / redline : 1;
                        Stretch(linkedNames[k], linkedRows[k][n], ratio);
                    }
                }
                this.Replace(link.Table, link.Key, oldId, linkedNames[k], linkedRows[k][0]);
                for (int n = 1; n <= levels; n++)
                {
                    this.Insert(link.Table, linkedNames[k], linkedRows[k][n]);
                }
            }

            // Main rows.
            string maker = this.Setting(table, "ManufacturerID", this.Setting("General", "ManufacturerID", "0"));
            Set(names, mainRows[0], "Level", stockLevel);
            this.Replace(table, names[0], stockId, names, mainRows[0]);
            for (int n = 1; n <= levels; n++)
            {
                object[] row = mainRows[n];
                Set(names, row, names[0], baseId + stockLevel + n);
                Set(names, row, "Level", stockLevel + n);
                Set(names, row, "IsStock", 0L);
                if (!rules.ContainsKey("ManufacturerID"))
                {
                    Set(names, row, "ManufacturerID", ParseLong(maker));
                }
                this.Insert(table, names, row);
            }
            string shortName = table.StartsWith("List_Upgrade", StringComparison.Ordinal) ? table.Substring(12) : table;
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}{2}-{3}", shortName, (sourceLevel != stockLevel) ? "stock " + sourceLevel + "->" : "", stockLevel, stockLevel + levels);
        }

        // Whether a linked row id is in use. A row no upgrade points at any more (left by an earlier
        // install of the same car, whose upgrade rows the port replaced) is deleted and the id reused.
        private bool LinkTaken(Link link, long id)
        {
            if (!this.KeyExists(link.Table, link.Key, id))
            {
                return false;
            }
            string[][] users = (link.Table == "List_AntiSwayPhysics")
                ? new string[][] { new string[] { "List_UpgradeAntiSwayFront", "AntiSwayPhysicsID" }, new string[] { "List_UpgradeAntiSwayRear", "AntiSwayPhysicsID" } }
                : (link.Table == "List_SpringDamperPhysics")
                ? new string[][] { new string[] { "List_UpgradeSpringDamper", "FrontSpringDamperPhysicsID" }, new string[] { "List_UpgradeSpringDamper", "RearSpringDamperPhysicsID" } }
                : new string[][] { new string[] { "List_UpgradeEngineCamshaft", "TorqueCurveFullThrottleID" }, new string[] { "List_UpgradeEngineCamshaft", "TorqueCurveZeroThrottleID" } };
            foreach (string[] user in users)
            {
                if (this.TableExists(user[0]) && Has(this.Columns(user[0]), user[1])
                    && Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM " + SqliteDatabase.Quote(user[0]) + " WHERE " + SqliteDatabase.Quote(user[1]) + " = ?", id)) > 0)
                {
                    return true;
                }
            }
            this.db.Execute("DELETE FROM " + SqliteDatabase.Quote(link.Table) + " WHERE " + SqliteDatabase.Quote(link.Key) + " = ?", id);
            this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  {0} {1}: left by an earlier install, unused, replaced", link.Table, id));
            return false;
        }

        // count rows (index 0 = the stock level) from the stock row and the rules of one prefix.
        private List<object[]> BuildLevels(string table, string[] names, object[] stock, Dictionary<string, string> rules, string prefix, int count)
        {
            List<string> integer = this.IntegerColumns(table);
            List<object[]> result = new List<object[]>();
            for (int n = 0; n < count; n++)
            {
                result.Add((object[]) stock.Clone());
            }
            // The stock level first, then the levels above it (their x / + / toward start from it);
            // in each, fixed values before references (=Column).
            Dictionary<string, double[]> computed = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            for (int phase = 0; phase < 4; phase++)
            {
                bool references = (phase % 2) == 1;
                int firstLevel = (phase < 2) ? 0 : 1;
                int lastLevel = (phase < 2) ? 0 : count - 1;
                foreach (KeyValuePair<string, string> rule in rules)
                {
                    string column = rule.Key;
                    if (prefix.Length > 0)
                    {
                        if (!column.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        column = column.Substring(prefix.Length);
                    }
                    else if (column.IndexOf('.') >= 0 || string.Equals(column, "StockLevel", StringComparison.OrdinalIgnoreCase) || string.Equals(column, "Levels", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    int index = IndexOf(names, column);
                    if (index < 0)
                    {
                        continue;
                    }
                    string[] specs = rule.Value.Split(',');
                    double[] values;
                    if (!computed.TryGetValue(rule.Key, out values))
                    {
                        values = new double[count];
                        computed[rule.Key] = values;
                    }
                    for (int n = firstLevel; n <= lastLevel; n++)
                    {
                        string spec = (n < specs.Length) ? specs[n].Trim() : null;
                        bool reference = (spec ?? specs[specs.Length - 1].Trim()).StartsWith("=", StringComparison.Ordinal);
                        if (reference != references)
                        {
                            continue;
                        }
                        if (spec == null && !reference)
                        {
                            // Past the list: the step between its last two values.
                            values[n] = (n >= 2) ? values[n - 1] + (values[n - 1] - values[n - 2]) : values[n - 1];
                        }
                        else
                        {
                            double basis = (n == 0) ? ToDouble(stock[index]) : ToDouble(result[0][index]);
                            values[n] = this.Evaluate(spec ?? specs[specs.Length - 1].Trim(), basis, names, result[n]);
                        }
                        result[n][index] = Store(values[n], integer.Contains(names[index].ToLowerInvariant()));
                    }
                }
            }
            return result;
        }

        private double Evaluate(string spec, double basis, string[] names, object[] row)
        {
            string s = spec.Trim();
            if (string.Equals(s, "stock", StringComparison.OrdinalIgnoreCase))
            {
                return basis;
            }
            if (s.StartsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                return basis * ParseDouble(s.Substring(1));
            }
            if (s.StartsWith("+", StringComparison.Ordinal))
            {
                return basis + ParseDouble(s.Substring(1));
            }
            if (s.StartsWith("=", StringComparison.Ordinal))
            {
                string[] parts = s.Substring(1).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                double value = ToDouble(Get(names, row, parts[0]));
                if (parts.Length > 1 && parts[1].StartsWith("x", StringComparison.OrdinalIgnoreCase))
                {
                    value *= ParseDouble(parts[1].Substring(1));
                }
                return value;
            }
            if (s.StartsWith("toward", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                double target = ParseDouble(parts[1]);
                return basis + ((target - basis) * ParseDouble(parts[2]));
            }
            if (s.EndsWith("%", StringComparison.Ordinal))
            {
                return this.curbKg * ParseDouble(s.Substring(0, s.Length - 1)) / 100;
            }
            return ParseDouble(s);
        }

        // A torque curve stretched along the rpm axis by ratio (the camshaft's new redline over the
        // stock one): FM4's cam curves are its stock curve moved that way (TOY_SupraRZ_98 215000..215004).
        private static void Stretch(string[] names, object[] row, double ratio)
        {
            int first = IndexOf(names, "v0");
            int count = (int) ToDouble(Get(names, row, "NumTorqueValues"));
            if (first < 0 || count < 2 || ratio <= 0)
            {
                return;
            }
            double[] values = new double[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = ToDouble(row[first + i]);
            }
            for (int i = 0; i < count; i++)
            {
                double at = i / ratio;
                int low = Math.Min((int) Math.Floor(at), count - 1);
                int high = Math.Min(low + 1, count - 1);
                row[first + i] = values[low] + ((values[high] - values[low]) * (at - low));
            }
        }

        // The user's conversion rule (2026-10-20): from the stock drive type to the one in the ini,
        // for a car without a conversion. As FM4's: a List_UpgradeDrivetrain row at level 1 with its
        // own Data_Drivetrain and parts. Here those are copies of the stock drivetrain's (same gears,
        // levels and clutch), the differential set for the new type: AWD gets a centre diff of
        // rear * (1 - split) (FM4: 350/700 at 0.5, 255/850 at 0.7), RWD/FWD none.
        private void DrivetrainSwap(long stockDrivetrain)
        {
            this.RemoveOldConversions();
            if (Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM List_UpgradeDrivetrain WHERE Ordinal = ? AND Level > 0", this.carId)) > 0)
            {
                this.lines.Add("  drivetrain: the car has its own conversion, kept");
                return;
            }
            object type = this.db.Scalar("SELECT DrivetypeID FROM Data_Drivetrain WHERE DrivetrainID = ?", stockDrivetrain);
            if (type == null)
            {
                return;
            }
            string from = TypeName(Convert.ToInt64(type));
            string to = (this.DriveOverride ?? this.Setting("Drivetrain swap", from, "")).ToUpperInvariant();
            long toType = TypeId(to);
            if (toType == 0 || toType == Convert.ToInt64(type))
            {
                this.lines.Add("  drivetrain: " + from + ", no conversion (" + ((this.DriveOverride != null) ? "port window" : FileName) + ")");
                return;
            }
            object placement = this.db.Scalar("SELECT EnginePlacementID FROM Data_Car WHERE Id = ?", this.carId);
            // Powertrains.DrivetrainId holds the drive type (1 FWD, 2 RWD, 3 AWD), not a Data_Drivetrain id.
            object powertrain = this.db.Scalar("SELECT Id FROM Powertrains WHERE DrivetrainId = ? AND EnginePlacementId = ?", toType, placement);
            if (powertrain == null)
            {
                this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  drivetrain: no FM4 powertrain for {0} with engine placement {1}, no conversion", to, placement));
                return;
            }
            long id = 100000 + this.carId;
            while (Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM Data_Drivetrain WHERE DrivetrainID = ?", id)) > 0)
            {
                id += 100000;
            }
            long rowId = (this.carId * 1000) + 1;
            if (Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM List_UpgradeDrivetrain WHERE Id = ?", rowId)) > 0)
            {
                this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  drivetrain: id {0} already taken, no conversion", rowId));
                return;
            }

            SqliteDatabase.Result data = this.db.Query("SELECT * FROM Data_Drivetrain WHERE DrivetrainID = ?", stockDrivetrain);
            object[] dataRow = (object[]) data.Rows[0].Clone();
            Set(data.Columns, dataRow, "DrivetrainID", id);
            Set(data.Columns, dataRow, "DrivetypeID", toType);
            this.Insert("Data_Drivetrain", data.Columns, dataRow);
            // Log lines in the port's format, so Uninstall removes them with the car.
            this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  Data_Drivetrain: 1 rows (DrivetrainID {0})", id));

            double split = ParseDouble(this.Setting("Drivetrain swap", to + ".RearToqueSplit", "0.5"));
            foreach (string table in new string[] { "List_UpgradeDrivetrainTransmission", "List_UpgradeDrivetrainClutch", "List_UpgradeDrivetrainDifferential", "List_UpgradeDrivetrainDriveline" })
            {
                SqliteDatabase.Result parts = this.db.Query("SELECT * FROM " + table + " WHERE DrivetrainID = ?", stockDrivetrain);
                foreach (object[] part in parts.Rows)
                {
                    object[] row = (object[]) part.Clone();
                    long old = Convert.ToInt64(row[0]);
                    Set(parts.Columns, row, parts.Columns[0], (id * 1000) + (old % 1000));
                    Set(parts.Columns, row, "DrivetrainID", id);
                    if (table == "List_UpgradeDrivetrainDifferential")
                    {
                        bool awd = toType == 3;
                        double rear = ToDouble(Get(parts.Columns, row, "RearLimitedSlipTorqueAccel"));
                        Set(parts.Columns, row, "CenterLimitedSlipTorqueAccel", awd ? Math.Round(rear * (1 - split), 4) : 0.0);
                        Set(parts.Columns, row, "CenterLimitedSlipTorqueDecel", 0.0);    // 0 on every FM4 row
                        Set(parts.Columns, row, "RearToqueSplit", awd ? split : 0.5);
                    }
                    this.Insert(table, parts.Columns, row);
                }
                if (parts.Rows.Count > 0)
                {
                    this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  {0}: {1} rows (DrivetrainID {2})", table, parts.Rows.Count, id));
                }
            }

            SqliteDatabase.Result upgrade = this.db.Query("SELECT * FROM List_UpgradeDrivetrain WHERE Ordinal = ? AND Level = 0", this.carId);
            object[] swap = (object[]) upgrade.Rows[0].Clone();
            Set(upgrade.Columns, swap, "Id", rowId);
            Set(upgrade.Columns, swap, "DrivetrainID", id);
            Set(upgrade.Columns, swap, "PowertrainId", Convert.ToInt64(powertrain));
            Set(upgrade.Columns, swap, "Level", 1L);
            Set(upgrade.Columns, swap, "IsStock", 0L);
            Set(upgrade.Columns, swap, "MassDiff", ParseDouble(this.Setting("Drivetrain swap", to + ".MassDiff", "0")));
            Set(upgrade.Columns, swap, "WeightDistDiff", ParseDouble(this.Setting("Drivetrain swap", to + ".WeightDistDiff", "0")));
            Set(upgrade.Columns, swap, "Price", ParseLong(this.Setting("Drivetrain swap", to + ".Price", "6800")));
            this.Insert("List_UpgradeDrivetrain", upgrade.Columns, swap);
            this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  drivetrain: {0} -> {1} conversion added (level 1, drivetrain {2}, powertrain {3}, stock gears, rear split {4})", from, to, id, powertrain, toType == 3 ? split.ToString(CultureInfo.InvariantCulture) : "-"));
        }

        // Conversions earlier installs of this car made (ids k * 100000 + car) that no List_UpgradeDrivetrain
        // row lists any more (the port replaced the car's rows): their drivetrain and parts go.
        public void RemoveOldConversions()
        {
            for (long k = 1; k <= 9; k++)
            {
                long old = (k * 100000) + this.carId;
                if (Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM Data_Drivetrain WHERE DrivetrainID = ?", old)) == 0
                    || Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM List_UpgradeDrivetrain WHERE DrivetrainID = ?", old)) > 0)
                {
                    continue;
                }
                this.db.Execute("DELETE FROM Data_Drivetrain WHERE DrivetrainID = ?", old);
                foreach (string table in new string[] { "List_UpgradeDrivetrainTransmission", "List_UpgradeDrivetrainClutch", "List_UpgradeDrivetrainDifferential", "List_UpgradeDrivetrainDriveline" })
                {
                    this.db.Execute("DELETE FROM " + table + " WHERE DrivetrainID = ?", old);
                }
                this.lines.Add(string.Format(CultureInfo.InvariantCulture, "  drivetrain {0}: conversion of an earlier install, removed", old));
            }
        }

        private static string TypeName(long type)
        {
            return (type == 1) ? "FWD" : (type == 2) ? "RWD" : (type == 3) ? "AWD" : type.ToString(CultureInfo.InvariantCulture);
        }

        private static long TypeId(string name)
        {
            return (name == "FWD") ? 1 : (name == "RWD") ? 2 : (name == "AWD") ? 3 : 0;
        }

        #region Rows

        private bool TableExists(string table)
        {
            return Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = ?", table)) > 0;
        }

        private List<string> Columns(string table)
        {
            List<string> columns = new List<string>();
            foreach (object[] column in this.db.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")").Rows)
            {
                columns.Add((string) column[1]);
            }
            return columns;
        }

        // Lower-case names of the INTEGER columns: their values are written as whole numbers.
        private List<string> IntegerColumns(string table)
        {
            List<string> columns = new List<string>();
            foreach (object[] column in this.db.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")").Rows)
            {
                string type = (column[2] as string ?? "").ToUpperInvariant();
                if (type.Contains("INT"))
                {
                    columns.Add(((string) column[1]).ToLowerInvariant());
                }
            }
            return columns;
        }

        private bool KeyExists(string table, string key, long id)
        {
            return Convert.ToInt64(this.db.Scalar("SELECT count(*) FROM " + SqliteDatabase.Quote(table) + " WHERE " + SqliteDatabase.Quote(key) + " = ?", id)) > 0;
        }

        private void Insert(string table, string[] names, object[] row)
        {
            string[] quoted = new string[names.Length];
            string[] marks = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                quoted[i] = SqliteDatabase.Quote(names[i]);
                marks[i] = "?";
            }
            this.db.Execute("INSERT INTO " + SqliteDatabase.Quote(table) + " (" + string.Join(", ", quoted) + ") VALUES (" + string.Join(", ", marks) + ")", row);
        }

        // The row whose key is oldKey, written with every value of row (its key may change).
        private void Replace(string table, string key, long oldKey, string[] names, object[] row)
        {
            string[] sets = new string[names.Length];
            object[] args = new object[names.Length + 1];
            for (int i = 0; i < names.Length; i++)
            {
                sets[i] = SqliteDatabase.Quote(names[i]) + " = ?";
                args[i] = row[i];
            }
            args[names.Length] = oldKey;
            this.db.Execute("UPDATE " + SqliteDatabase.Quote(table) + " SET " + string.Join(", ", sets) + " WHERE " + SqliteDatabase.Quote(key) + " = ?", args);
        }

        private static bool Has(List<string> columns, string name)
        {
            return columns.Exists(delegate(string c) { return string.Equals(c, name, StringComparison.OrdinalIgnoreCase); });
        }

        private static int IndexOf(string[] names, string name)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static object Get(string[] names, object[] row, string name)
        {
            int index = IndexOf(names, name);
            return (index < 0) ? null : row[index];
        }

        private static void Set(string[] names, object[] row, string name, object value)
        {
            int index = IndexOf(names, name);
            if (index >= 0)
            {
                row[index] = value;
            }
        }

        private static object Store(double value, bool integer)
        {
            if (integer)
            {
                return (long) Math.Round(value);
            }
            return Math.Round(value, 6);
        }

        private static double ToDouble(object value)
        {
            return (value == null || value is string) ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string text)
        {
            return double.Parse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static long ParseLong(string text)
        {
            return (long) Math.Round(ParseDouble(text));
        }

        #endregion
    }
}
