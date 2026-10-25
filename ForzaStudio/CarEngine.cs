namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    // What the user asks of a car's own engine (Duplicate Car, ROADMAP 2.3.2).
    public sealed class EngineSpec
    {
        public string Name;                                 // Data_Engine.EngineName; null = the source engine's
        public Dictionary<string, object> Data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);   // Data_Engine column -> new value
        public double? DisplacementCc;                      // the stock displacement; the upgrade levels scale with it
        public double TorqueScale = 1.0;                    // times every torque curve of the engine
        public double? RedlineRpm;                          // the stock redline; the upgrade levels and the curve's rpm scale with it
        public double? IdleRpm;                             // StallRPM of every camshaft level of the copy
        public double[] Shape;                              // the stock torque curve's values (normalised, as many as the source's curve); the upgrade curves follow the change
    }

    // A copy of a car's stock engine under an EngineID of its own (the Duplicate Car wizard shares the source's
    // engine unless it is asked for this): the Data_Engine row, every List_UpgradeEngine* row of the engine and
    // the torque curves its camshafts point at. FM4 numbers the rows of an engine EngineID * 1000 + n (camshaft
    // Id = its torque curve's id), kept here; the copy's ids are above every id of FM4 and its DLC (the first
    // free EngineID, which is above 933). Run inside the caller's transaction, before the integrity check.
    public static class CarEngine
    {
        // The stock engine's id of a car, or -1.
        public static long StockEngine(SqliteDatabase db, long carId)
        {
            object id = db.Scalar("SELECT EngineID FROM List_UpgradeEngine WHERE Ordinal = ? AND IsStock = 1 ORDER BY Level LIMIT 1", carId);
            return (id == null || id == DBNull.Value) ? -1 : Convert.ToInt64(id, CultureInfo.InvariantCulture);
        }

        public static long Clone(SqliteDatabase db, long carId, EngineSpec spec, List<string> plan)
        {
            ForzaGame game = ForzaGame.Motorsport4;
            long old = StockEngine(db, carId);
            if (old < 0)
            {
                throw new InvalidOperationException("The car has no stock engine in List_UpgradeEngine.");
            }
            Dictionary<string, List<string>> schema = DbIds.Schema(db);
            List<string> partTables = new List<string>();
            foreach (KeyValuePair<string, List<string>> table in schema)
            {
                if (table.Key != "Data_Engine" && table.Key != "List_UpgradeEngine" && table.Key != "Combo_Engines" && table.Key != "List_TorqueCurve" && table.Value.Exists(delegate(string c) { return string.Equals(c, "EngineID", StringComparison.OrdinalIgnoreCase); }))
                {
                    partTables.Add(table.Key);
                }
            }
            partTables.Sort(StringComparer.OrdinalIgnoreCase);

            // The new EngineID: free, and EngineID * 1000 + 0..999 above FM4's largest key and free in every part table.
            long id = DbIds.Next(db, "Data_Engine", "EngineID", game);
            List<string> keyed = new List<string>(partTables);
            keyed.Add("List_TorqueCurve");
            while (true)
            {
                bool free = true;
                foreach (string table in keyed)
                {
                    string key = DbIds.KeyColumn(db, table, schema[table]);
                    if (key == null)
                    {
                        continue;
                    }
                    long limit = GameIdLimits.Max(game, table, key);
                    string k = SqliteDatabase.Quote(key);
                    free = free && id * 1000 > limit
                        && Convert.ToInt64(db.Scalar("SELECT count(*) FROM " + SqliteDatabase.Quote(table) + " WHERE " + k + " BETWEEN ? AND ?", id * 1000, (id * 1000) + 999), CultureInfo.InvariantCulture) == 0;
                }
                if (free)
                {
                    break;
                }
                id++;
            }

            // Data_Engine.
            SqliteDatabase.Result engine = db.Query("SELECT * FROM Data_Engine WHERE EngineID = ?", old);
            if (engine.Rows.Count == 0)
            {
                throw new InvalidOperationException("Data_Engine has no engine " + old + ".");
            }
            object[] row = (object[]) engine.Rows[0].Clone();
            double torque = spec.TorqueScale;
            for (int i = 0; i < engine.Columns.Length; i++)
            {
                string column = engine.Columns[i];
                object value;
                if (string.Equals(column, "EngineID", StringComparison.OrdinalIgnoreCase))
                {
                    row[i] = id;
                }
                else if (string.Equals(column, "EngineName", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(spec.Name))
                {
                    row[i] = spec.Name;
                }
                else if (spec.Data.TryGetValue(column, out value))
                {
                    row[i] = value;
                }
                else if ((string.Equals(column, "EngineGraphingMaxTorque", StringComparison.OrdinalIgnoreCase) || string.Equals(column, "EngineGraphingMaxPower", StringComparison.OrdinalIgnoreCase)) && row[i] is double)
                {
                    row[i] = (double) row[i] * torque;
                }
            }
            Insert(db, "Data_Engine", engine.Columns, row);
            plan.Add(string.Format(CultureInfo.InvariantCulture, "engine: Data_Engine {0} copied as {1}{2}", old, id, string.IsNullOrEmpty(spec.Name) ? "" : " \"" + spec.Name + "\""));

            // The car's Data_Car numbers that mirror the engine.
            Dictionary<string, string> carColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            carColumns["CylinderID"] = "CylinderID";
            carColumns["ConfigID"] = "EngineConfigID";
            carColumns["AspirationID_Stock"] = "AspirationTypeId";
            foreach (KeyValuePair<string, string> pair in carColumns)
            {
                object value;
                if (spec.Data.TryGetValue(pair.Key, out value) && schema["Data_Car"].Exists(delegate(string c) { return string.Equals(c, pair.Value, StringComparison.OrdinalIgnoreCase); }))
                {
                    db.Execute("UPDATE Data_Car SET " + SqliteDatabase.Quote(pair.Value) + " = ? WHERE Id = ?", value, carId);
                }
            }

            // The upgrade parts and the torque curves.
            double oldDisplacement = 0;
            object stockDisplacement = schema.ContainsKey("List_UpgradeEngineDisplacement") ? db.Scalar("SELECT Disp FROM List_UpgradeEngineDisplacement WHERE EngineID = ? AND IsStock = 1 ORDER BY Level LIMIT 1", old) : null;
            if (stockDisplacement != null && stockDisplacement != DBNull.Value)
            {
                oldDisplacement = Convert.ToDouble(stockDisplacement, CultureInfo.InvariantCulture);
            }
            double displacementRatio = (spec.DisplacementCc.HasValue && oldDisplacement > 0) ? spec.DisplacementCc.Value / oldDisplacement : 1.0;
            double oldRedline = 0;
            object stockRedline = schema.ContainsKey("List_UpgradeEngineCamshaft") ? db.Scalar("SELECT RedlineRPM FROM List_UpgradeEngineCamshaft WHERE EngineID = ? AND IsStock = 1 ORDER BY Level LIMIT 1", old) : null;
            if (stockRedline != null && stockRedline != DBNull.Value)
            {
                oldRedline = Convert.ToDouble(stockRedline, CultureInfo.InvariantCulture);
            }
            double redlineRatio = (spec.RedlineRpm.HasValue && oldRedline > 0) ? spec.RedlineRpm.Value / oldRedline : 1.0;
            Dictionary<long, long> curves = new Dictionary<long, long>();
            double[] stockShape = null;
            if (spec.Shape != null && schema.ContainsKey("List_UpgradeEngineCamshaft"))
            {
                object stockCurve = db.Scalar("SELECT TorqueCurveFullThrottleID FROM List_UpgradeEngineCamshaft WHERE EngineID = ? AND IsStock = 1 ORDER BY Level LIMIT 1", old);
                if (stockCurve != null && stockCurve != DBNull.Value)
                {
                    stockShape = CurveValues(db, Convert.ToInt64(stockCurve, CultureInfo.InvariantCulture));
                    if (stockShape == null || stockShape.Length != spec.Shape.Length)
                    {
                        throw new InvalidOperationException("The new torque curve has " + spec.Shape.Length + " points, the source's " + ((stockShape == null) ? 0 : stockShape.Length) + ".");
                    }
                }
            }
            List<string> copied = new List<string>();
            foreach (string table in partTables)
            {
                List<string> columns = schema[table];
                string key = DbIds.KeyColumn(db, table, columns);
                if (key == null)
                {
                    continue;
                }
                SqliteDatabase.Result rows = db.Query("SELECT * FROM " + SqliteDatabase.Quote(table) + " WHERE EngineID = ?", old);
                int count = 0;
                foreach (object[] part in rows.Rows)
                {
                    object[] copy = (object[]) part.Clone();
                    long oldKey = 0;
                    int keyIndex = Array.FindIndex(rows.Columns, delegate(string c) { return string.Equals(c, key, StringComparison.OrdinalIgnoreCase); });
                    oldKey = Convert.ToInt64(copy[keyIndex], CultureInfo.InvariantCulture);
                    long newKey = (oldKey >= old * 1000 && oldKey < (old * 1000) + 1000) ? (id * 1000) + (oldKey - (old * 1000)) : DbIds.Next(db, table, key, game);
                    for (int i = 0; i < rows.Columns.Length; i++)
                    {
                        string column = rows.Columns[i];
                        if (i == keyIndex)
                        {
                            copy[i] = newKey;
                        }
                        else if (string.Equals(column, "EngineID", StringComparison.OrdinalIgnoreCase))
                        {
                            copy[i] = id;
                        }
                        else if (string.Equals(table, "List_UpgradeEngineDisplacement", StringComparison.OrdinalIgnoreCase) && string.Equals(column, "Disp", StringComparison.OrdinalIgnoreCase) && copy[i] != null && copy[i] != DBNull.Value)
                        {
                            copy[i] = (long) Math.Round(Convert.ToDouble(copy[i], CultureInfo.InvariantCulture) * displacementRatio);
                        }
                        else if (string.Equals(table, "List_UpgradeEngineCamshaft", StringComparison.OrdinalIgnoreCase) && redlineRatio != 1.0
                            && (string.Equals(column, "RedlineRPM", StringComparison.OrdinalIgnoreCase) || string.Equals(column, "TorqueCurveMaxRPM", StringComparison.OrdinalIgnoreCase) || string.Equals(column, "StartRPM", StringComparison.OrdinalIgnoreCase))
                            && copy[i] != null && copy[i] != DBNull.Value)
                        {
                            copy[i] = Convert.ToDouble(copy[i], CultureInfo.InvariantCulture) * redlineRatio;
                        }
                        else if (string.Equals(table, "List_UpgradeEngineCamshaft", StringComparison.OrdinalIgnoreCase) && spec.IdleRpm.HasValue && string.Equals(column, "StallRPM", StringComparison.OrdinalIgnoreCase))
                        {
                            copy[i] = spec.IdleRpm.Value;
                        }
                        else if (column.StartsWith("TorqueCurve", StringComparison.OrdinalIgnoreCase) && column.EndsWith("ID", StringComparison.OrdinalIgnoreCase) && copy[i] != null && copy[i] != DBNull.Value)
                        {
                            long oldCurve = Convert.ToInt64(copy[i], CultureInfo.InvariantCulture);
                            long newCurve;
                            if (!curves.TryGetValue(oldCurve, out newCurve))
                            {
                                newCurve = (oldCurve == oldKey) ? newKey : (id * 1000) + 900 + curves.Count;
                                if (Convert.ToInt64(db.Scalar("SELECT count(*) FROM List_TorqueCurve WHERE TorqueCurveID = ?", newCurve), CultureInfo.InvariantCulture) > 0)
                                {
                                    newCurve = DbIds.Next(db, "List_TorqueCurve", "TorqueCurveID", game);
                                }
                                int stockIndex = Array.FindIndex(rows.Columns, delegate(string c) { return string.Equals(c, "IsStock", StringComparison.OrdinalIgnoreCase); });
                                bool stockLevel = stockIndex >= 0 && copy[stockIndex] != null && copy[stockIndex] != DBNull.Value && Convert.ToInt64(copy[stockIndex], CultureInfo.InvariantCulture) == 1;
                                CopyCurve(db, oldCurve, newCurve, torque, spec.Shape, stockShape, stockLevel);
                                curves[oldCurve] = newCurve;
                            }
                            copy[i] = newCurve;
                        }
                    }
                    Insert(db, table, rows.Columns, copy);
                    count++;
                }
                if (count > 0)
                {
                    copied.Add(table.Replace("List_UpgradeEngine", "") + " " + count);
                }
            }
            plan.Add(string.Format(CultureInfo.InvariantCulture, "engine: {0} upgrade rows ({1}), {2} torque curves, torque x{3:0.###}{4}{5}",
                SumOf(copied), string.Join(", ", copied.ToArray()), curves.Count, torque,
                (displacementRatio != 1.0) ? string.Format(CultureInfo.InvariantCulture, ", displacement {0} -> {1} cc", oldDisplacement, spec.DisplacementCc.Value) : "",
                (redlineRatio != 1.0) ? string.Format(CultureInfo.InvariantCulture, ", redline {0} -> {1} rpm", oldRedline, spec.RedlineRpm.Value) : ""));

            // The car's stock engine row points at the copy; the swap engines stay as they are.
            int moved = db.Execute("UPDATE List_UpgradeEngine SET EngineID = ? WHERE Ordinal = ? AND EngineID = ?", id, carId, old);
            plan.Add("engine: " + moved + " List_UpgradeEngine row of the car now point at engine " + id + " (the source car keeps " + old + ")");
            if (schema.ContainsKey("Combo_Engines"))
            {
                // The old engine table: the car's row (not the rows of the other cars that use the engine).
                db.Execute("UPDATE Combo_Engines SET EngineID = ? WHERE Ordinal = ? AND EngineID = ?", id, carId, old);
            }
            UpdateSimulation(db, schema, carId, id, plan);
            return id;
        }


        // Data_Car's Sim* numbers from the copy's stock torque curve (checked on the 502 stock cars of FM4: the redline
        // is exact, the peak torque within 5% on 353 of them, 332 within 1%, the rest supercharged or boosted):
        // SimPeakTorque and SimPeakPower are N.m and W divided by 100, the angular velocities rad/s.
        private static void UpdateSimulation(SqliteDatabase db, Dictionary<string, List<string>> schema, long carId, long engine, List<string> plan)
        {
            string[] columns = { "SimPeakTorque", "SimPeakTorqueAngVel", "SimPeakPower", "SimPeakAngVel", "SimRedlineAngVel" };
            foreach (string column in columns)
            {
                if (!schema["Data_Car"].Exists(delegate(string c) { return string.Equals(c, column, StringComparison.OrdinalIgnoreCase); }))
                {
                    return;
                }
            }
            SqliteDatabase.Result cam = db.Query("SELECT RedlineRPM, TorqueCurveMaxRPM, NumRPMEntriesArray, TorqueCurveFullThrottleID FROM List_UpgradeEngineCamshaft WHERE EngineID = ? AND IsStock = 1 ORDER BY Level LIMIT 1", engine);
            if (cam.Rows.Count == 0)
            {
                return;
            }
            double redline = Real(cam.Rows[0][0]);
            double maxRpm = Real(cam.Rows[0][1]);
            SqliteDatabase.Result curve = db.Query("SELECT * FROM List_TorqueCurve WHERE TorqueCurveID = ?", Convert.ToInt64(cam.Rows[0][3], CultureInfo.InvariantCulture));
            if (curve.Rows.Count == 0)
            {
                return;
            }
            object[] values = curve.Rows[0];
            double scale = Real(values[1]);
            int count = Math.Min((int) Real(values[2]), values.Length - 3);
            if (count < 2)
            {
                return;
            }
            double factor, boost;
            StockMultipliers(db, engine, out factor, out boost);
            if (Real(db.Scalar("SELECT AspirationID_Stock FROM Data_Engine WHERE EngineID = ?", engine)) <= 1)
            {
                boost = 1;
            }
            double bestTorque = 0, torqueSpeed = 0, bestPower = 0, powerSpeed = 0;
            for (int i = 0; i < count; i++)
            {
                double rpm = i * maxRpm / (count - 1);
                if (rpm > redline)
                {
                    break;
                }
                double torque = Real(values[3 + i]) * scale * factor * boost;
                double speed = rpm * 2 * Math.PI / 60;
                if (torque > bestTorque)
                {
                    bestTorque = torque;
                    torqueSpeed = speed;
                }
                if (torque * speed > bestPower)
                {
                    bestPower = torque * speed;
                    powerSpeed = speed;
                }
            }
            db.Execute("UPDATE Data_Car SET SimPeakTorque = ?, SimPeakTorqueAngVel = ?, SimPeakPower = ?, SimPeakAngVel = ?, SimRedlineAngVel = ? WHERE Id = ?",
                bestTorque / 100, torqueSpeed, bestPower / 100, powerSpeed, redline * 2 * Math.PI / 60, carId);
            plan.Add(string.Format(CultureInfo.InvariantCulture, "engine: Data_Car Sim numbers: peak torque {0:0} N.m at {1:0} rpm, peak power {2:0} kW at {3:0} rpm, redline {4:0} rpm",
                bestTorque, torqueSpeed * 60 / (2 * Math.PI), bestPower / 1000, powerSpeed * 60 / (2 * Math.PI), redline));
        }

        // What the game multiplies the stock torque curve by to show the car's torque: factor = the TorqueScale of the
        // engine's stock parts, boost = the stock turbo's MaxScale (1 when the engine has none). The boost only counts
        // for a turbocharged / supercharged engine (AspirationID_Stock above 1).
        public static void StockMultipliers(SqliteDatabase db, long engine, out double factor, out double boost)
        {
            factor = 1;
            foreach (string part in new string[] { "Intake", "FuelSystem", "Ignition", "Exhaust", "Valves", "PistonsCompression", "Displacement", "Manifold", "OilCooling", "RestrictorPlate" })
            {
                string table = "List_UpgradeEngine" + part;
                if (DbIds.HasTable(db, table))
                {
                    double s = Real(db.Scalar("SELECT TorqueScale FROM " + table + " WHERE EngineID = ? AND IsStock = 1 LIMIT 1", engine));
                    factor *= (s > 0) ? s : 1;
                }
            }
            boost = 1;
            foreach (string turbo in new string[] { "TurboSingle", "TurboTwin", "TurboQuad" })
            {
                string table = "List_UpgradeEngine" + turbo;
                if (DbIds.HasTable(db, table))
                {
                    double m = Real(db.Scalar("SELECT MaxScale FROM " + table + " WHERE EngineID = ? AND IsStock = 1 LIMIT 1", engine));
                    if (m > 0)
                    {
                        boost = m;
                    }
                }
            }
        }

        private static double Real(object value)
        {
            return (value == null || value == DBNull.Value) ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static int SumOf(List<string> parts)
        {
            int total = 0;
            foreach (string part in parts)
            {
                total += int.Parse(part.Substring(part.LastIndexOf(' ') + 1), CultureInfo.InvariantCulture);
            }
            return total;
        }

        private static double[] CurveValues(SqliteDatabase db, long curveId)
        {
            SqliteDatabase.Result curve = db.Query("SELECT * FROM List_TorqueCurve WHERE TorqueCurveID = ?", curveId);
            if (curve.Rows.Count == 0)
            {
                return null;
            }
            object[] row = curve.Rows[0];
            int count = Math.Min((int) Real(row[2]), row.Length - 3);
            double[] values = new double[Math.Max(0, count)];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Real(row[3 + i]);
            }
            return values;
        }

        // The stock level's curve is the drawn shape; the upgrade levels' curves are changed by the same ratio point by point.
        private static void CopyCurve(SqliteDatabase db, long from, long to, double scale, double[] shape, double[] stockShape, bool stockLevel)
        {
            SqliteDatabase.Result curve = db.Query("SELECT * FROM List_TorqueCurve WHERE TorqueCurveID = ?", from);
            if (curve.Rows.Count == 0)
            {
                throw new InvalidOperationException("List_TorqueCurve has no curve " + from + ".");
            }
            object[] row = (object[]) curve.Rows[0].Clone();
            for (int i = 0; i < curve.Columns.Length; i++)
            {
                if (string.Equals(curve.Columns[i], "TorqueCurveID", StringComparison.OrdinalIgnoreCase))
                {
                    row[i] = to;
                }
                else if (string.Equals(curve.Columns[i], "TorqueScale", StringComparison.OrdinalIgnoreCase) && row[i] is double)
                {
                    row[i] = (double) row[i] * scale;
                }
                else if (shape != null && stockShape != null && i >= 3 && i - 3 < shape.Length && row[i] is double)
                {
                    double was = (double) row[i];
                    row[i] = stockLevel ? shape[i - 3] : ((stockShape[i - 3] > 0.02) ? was * shape[i - 3] / stockShape[i - 3] : was);
                }
            }
            Insert(db, "List_TorqueCurve", curve.Columns, row);
        }

        private static void Insert(SqliteDatabase db, string table, string[] columns, object[] row)
        {
            List<string> quoted = new List<string>();
            List<string> marks = new List<string>();
            foreach (string c in columns)
            {
                quoted.Add(SqliteDatabase.Quote(c));
                marks.Add("?");
            }
            db.Execute("INSERT INTO " + SqliteDatabase.Quote(table) + " (" + string.Join(",", quoted.ToArray()) + ") VALUES (" + string.Join(",", marks.ToArray()) + ")", row);
        }
    }
}
