namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Xml;

    // FM2 car data onto an FM4 database row (FM2 -> FM4 port, 2026-10-20). The stage database of an FM2
    // car is a copy of the FM4 one whose donor row was renamed, so every figure below is still the donor's;
    // this replaces what FM2 knows and FM4 needs with the FM2 car's own values (rule: source values kept).
    // Compared on the Supra RZ 98 (fm4dev\FM2_FM4\supra_cmp): FM2 stores the tire sizes, gear count, weight
    // distribution, ride heights and the body measures under other column names, the engine data in
    // Data_Engine; FM2 has no Data_CarBody (its measures sit in Data_Car).
    // NOT done here (open, see ROADMAP): engines of cars the target lacks (Data_Engine, torque curves),
    // the FM2 upgrade tables (List_UpgradeFrontBumper..., one table per part) -> List_Upgrade* of FM4,
    // physics columns (aero, steering, Sim*, ratings: the donor's stay), CarPartPositions, ClassID/PI.
    public static class Fm2CarData
    {
        // FM4 Data_Car column <- FM2 Data_Car column.
        private static readonly string[][] CarColumns = new string[][]
        {
            new string[] { "FrontTireWidthMM", "StockTire-FrontWidth-mm" },
            new string[] { "FrontTireAspect", "StockTire-FrontAspect-pct" },
            new string[] { "FrontWheelDiameterIN", "StockTire-FrontWheel-in" },
            new string[] { "RearTireWidthMM", "StockTire-RearWidth-mm" },
            new string[] { "RearTireAspect", "StockTire-RearAspect-pct" },
            new string[] { "RearWheelDiameterIN", "StockTire-RearWheel-in" },
            new string[] { "NumGears", "StockGears" },
            new string[] { "WeightDistribution", "FrontWeight-pct" },
            new string[] { "FrontStockRideHeight", "ModelFrontStockRideHeight" },
            new string[] { "RearStockRideHeight", "ModelRearStockRideHeight" },
            new string[] { "CurbWeight", "CurbWeight" },
            new string[] { "BaseCost", "BaseCost" },
            new string[] { "Year", "Year" },
            new string[] { "MakeID", "MakeID" },
            new string[] { "FamilyModelID", "FamilyModelID" },
            new string[] { "EnginePlacementID", "EnginePlacementID" },
            new string[] { "TopSpeed-mph", "TopSpeed-mph" },
            new string[] { "Time:0-60-sec", "Time:0-60-sec" },
            new string[] { "Time:0-100-sec", "Time:0-100-sec" },
            new string[] { "QuarterMileTime-sec", "QuarterMileTime-sec" },
            new string[] { "QuarterMileSpeed-mph", "QuarterMileSpeed-mph" },
        };

        // FM4 Data_Car column <- FM2 Data_Engine column (the car's stock engine).
        private static readonly string[][] EngineColumns = new string[][]
        {
            new string[] { "Displacement", "Disp_cc" },
            new string[] { "EngineConfigID", "ConfigID" },
            new string[] { "CylinderID", "CylinderID" },
            new string[] { "AspirationTypeId", "AspirationID_Stock" },
        };

        // Applies the FM2 car to the target's row of the same MediaName. maxData = the FM2 physics\maxdata.xml
        // (may be null). Returns a log, one line per change.
        public static List<string> Apply(SqliteDatabase target, SqliteDatabase fm2, string mediaName, string maxData)
        {
            List<string> log = new List<string>();
            SqliteDatabase.Result src = fm2.Query("SELECT * FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", mediaName);
            SqliteDatabase.Result dst = target.Query("SELECT * FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", mediaName);
            if (src.Rows.Count == 0 || dst.Rows.Count == 0)
            {
                log.Add("Fm2CarData: " + mediaName + " not found in " + (src.Rows.Count == 0 ? "the FM2" : "the target") + " database");
                return log;
            }
            object[] from = src.Rows[0];
            long carId = Convert.ToInt64(Value(dst, dst.Rows[0], "Id"));
            string where = "MediaName = '" + mediaName.Replace("'", "''") + "' COLLATE NOCASE";

            foreach (string[] map in CarColumns)
            {
                object value = Value(src, from, map[1]);
                bool stat = map[0].StartsWith("TopSpeed") || map[0].StartsWith("Time:") || map[0].StartsWith("QuarterMile");
                if (value != null && Has(dst, map[0]) && !(stat && Convert.ToDouble(value, CultureInfo.InvariantCulture) == 0))   // FM2 stores 0 = not measured
                {
                    Set(target, "Data_Car", map[0], value, where, dst, log);
                }
            }

            object engineId = fm2.Scalar("SELECT EngineID FROM List_UpgradeEngine WHERE Ordinal = ? AND IsStock = 1 ORDER BY Level LIMIT 1", Value(src, from, "Id"));
            if (engineId != null)
            {
                SqliteDatabase.Result engine = fm2.Query("SELECT * FROM Data_Engine WHERE EngineID = ?", engineId);
                if (engine.Rows.Count > 0)
                {
                    foreach (string[] map in EngineColumns)
                    {
                        object value = Value(engine, engine.Rows[0], map[1]);
                        if (value != null && Has(dst, map[0]))
                        {
                            Set(target, "Data_Car", map[0], value, where, dst, log);
                        }
                    }
                }
            }

            object drive = fm2.Scalar("SELECT DrivetrainId FROM Powertrains WHERE Id = ?", Value(src, from, "PowertrainID"));
            if (drive != null && Has(dst, "DriveTypeID") && Has(dst, "PowertrainID"))
            {
                Set(target, "Data_Car", "DriveTypeID", drive, where, dst, log);
                Set(target, "Data_Car", "PowertrainID", Value(src, from, "PowertrainID"), where, dst, log);
            }


            // Columns FM2 has under the same name and meaning (the sim results and ratings of the car, steering,
            // tyre / aero / traction constants): the car's own FM2 values replace the donor's. Lookup ids (class, body
            // family, material, speed limiter...) are not here: their numbering is not checked between the games.
            string[] prefixes = { "Sim", "SpeedRating", "AccelerationRating", "BrakingRating", "HandlingRating", "Steer", "FixListing", "BodyAero", "TireAeroHack", "Assists", "CaliperAngle", "CaliperRGB", "GameTorqueScale", "GameDragScale", "WingMask", "BaseRarity" };
            int copied = 0;
            foreach (string column in src.Columns)
            {
                if (Array.Exists(prefixes, delegate(string p) { return column.StartsWith(p, StringComparison.OrdinalIgnoreCase); }) && Has(dst, column) && Value(src, from, column) != null)
                {
                    Set(target, "Data_Car", column, Value(src, from, column), where, dst, log);
                    copied++;
                }
            }
            // LaunchRating is FM4's own: FM4's median LaunchRating / AccelerationRating times this car's.
            object accel = Value(src, from, "AccelerationRating");
            if (accel != null && Has(dst, "LaunchRating"))
            {
                List<double> ratios = new List<double>();
                foreach (object[] r in target.Query("SELECT LaunchRating, AccelerationRating FROM Data_Car WHERE AccelerationRating > 0 AND LaunchRating IS NOT NULL").Rows)
                {
                    ratios.Add(Convert.ToDouble(r[0], CultureInfo.InvariantCulture) / Convert.ToDouble(r[1], CultureInfo.InvariantCulture));
                }
                if (ratios.Count > 0)
                {
                    ratios.Sort();
                    Set(target, "Data_Car", "LaunchRating", ratios[ratios.Count / 2] * Convert.ToDouble(accel, CultureInfo.InvariantCulture), where, dst, log);
                }
            }
            log.Add("Data_Car: " + copied + " sim / rating / steering / aero columns from FM2");

            // Lookup ids, checked on the 188 cars both games have (fm4dev\FM2_FM4\tools\lookupmap): make, material type,
            // car type and family model are the same ids (184-186 of 188); FM4's speed limiter is 0 on all 503 cars;
            // country and region follow the make; the body family and tyre brand are numbered differently, so they
            // come from what the shared cars with the same FM2 values got in FM4 (majority).
            foreach (string column in new string[] { "MakeID", "MaterialTypeID", "CarTypeID", "FamilyModelID" })
            {
                if (Value(src, from, column) != null && Has(dst, column))
                {
                    Set(target, "Data_Car", column, Value(src, from, column), where, dst, log);
                }
            }
            if (Has(dst, "SpeedLimiterID"))
            {
                Set(target, "Data_Car", "SpeedLimiterID", 0L, where, dst, log);
            }
            object make = Value(src, from, "MakeID");
            foreach (string column in new string[] { "CountryID", "RegionID" })
            {
                object common = (make == null || !Has(dst, column)) ? null : target.Scalar("SELECT " + column + " FROM Data_Car WHERE MakeID = ? AND Id <> ? GROUP BY " + column + " ORDER BY COUNT(*) DESC LIMIT 1", make, carId);
                if (common != null)
                {
                    Set(target, "Data_Car", column, common, where, dst, log);
                }
            }
            if (!SetFromSharedCars(target, fm2, src, from, dst, where, carId, "FamilyBodyID", new string[] { "FamilyModelID", "FamilyBodyID" }, log))
            {
                SetFromSharedCars(target, fm2, src, from, dst, where, carId, "FamilyBodyID", new string[] { "FamilyBodyID" }, log);
            }
            SetFromSharedCars(target, fm2, src, from, dst, where, carId, "TireBrandID", new string[] { "TireBrandID" }, log);
            ApplyBody(target, src, from, carId, maxData, log);

            WheelHeights(target, carId, log);

            // Track offsets: the same two columns in FM2.
            SqliteDatabase.Result off = fm2.Query("SELECT FrontOffset, RearOffset FROM CarTrackOffsets WHERE Id = ?", Value(src, from, "Id"));
            if (off.Rows.Count > 0 && HasTable(target, "CarTrackOffsets"))
            {
                target.Execute("UPDATE CarTrackOffsets SET FrontOffset = ?, RearOffset = ? WHERE Id = ?", off.Rows[0][0], off.Rows[0][1], carId);
                log.Add("CarTrackOffsets " + carId + ": " + Text(off.Rows[0][0]) + " / " + Text(off.Rows[0][1]));
            }
            return log;
        }

        // Data_CarBody (Id = car * 1000): wheelbase, track, ride height, bottom centre from FM2 Data_Car, the
        // bounding box from maxdata.xml (the body without wheels and mirrors: smaller than FM4's pristine box).
        private static void ApplyBody(SqliteDatabase target, SqliteDatabase.Result src, object[] from, long carId, string maxData, List<string> log)
        {
            if (!HasTable(target, "Data_CarBody"))
            {
                return;
            }
            long id = carId * 1000;
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["ModelWheelbase"] = Value(src, from, "ModelWheelbase");
            values["ModelFrontTrackOuter"] = Value(src, from, "ModelFrontTrackOuter");
            values["ModelRearTrackOuter"] = Value(src, from, "ModelRearTrackOuter");
            values["ModelFrontStockRideHeight"] = Value(src, from, "ModelFrontStockRideHeight");
            values["ModelRearStockRideHeight"] = Value(src, from, "ModelRearStockRideHeight");
            values["BottomCenterWheelbasePosx"] = Value(src, from, "BottomCenterWheelbasePosx");
            values["BottomCenterWheelbasePosy"] = Value(src, from, "BottomCenterWheelbasePosy");
            values["BottomCenterWheelbasePosZ"] = Value(src, from, "BottomCenterWheelbasePosz");
            if (maxData != null)
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(maxData);
                XmlElement box = doc.SelectSingleNode("//BoundingBox") as XmlElement;
                if (box != null)
                {
                    foreach (string axis in new string[] { "X", "Y", "Z" })
                    {
                        values["PristineBoundingBoxMin" + axis] = double.Parse(box.GetAttribute("Min" + axis), CultureInfo.InvariantCulture);
                        values["PristineBoundingBoxMax" + axis] = double.Parse(box.GetAttribute("Max" + axis), CultureInfo.InvariantCulture);
                    }
                }
            }
            if (Convert.ToInt64(target.Scalar("SELECT COUNT(*) FROM Data_CarBody WHERE Id = ?", id)) == 0)
            {
                target.Execute("INSERT INTO Data_CarBody (Id) VALUES (?)", id);
            }
            foreach (KeyValuePair<string, object> pair in values)
            {
                if (pair.Value != null)
                {
                    target.Execute("UPDATE Data_CarBody SET " + SqliteDatabase.Quote(pair.Key) + " = ? WHERE Id = ?", pair.Value, id);
                }
            }
            log.Add("Data_CarBody " + id + ": " + values.Count + " values from FM2" + (maxData == null ? " (no maxdata box)" : ""));
        }

        // The model's ground is `ride height` below its origin, so a wheel centre sits at tyre radius - stock
        // ride height (Supra RZ 98 in FM4: 0.32165 - 0.1822 = 0.1396, CarPartPositions parts 5-8). The donor's
        // positions are kept for x / z (wheelbase and track), only y follows this car's tyres and ride height:
        // without it a lowered car (ride 0.07) stands 0.11 too high on the donor's wheels ("monster truck").
        private static void WheelHeights(SqliteDatabase target, long carId, List<string> log)
        {
            if (!HasTable(target, "CarPartPositions"))
            {
                return;
            }
            SqliteDatabase.Result car = target.Query("SELECT FrontTireWidthMM, FrontTireAspect, FrontWheelDiameterIN, FrontStockRideHeight, RearTireWidthMM, RearTireAspect, RearWheelDiameterIN, RearStockRideHeight FROM Data_Car WHERE Id = ?", carId);
            if (car.Rows.Count == 0)
            {
                return;
            }
            object[] r = car.Rows[0];
            for (int axle = 0; axle < 2; axle++)
            {
                double width = Convert.ToDouble(r[axle * 4]), aspect = Convert.ToDouble(r[axle * 4 + 1]), rim = Convert.ToDouble(r[axle * 4 + 2]), ride = Convert.ToDouble(r[axle * 4 + 3]);
                double radius = (rim * 0.0254 / 2) + (width / 1000 * aspect / 100);
                double y = radius - ride;
                target.Execute("UPDATE CarPartPositions SET PartPosY = ? WHERE Ordinal = ? AND PartId IN (" + (axle == 0 ? "5, 6" : "7, 8") + ")", y, carId);
                log.Add("CarPartPositions " + carId + " " + (axle == 0 ? "front" : "rear") + " wheels y = tyre radius " + radius.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + " - ride height " + ride.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + " = " + y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        // column of the car = the value FM4 gave to the cars both games have that share the FM2 car's values of the
        // key columns (most frequent); the first call that finds cars wins, a later one with fewer keys is skipped.
        private static bool SetFromSharedCars(SqliteDatabase target, SqliteDatabase fm2, SqliteDatabase.Result src, object[] from, SqliteDatabase.Result dst, string where, long carId, string column, string[] keys, List<string> log)
        {
            if (!Has(dst, column))
            {
                return false;
            }
            string condition = "";
            foreach (string key in keys)
            {
                object v = Value(src, from, key);
                if (v == null)
                {
                    return false;
                }
                condition += " AND " + SqliteDatabase.Quote(key) + " = " + Convert.ToString(v, CultureInfo.InvariantCulture);
            }
            SqliteDatabase.Result names = fm2.Query("SELECT MediaName FROM Data_Car WHERE 1 = 1" + condition);
            Dictionary<string, int> votes = new Dictionary<string, int>();
            foreach (object[] n in names.Rows)
            {
                object v = target.Scalar("SELECT " + SqliteDatabase.Quote(column) + " FROM Data_Car WHERE MediaName = ? COLLATE NOCASE AND Id <> ?", n[0], carId);
                if (v != null)
                {
                    string k = Convert.ToString(v, CultureInfo.InvariantCulture);
                    int c;
                    votes.TryGetValue(k, out c);
                    votes[k] = c + 1;
                }
            }
            string best = null;
            int bestVotes = 0;
            foreach (KeyValuePair<string, int> pair in votes)
            {
                if (pair.Value > bestVotes)
                {
                    best = pair.Key;
                    bestVotes = pair.Value;
                }
            }
            if (best != null)
            {
                Set(target, "Data_Car", column, long.Parse(best, CultureInfo.InvariantCulture), where, dst, log);
                log.Add("  " + column + " from " + bestVotes + " of " + names.Rows.Count + " shared cars with the same FM2 " + string.Join("/", keys));
                return true;
            }
            return false;
        }

        private static bool HasTable(SqliteDatabase db, string table)
        {
            return Convert.ToInt64(db.Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ? COLLATE NOCASE", table)) > 0;
        }

        private static bool Has(SqliteDatabase.Result result, string column)
        {
            return Array.FindIndex(result.Columns, delegate(string c) { return string.Equals(c, column, StringComparison.OrdinalIgnoreCase); }) >= 0;
        }

        private static object Value(SqliteDatabase.Result result, object[] row, string column)
        {
            int index = Array.FindIndex(result.Columns, delegate(string c) { return string.Equals(c, column, StringComparison.OrdinalIgnoreCase); });
            return (index < 0) ? null : row[index];
        }

        private static string Text(object value)
        {
            return (value == null) ? "NULL" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void Set(SqliteDatabase db, string table, string column, object value, string where, SqliteDatabase.Result current, List<string> log)
        {
            object old = Value(current, current.Rows[0], column);
            if (Text(old) == Text(value))
            {
                return;
            }
            db.Execute("UPDATE " + table + " SET " + SqliteDatabase.Quote(column) + " = ? WHERE " + where, value);
            log.Add(table + "." + column + ": " + Text(old) + " -> " + Text(value));
        }
    }
}
