namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    // The cockpit camera of an FM4 car, as the game builds it (measured 2026-10-16):
    //  - CarPartPositions row PartId 9 is the steering wheel point. FM4, FH1 and FH2 have it for every
    //    car, FM3 has PartId 0-8 only: a car ported from FM3 without it gets its cockpit camera at
    //    0,0,0 while the exterior cameras are fine (BMW_X5_09 in game, ROADMAP 9 3d.2).
    //    On the 313 FM4 cars with a steering_wheel section in the _cockpit.carbin it is
    //    x = the section's origin x (exact), y = origin y + a third of the half height
    //    (median error 1 mm), z = -(origin z + the mean vertex offset z) + 2 cm, i.e. near the rim's
    //    own centre: 1.2 cm median error over the whole point, 3 cm for 90% of the cars, at most 10 cm.
    //    The database's Z points forward, the model's backward.
    //  - The eye sits at the steering wheel point plus CameraOverrides.CamCockpitOffsetX/Y/Z; a car
    //    whose offsets are all 0 uses Media\Camera\CameraSettings.ini Driver\DefaultOffsetFromSteeringWheel
    //    (0, 0.21, -0.51), FOV 62 (vertical) and near clip 0.1. The FM4 values (205 cars, typically
    //    0, 0.18..0.25, -0.44..-0.58) are close to that default, so they replace it, not add to it.
    //    That reading is inferred from the numbers, not yet checked in game.
    public sealed class CarCamera
    {
        public const long SteeringWheelPart = 9;
        public static readonly Vector3 DefaultCockpitOffset = new Vector3(0f, 0.21f, -0.51f);
        public const float CockpitFov = 62f;
        public const float CockpitNearClip = 0.1f;

        public long CarId;
        public string MediaName;
        public bool HasSteeringWheel;      // the CarPartPositions row exists
        public Vector3 SteeringWheel;      // database axes
        public bool HasOverrides;          // the CameraOverrides row exists
        public Vector3 CockpitOffset;

        public bool UsesDefaultOffset
        {
            get { return this.CockpitOffset == Vector3.Zero; }
        }

        public Vector3 EffectiveOffset
        {
            get { return this.UsesDefaultOffset ? DefaultCockpitOffset : this.CockpitOffset; }
        }

        // Database axes.
        public Vector3 Eye
        {
            get { return this.SteeringWheel + this.EffectiveOffset; }
        }

        // Database axes (Z forward) <-> model axes (Z backward).
        public static Vector3 ToModel(Vector3 point)
        {
            return new Vector3(point.X, point.Y, -point.Z);
        }

        public static Vector3 FromModel(Vector3 point)
        {
            return new Vector3(point.X, point.Y, -point.Z);
        }

        // Null when the database has no car with that media name.
        public static CarCamera Load(SqliteDatabase db, string mediaName)
        {
            object id = db.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", mediaName);
            if (id == null)
            {
                return null;
            }
            CarCamera camera = new CarCamera();
            camera.CarId = Convert.ToInt64(id);
            camera.MediaName = Convert.ToString(db.Scalar("SELECT MediaName FROM Data_Car WHERE Id = ?", camera.CarId), CultureInfo.InvariantCulture);
            SqliteDatabase.Result part = db.Query("SELECT PartPosX, PartPosY, PartPosZ FROM CarPartPositions WHERE Ordinal = ? AND PartId = ?", camera.CarId, SteeringWheelPart);
            if (part.Rows.Count > 0)
            {
                camera.HasSteeringWheel = true;
                camera.SteeringWheel = ToVector(part.Rows[0]);
            }
            SqliteDatabase.Result offsets = db.Query("SELECT CamCockpitOffsetX, CamCockpitOffsetY, CamCockpitOffsetZ FROM CameraOverrides WHERE CarId = ?", camera.CarId);
            if (offsets.Rows.Count > 0)
            {
                camera.HasOverrides = true;
                camera.CockpitOffset = ToVector(offsets.Rows[0]);
            }
            return camera;
        }

        private static Vector3 ToVector(object[] row)
        {
            return new Vector3(ToFloat(row[0]), ToFloat(row[1]), ToFloat(row[2]));
        }

        private static float ToFloat(object value)
        {
            return (value == null || value is DBNull) ? 0f : Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        // Writes the steering wheel point and the cockpit offsets, adding the rows a car lacks. A new
        // CameraOverrides row gets 0 in every other column (no override: the game's default cameras).
        // Returns what was written, one line per row.
        public List<string> Save(SqliteDatabase db)
        {
            List<string> done = new List<string>();
            double x = Round(this.SteeringWheel.X), y = Round(this.SteeringWheel.Y), z = Round(this.SteeringWheel.Z);
            if (Convert.ToInt64(db.Scalar("SELECT count(*) FROM CarPartPositions WHERE Ordinal = ? AND PartId = ?", this.CarId, SteeringWheelPart)) > 0)
            {
                db.Execute("UPDATE CarPartPositions SET PartPosX = ?, PartPosY = ?, PartPosZ = ? WHERE Ordinal = ? AND PartId = ?", x, y, z, this.CarId, SteeringWheelPart);
                done.Add(string.Format(CultureInfo.InvariantCulture, "CarPartPositions {0} part 9 (steering wheel) = {1}, {2}, {3}", this.CarId, x, y, z));
            }
            else if (HasColumn(db, "CarPartPositions", "ID"))
            {
                db.Execute("INSERT INTO CarPartPositions (ID, Ordinal, PartId, PartPosX, PartPosY, PartPosZ) VALUES (?, ?, ?, ?, ?, ?)", (this.CarId * 1000) + SteeringWheelPart, this.CarId, SteeringWheelPart, x, y, z);
                done.Add(string.Format(CultureInfo.InvariantCulture, "CarPartPositions {0} part 9 (steering wheel) added = {1}, {2}, {3}", this.CarId, x, y, z));
            }
            else
            {
                db.Execute("INSERT INTO CarPartPositions (Ordinal, PartId, PartPosX, PartPosY, PartPosZ) VALUES (?, ?, ?, ?, ?)", this.CarId, SteeringWheelPart, x, y, z);
                done.Add(string.Format(CultureInfo.InvariantCulture, "CarPartPositions {0} part 9 (steering wheel) added = {1}, {2}, {3}", this.CarId, x, y, z));
            }
            this.HasSteeringWheel = true;

            double ox = Round(this.CockpitOffset.X), oy = Round(this.CockpitOffset.Y), oz = Round(this.CockpitOffset.Z);
            if (Convert.ToInt64(db.Scalar("SELECT count(*) FROM CameraOverrides WHERE CarId = ?", this.CarId)) == 0)
            {
                List<string> columns = new List<string>();
                List<string> marks = new List<string>();
                List<object> values = new List<object>();
                foreach (object[] column in db.Query("PRAGMA table_info(CameraOverrides)").Rows)
                {
                    string name = (string) column[1];
                    columns.Add(SqliteDatabase.Quote(name));
                    marks.Add("?");
                    values.Add(name == "CarId" ? (object) this.CarId : name == "CamCockpitOffsetX" ? ox : name == "CamCockpitOffsetY" ? oy : name == "CamCockpitOffsetZ" ? oz : (object) 0.0);
                }
                db.Execute("INSERT INTO CameraOverrides (" + string.Join(", ", columns.ToArray()) + ") VALUES (" + string.Join(", ", marks.ToArray()) + ")", values.ToArray());
                done.Add(string.Format(CultureInfo.InvariantCulture, "CameraOverrides {0} added, cockpit offset {1}, {2}, {3}", this.CarId, ox, oy, oz));
            }
            else
            {
                db.Execute("UPDATE CameraOverrides SET CamCockpitOffsetX = ?, CamCockpitOffsetY = ?, CamCockpitOffsetZ = ? WHERE CarId = ?", ox, oy, oz, this.CarId);
                done.Add(string.Format(CultureInfo.InvariantCulture, "CameraOverrides {0} cockpit offset = {1}, {2}, {3}", this.CarId, ox, oy, oz));
            }
            this.HasOverrides = true;
            return done;
        }

        private static double Round(float value)
        {
            return Math.Round((double) value, 6);
        }

        private static bool HasColumn(SqliteDatabase db, string table, string column)
        {
            foreach (object[] row in db.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")").Rows)
            {
                if (string.Equals((string) row[1], column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // The steering wheel point (database axes) from a car model's steering_wheel section: the
        // _cockpit.carbin's in FM4 (the main and _lod0 models have the same part in the same place on
        // the cars measured, but the rule was fitted on the cockpit). False when there is none.
        public static bool SteeringWheelFromModel(CarbinModel model, out Vector3 point)
        {
            point = Vector3.Zero;
            CarbinModel.Section section = model.Find("steering_wheel");
            if (section == null)
            {
                return false;
            }
            float meanZ = (section.Unk1c != null && section.Unk1c.Length >= 16) ? ReadFloat(section.Unk1c, 12) : 0f;
            point = new Vector3(section.Bounds[0], section.Bounds[1] + (section.Bounds[7] / 3f), -(section.Bounds[2] + meanZ) + MeanZBias);
            return true;
        }

        // The median of part 9 minus the rim centre along Z on the 313 FM4 cars.
        private const float MeanZBias = 0.02f;

        private static float ReadFloat(byte[] data, int offset)
        {
            byte[] value = { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] };
            return BitConverter.ToSingle(value, 0);
        }
    }
}
