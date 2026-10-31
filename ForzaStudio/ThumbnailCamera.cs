namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Xml;

    // GAME:\Media\ThumbnailCamSettings.xml: the camera the game takes a car's thumbnail with.
    // Rows with ordinal -1 are the defaults, rows with a car's Data_Car.Id replace them for that car
    // (the long cars sit further away). Two views per car: the 3/4 front one (608x288) and the side one
    // (288x160, the strip at the right of the 768x288 thumbnail). Position and facing are in the car's
    // database axes (Z forward) like the garage presets; FOV is vertical. That the game reads it for
    // every thumbnail is inferred from the values, not checked in game.
    public sealed class ThumbnailCamera
    {
        public const int MainWidth = 608;
        public const int MainHeight = 288;
        public const int SideWidth = 288;
        public const int SideHeight = 160;

        public long Ordinal;       // -1 = default
        public int Width;
        public int Height;
        public Vector3 Position;
        public Vector3 Facing;
        public float Fov;
        public float Roll;

        public ThumbnailCamera Clone()
        {
            return (ThumbnailCamera) this.MemberwiseClone();
        }

        // The point the camera looks at where its line crosses the car's middle plane (x = 0).
        public Vector3 AimPoint
        {
            get
            {
                float t = (Math.Abs(this.Facing.X) < 1e-4f) ? this.Position.Length() : -this.Position.X / this.Facing.X;
                return this.Position + (this.Facing * t);
            }
        }

        public float Distance
        {
            get { return (this.AimPoint - this.Position).Length(); }
        }

        // The row for a car and view: its own, else the default of that size. Null when the file has neither.
        public static ThumbnailCamera Find(List<ThumbnailCamera> all, long car, int width, int height, out bool own)
        {
            own = false;
            ThumbnailCamera fallback = null;
            foreach (ThumbnailCamera c in all)
            {
                if (c.Width != width || c.Height != height)
                {
                    continue;
                }
                if (c.Ordinal == car)
                {
                    own = true;
                    return c;
                }
                if (c.Ordinal == -1)
                {
                    fallback = c;
                }
            }
            return fallback;
        }

        public static string PathFor(string gameRoot)
        {
            return (gameRoot == null) ? null : Path.Combine(Path.Combine(gameRoot, "Media"), "ThumbnailCamSettings.xml");
        }

        public static List<ThumbnailCamera> Load(string path)
        {
            List<ThumbnailCamera> list = new List<ThumbnailCamera>();
            XmlDocument document = new XmlDocument();
            document.Load(path);
            foreach (XmlElement element in document.GetElementsByTagName("Thumb"))
            {
                list.Add(FromElement(element));
            }
            return list;
        }

        private static ThumbnailCamera FromElement(XmlElement e)
        {
            ThumbnailCamera c = new ThumbnailCamera();
            c.Ordinal = (long) Number(e, "ordinal");
            c.Width = (int) Number(e, "width");
            c.Height = (int) Number(e, "height");
            c.Position = new Vector3(Number(e, "posx"), Number(e, "posy"), Number(e, "posz"));
            c.Facing = new Vector3(Number(e, "facex"), Number(e, "facey"), Number(e, "facez"));
            c.Fov = Number(e, "fov");
            c.Roll = Number(e, "roll");
            return c;
        }

        private static float Number(XmlElement e, string name)
        {
            float value;
            return float.TryParse(e.GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : 0f;
        }

        private static void Fill(XmlElement e, ThumbnailCamera c)
        {
            string[] names = { "ordinal", "width", "height", "posx", "posy", "posz", "facex", "facey", "facez", "fov", "roll" };
            string[] values =
            {
                c.Ordinal.ToString(CultureInfo.InvariantCulture), c.Width.ToString(CultureInfo.InvariantCulture), c.Height.ToString(CultureInfo.InvariantCulture),
                F(c.Position.X), F(c.Position.Y), F(c.Position.Z), F(c.Facing.X), F(c.Facing.Y), F(c.Facing.Z), F(c.Fov), F(c.Roll)
            };
            for (int i = 0; i < names.Length; i++)
            {
                e.SetAttribute(names[i], values[i]);
            }
        }

        private static string F(float value)
        {
            return value.ToString("0.000000", CultureInfo.InvariantCulture);
        }

        // Writes the car's row for that size (replaced or added in order), or removes it when camera is null.
        // The caller backs the file up first. Returns a one line description.
        public static string Save(string path, long car, int width, int height, ThumbnailCamera camera)
        {
            XmlDocument document = new XmlDocument();
            document.PreserveWhitespace = false;
            document.Load(path);
            XmlElement root = document.DocumentElement;
            XmlElement found = null;
            XmlElement before = null;
            foreach (XmlElement e in root.GetElementsByTagName("Thumb"))
            {
                ThumbnailCamera c = FromElement(e);
                if (c.Ordinal == car && c.Width == width && c.Height == height)
                {
                    found = e;
                }
                else if (before == null && (c.Ordinal > car || (c.Ordinal == car && c.Width > width)))
                {
                    before = e;
                }
            }
            string text;
            if (camera == null)
            {
                if (found != null)
                {
                    root.RemoveChild(found);
                }
                text = string.Format("ThumbnailCamSettings {0}x{1} row of car {2} removed (default again)", width, height, car);
            }
            else
            {
                ThumbnailCamera row = camera.Clone();
                row.Ordinal = car;
                row.Width = width;
                row.Height = height;
                if (found == null)
                {
                    found = document.CreateElement("Thumb");
                    root.InsertBefore(found, before);
                }
                Fill(found, row);
                text = string.Format(CultureInfo.InvariantCulture, "ThumbnailCamSettings {0}x{1} row of car {2}: distance {3:0.00} m, FOV {4:0.00}", width, height, car, row.Distance, row.Fov);
            }
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.OmitXmlDeclaration = true;
            settings.Encoding = new UTF8Encoding(false);
            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                document.Save(writer);
            }
            return text;
        }
    }
}
