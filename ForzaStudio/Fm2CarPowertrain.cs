namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    // FM2 engine and drivetrain -> FM4 rows of their own for the car (FM2 -> FM4 port, 2026-10-20, rule of
    // the user: the car gets a drivetrain and an engine with its Id, parts Id = car * 1000 + level, like the
    // FM4 cars; no FM4 engine is reused). Call it with the car's FINAL Id in the target (after the renumber):
    // DrivetrainID and EngineID are the car's Id, which RenumberCar would not move.
    //
    // Drivetrain: Data_Drivetrain (drive type from FM2's Powertrains), List_UpgradeDrivetrain (stock row) and
    // the transmission / clutch / differential / driveline parts, which FM2 keeps per car.
    // Engine: Data_Engine and List_UpgradeEngine, the camshaft (FM2 CamValves: FM4 has no zero-throttle curve
    // Id, the zero-throttle curve's scale becomes List_TorqueCurve.ZeroThrottleTorqueScale: 137.964 in both
    // games on the Supra's 2JZ), the torque curves, displacement (FM2 "block"), intake, ignition, exhaust,
    // fuel system, intercooler, turbo (-> single), CSC, DSC, flywheel. FM4 parts FM2 has no source for
    // (valves, pistons / compression, manifold, oil cooling, restrictor plate) are left out: the game
    // offers only the parts a car has (the Supra has no manifold or restrictor plate rows either).
    public static class Fm2CarPowertrain
    {
        public static List<string> Apply(SqliteDatabase target, SqliteDatabase fm2, string mediaName)
        {
            List<string> log = new List<string>();
            object fromObj = fm2.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", mediaName);
            object toObj = target.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", mediaName);
            if (fromObj == null || toObj == null)
            {
                log.Add("Fm2CarPowertrain: " + mediaName + " not found in " + (fromObj == null ? "the FM2" : "the target") + " database");
                return log;
            }
            long from = Convert.ToInt64(fromObj), car = Convert.ToInt64(toObj), idBase = car * 1000;
            SqliteDatabase.Result carRow = fm2.Query("SELECT * FROM Data_Car WHERE Id = ?", from);
            Drivetrain(target, fm2, from, car, carRow, log);
            Engine(target, fm2, from, car, carRow, log);
            WeightDistribution(target, car, log);
            return log;
        }

        private static void Drivetrain(SqliteDatabase target, SqliteDatabase fm2, long from, long car, SqliteDatabase.Result carRow, List<string> log)
        {
            long id = car * 1000;
            object powertrain = Fm2CarUpgrades.Value(carRow, carRow.Rows[0], "PowertrainID");
            object driveType = fm2.Scalar("SELECT DrivetrainId FROM Powertrains WHERE Id = ?", powertrain);
            object placement = Fm2CarUpgrades.Value(carRow, carRow.Rows[0], "EnginePlacementID");
            // How the engine is mounted is not in FM2: the most common value of the FM4 cars with this drive type
            // and engine placement.
            object mounting = target.Scalar("SELECT d.EngineMountingDirection FROM Data_Car c JOIN List_UpgradeDrivetrain u ON u.Ordinal = c.Id AND u.IsStock = 1 JOIN Data_Drivetrain d ON d.DrivetrainID = u.DrivetrainID WHERE d.DrivetypeID = ? AND c.EnginePlacementID = ? AND c.Id <> ? GROUP BY d.EngineMountingDirection ORDER BY COUNT(*) DESC LIMIT 1", driveType, placement, car);
            target.Execute("DELETE FROM Data_Drivetrain WHERE DrivetrainID = ?", car);
            target.Execute("INSERT INTO Data_Drivetrain (DrivetrainID, DrivetypeID, EngineMountingDirection) VALUES (?, ?, ?)", car, driveType, mounting ?? (object) 0L);
            target.Execute("DELETE FROM List_UpgradeDrivetrain WHERE Ordinal = ?", car);
            target.Execute("INSERT INTO List_UpgradeDrivetrain (Id, Ordinal, DrivetrainID, PowertrainId, MassDiff, WeightDistDiff, Level, ManufacturerId, Price, IsStock) VALUES (?, ?, ?, ?, 0, 0, 0, ?, 0, 1)", id, car, car, powertrain, Fm2CarUpgrades.Value(carRow, carRow.Rows[0], "MakeID"));
            log.Add("Drivetrain " + car + ": type " + driveType + ", engine mounting " + mounting + " (most common of the FM4 cars with this drive and placement)");

            Dictionary<string, object> noGear = new Dictionary<string, object>();
            noGear["GearRatio8"] = -1.0;    // FM2 stops at GearRatio7 = -1; FM4's own rows use -1 for the unused ones
            noGear["CVTShiftTime"] = -1.0;
            Fm2CarUpgrades.CopyRows(target, fm2, "List_UpgradeTransmission", "Ordinal", from, "List_UpgradeDrivetrainTransmission", "DrivetrainID", car, id, null, noGear, log);
            Fm2CarUpgrades.CopyRows(target, fm2, "List_UpgradeClutch", "Ordinal", from, "List_UpgradeDrivetrainClutch", "DrivetrainID", car, id, null, log);
            Fm2CarUpgrades.CopyRows(target, fm2, "List_UpgradeDifferential", "Ordinal", from, "List_UpgradeDrivetrainDifferential", "DrivetrainID", car, id, null, log);
            Fm2CarUpgrades.CopyRows(target, fm2, "List_UpgradeDriveline", "Ordinal", from, "List_UpgradeDrivetrainDriveline", "DrivetrainID", car, id, null, log);
        }

        private static void Engine(SqliteDatabase target, SqliteDatabase fm2, long from, long car, SqliteDatabase.Result carRow, List<string> log)
        {
            long id = car * 1000;
            object stock = fm2.Scalar("SELECT EngineID FROM List_UpgradeEngine WHERE Ordinal = ? AND IsStock = 1 ORDER BY Level LIMIT 1", from);
            if (stock == null)
            {
                log.Add("Engine: the FM2 car has no stock engine row");
                return;
            }
            long engine = Convert.ToInt64(stock);
            SqliteDatabase.Result data = fm2.Query("SELECT * FROM Data_Engine WHERE EngineID = ?", engine);
            if (data.Rows.Count == 0)
            {
                log.Add("Engine " + engine + ": not in the FM2 Data_Engine");
                return;
            }

            // Data_Engine of the car's own.
            object config = Fm2CarUpgrades.Value(data, data.Rows[0], "ConfigID");
            object carbureted = fm2.Scalar("SELECT Carbureted FROM List_UpgradeEngineFuelSystem WHERE EngineID = ? AND IsStock = 1 LIMIT 1", engine);
            target.Execute("DELETE FROM Data_Engine WHERE EngineID = ?", car);
            Dictionary<string, object> extra = new Dictionary<string, object>();
            extra["EngineRotation"] = 0L;
            extra["Carbureted"] = carbureted ?? (object) 0L;
            extra["Diesel"] = 0L;
            extra["Rotary"] = (config != null && Convert.ToInt64(config) == 4) ? 1L : 0L;   // List_EngineConfig 4 = Rotary
            Fm2CarUpgrades.Insert(target, "Data_Engine", data, data.Rows[0], "EngineID", car, extra);
            log.Add("Data_Engine " + car + ": " + Fm2CarUpgrades.Value(data, data.Rows[0], "EngineName") + " (FM2 engine " + engine + ")");

            // List_UpgradeEngine: the stock engine under the car's Id; a swap stays only when FM4 has that very
            // engine (same id and name), the others have no FM4 data.
            target.Execute("DELETE FROM List_UpgradeEngine WHERE Ordinal = ?", car);
            SqliteDatabase.Result rows = fm2.Query("SELECT * FROM List_UpgradeEngine WHERE Ordinal = ? ORDER BY Level, Id", from);
            int kept = 0;
            foreach (object[] row in rows.Rows)
            {
                long rowEngine = Convert.ToInt64(Fm2CarUpgrades.Value(rows, row, "EngineID"));
                long slot = Convert.ToInt64(Fm2CarUpgrades.Value(rows, row, "Id")) - from * 1000;
                Dictionary<string, object> values = new Dictionary<string, object>();
                values["Ordinal"] = car;
                values["WeightDistDiff"] = 0.0;
                if (rowEngine == engine)
                {
                    values["EngineID"] = car;
                }
                else
                {
                    object name = fm2.Scalar("SELECT MediaName FROM Data_Engine WHERE EngineID = ?", rowEngine);
                    object present = target.Scalar("SELECT MediaName FROM Data_Engine WHERE EngineID = ?", rowEngine);
                    if (name == null || present == null || !string.Equals((string) name, (string) present, StringComparison.OrdinalIgnoreCase))
                    {
                        log.Add("  engine swap " + rowEngine + " (" + name + "): not in FM4, dropped");
                        continue;
                    }
                }
                Fm2CarUpgrades.Insert(target, "List_UpgradeEngine", rows, row, "Id", id + ((slot >= 0 && slot < 1000) ? slot : kept), values);
                kept++;
            }
            log.Add("List_UpgradeEngine: " + kept + " rows");
            if (Fm2CarUpgrades.Has(target, "Combo_Engines"))
            {
                target.Execute("UPDATE Combo_Engines SET EngineID = ? WHERE Ordinal = ?", car, car);
            }

            // Camshaft + torque curves (FM2 CamValves). Curve Id = car * 1000 + the part's slot.
            if (Fm2CarUpgrades.Has(fm2, "List_UpgradeEngineCamValves"))
            {
                target.Execute("DELETE FROM List_UpgradeEngineCamshaft WHERE EngineID = ?", car);
                target.Execute("DELETE FROM List_TorqueCurve WHERE TorqueCurveID BETWEEN ? AND ?", id, id + 999);
                SqliteDatabase.Result cams = fm2.Query("SELECT * FROM List_UpgradeEngineCamValves WHERE EngineID = ? ORDER BY Level, Id", engine);
                foreach (object[] cam in cams.Rows)
                {
                    long slot = Convert.ToInt64(Fm2CarUpgrades.Value(cams, cam, "Id")) - engine * 1000;
                    long partId = id + slot;
                    SqliteDatabase.Result full = fm2.Query("SELECT * FROM List_TorqueCurve WHERE TorqueCurveID = ?", Fm2CarUpgrades.Value(cams, cam, "TorqueCurveFullThrottleID"));
                    SqliteDatabase.Result zero = fm2.Query("SELECT TorqueScale FROM List_TorqueCurve WHERE TorqueCurveID = ?", Fm2CarUpgrades.Value(cams, cam, "TorqueCurveZeroThrottleID"));
                    if (full.Rows.Count == 0)
                    {
                        log.Add("  camshaft " + slot + ": torque curve " + Fm2CarUpgrades.Value(cams, cam, "TorqueCurveFullThrottleID") + " missing, skipped");
                        continue;
                    }
                    Dictionary<string, object> curve = new Dictionary<string, object>();
                    curve["ZeroThrottleTorqueScale"] = zero.Rows.Count > 0 ? zero.Rows[0][0] : (object) 0.0;
                    Fm2CarUpgrades.Insert(target, "List_TorqueCurve", full, full.Rows[0], "TorqueCurveID", partId, curve);
                    Dictionary<string, object> part = new Dictionary<string, object>();
                    part["EngineID"] = car;
                    part["TorqueCurveFullThrottleID"] = partId;
                    Fm2CarUpgrades.Insert(target, "List_UpgradeEngineCamshaft", cams, cam, "Id", partId, part);
                }
                log.Add("List_UpgradeEngineCamshaft + List_TorqueCurve: " + cams.Rows.Count + " rows each");
            }

            // Block -> displacement. FM2 gives the torque scale only; the displacement follows FM4's own
            // rows (2JZ: scale 1.055 / 1.122 / 1.195 for 3077 / 3178 / 3400 cc of 2997 = 1 + 0.5 * (scale - 1)).
            if (Fm2CarUpgrades.Has(fm2, "List_UpgradeEngineBlock"))
            {
                target.Execute("DELETE FROM List_UpgradeEngineDisplacement WHERE EngineID = ?", car);
                object cc = Fm2CarUpgrades.Value(data, data.Rows[0], "Disp_cc");
                SqliteDatabase.Result blocks = fm2.Query("SELECT * FROM List_UpgradeEngineBlock WHERE EngineID = ? ORDER BY Level, Id", engine);
                foreach (object[] block in blocks.Rows)
                {
                    long slot = Convert.ToInt64(Fm2CarUpgrades.Value(blocks, block, "Id")) - engine * 1000;
                    double scale = Convert.ToDouble(Fm2CarUpgrades.Value(blocks, block, "TorqueScale"), CultureInfo.InvariantCulture);
                    Dictionary<string, object> part = new Dictionary<string, object>();
                    part["EngineID"] = car;
                    if (cc != null)
                    {
                        part["Disp"] = Math.Round(Convert.ToDouble(cc, CultureInfo.InvariantCulture) * (1 + 0.5 * (scale - 1)));
                    }
                    Fm2CarUpgrades.Insert(target, "List_UpgradeEngineDisplacement", blocks, block, "Id", id + slot, part);
                }
                log.Add("List_UpgradeEngineDisplacement: " + blocks.Rows.Count + " rows (displacement estimated from the block's torque scale)");
            }

            string[][] parts = new string[][]
            {
                new string[] { "List_UpgradeEngineIntake", "List_UpgradeEngineIntake" },
                new string[] { "List_UpgradeEngineIgnition", "List_UpgradeEngineIgnition" },
                new string[] { "List_UpgradeEngineExhaust", "List_UpgradeEngineExhaust" },
                new string[] { "List_UpgradeEngineFuelSystem", "List_UpgradeEngineFuelSystem" },
                new string[] { "List_UpgradeEngineIntercooler", "List_UpgradeEngineIntercooler" },
                new string[] { "List_UpgradeEngineTurbo", "List_UpgradeEngineTurboSingle" },
                new string[] { "List_UpgradeEngineCSC", "List_UpgradeEngineCSC" },
                new string[] { "List_UpgradeEngineDSC", "List_UpgradeEngineDSC" },
            };
            foreach (string[] map in parts)
            {
                Fm2CarUpgrades.CopyRows(target, fm2, map[0], "EngineID", engine, map[1], "EngineID", car, id, null, log);
            }
            // The flywheel is per car in FM2.
            Fm2CarUpgrades.CopyRows(target, fm2, "List_UpgradeFlywheel", "Ordinal", from, "List_UpgradeEngineFlywheel", "EngineID", car, id, null, log);
            // FM2 has no valves / pistons / oil cooling: FM4 expects them (502 of the FM4 engines have them; an engine
            // without them crashed the game on selecting the car). They are generated: FM4 medians per level.
            Fm2CarUpgrades.GenerateEngineParts(target, car, new string[] { "List_UpgradeEngineValves", "List_UpgradeEnginePistonsCompression", "List_UpgradeEngineOilCooling" }, log);
        }

        // FM4 rows carry WeightDistDiff = MassDiff * (+0.3 front engine, -0.3 mid / rear) / (CurbWeight * 100)
        // (List_UpgradeEngine, hood; checked on the Supra's hood 1510 kg: -2.8086 -> -0.000558). FM2 has none.
        private static void WeightDistribution(SqliteDatabase target, long car, List<string> log)
        {
            SqliteDatabase.Result info = target.Query("SELECT CurbWeight, EnginePlacementID FROM Data_Car WHERE Id = ?", car);
            if (info.Rows.Count == 0 || info.Rows[0][0] == null)
            {
                return;
            }
            double weight = Convert.ToDouble(info.Rows[0][0], CultureInfo.InvariantCulture) * 100;
            double engineSign = (Convert.ToInt64(info.Rows[0][1]) == 1) ? 0.3 : -0.3;
            string[][] tables = new string[][]
            {
                new string[] { "List_UpgradeEngine", "Ordinal", car.ToString(CultureInfo.InvariantCulture), "E" },
                new string[] { "List_UpgradeCarBodyHood", "CarBodyID", (car * 1000).ToString(CultureInfo.InvariantCulture), "F" },
            };
            int rows = 0;
            foreach (string[] t in tables)
            {
                if (!Fm2CarUpgrades.Has(target, t[0]))
                {
                    continue;
                }
                double sign = (t[3] == "F") ? 0.3 : engineSign;
                rows += target.Execute("UPDATE " + t[0] + " SET WeightDistDiff = MassDiff * " + sign.ToString(CultureInfo.InvariantCulture) + " / " + weight.ToString(CultureInfo.InvariantCulture) + " WHERE " + t[1] + " = " + t[2] + " AND (WeightDistDiff IS NULL OR WeightDistDiff = 0)");
            }
            // The engine part tables of the car's engine.
            foreach (string table in new string[] { "List_UpgradeEngineExhaust", "List_UpgradeEngineFuelSystem", "List_UpgradeEngineIntercooler", "List_UpgradeEngineTurboSingle", "List_UpgradeEngineCSC", "List_UpgradeEngineDSC" })
            {
                if (Fm2CarUpgrades.Has(target, table))
                {
                    rows += target.Execute("UPDATE " + table + " SET WeightDistDiff = MassDiff * " + engineSign.ToString(CultureInfo.InvariantCulture) + " / " + weight.ToString(CultureInfo.InvariantCulture) + " WHERE EngineID = " + car.ToString(CultureInfo.InvariantCulture) + " AND (WeightDistDiff IS NULL OR WeightDistDiff = 0)");
                }
            }
            log.Add("WeightDistDiff: " + rows + " rows from MassDiff");
        }
    }
}
