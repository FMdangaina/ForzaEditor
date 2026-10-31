namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // Viewport menu > Car Colour: the paint the car's body materials (body, misc, engine...) are drawn with, picked
    // from the car's factory colours in the game database (Combo_Colors) or any colour (override). It only changes
    // the viewport; Reset goes back to Viewer.BodyColor of Tools > Settings.
    public partial class ArchiveEditorForm
    {
        private ToolStripMenuItem mnuViewColor;

        private void AddCarColorMenu(ContextMenuStrip menu)
        {
            this.mnuViewColor = new ToolStripMenuItem("Car Colour");
            this.mnuViewColor.DropDownOpening += delegate { this.FillCarColorMenu(); };
            this.mnuViewColor.DropDownItems.Add(new ToolStripMenuItem("..."));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(this.mnuViewColor);
        }

        private void FillCarColorMenu()
        {
            ToolStripItemCollection items = this.mnuViewColor.DropDownItems;
            items.Clear();
            foreach (KeyValuePair<string, Color> chip in this.CarFactoryColors())
            {
                Color color = chip.Value;
                ToolStripMenuItem item = new ToolStripMenuItem(chip.Key + "  " + GameColor.Hex(color), SwatchOf(color), delegate { this.SetCarColor(color); });
                items.Add(item);
            }
            if (items.Count > 0)
            {
                items.Add(new ToolStripSeparator());
            }
            items.Add(new ToolStripMenuItem("Other Colour (override)...", null, delegate { this.PickCarColor(); }));
            ToolStripMenuItem reset = new ToolStripMenuItem("Default (Settings)", null, delegate { this.SetCarColor(null); });
            reset.Enabled = this.scene != null && this.scene.BodyColorOverride.HasValue;
            items.Add(reset);
            ForzaTheme.Apply(this.mnuViewColor.DropDown);
        }

        private static Bitmap SwatchOf(Color color)
        {
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(color);
                g.DrawRectangle(Pens.Gray, 0, 0, 15, 15);
            }
            return bitmap;
        }

        // The open car's rows of Combo_Colors (name from its colour class), none when the database or the car is missing.
        private List<KeyValuePair<string, Color>> CarFactoryColors()
        {
            List<KeyValuePair<string, Color>> list = new List<KeyValuePair<string, Color>>();
            try
            {
                string name = (this.currentEntry != null) ? this.currentEntry.Name : "";
                if (!name.ToLowerInvariant().EndsWith(".carbin"))
                {
                    return list;
                }
                string path;
                byte[] data = this.ReadGameDbFile(out path);
                if (data == null)
                {
                    return list;
                }
                using (SqliteDatabase db = SqliteDatabase.FromBytes(data))
                {
                    if (db.Scalar("SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'Combo_Colors'") == null)
                    {
                        return list;
                    }
                    object id = db.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", CarMediaName(name));
                    if (id == null)
                    {
                        return list;
                    }
                    foreach (object[] row in db.Query("SELECT Sequence, RGB, ColorClassId FROM Combo_Colors WHERE Ordinal = ? ORDER BY Sequence", Convert.ToInt64(id, CultureInfo.InvariantCulture)).Rows)
                    {
                        if (row[1] == null)
                        {
                            continue;
                        }
                        string label = "#" + Convert.ToString(row[0], CultureInfo.InvariantCulture);
                        try
                        {
                            object cls = (row[2] == null) ? null : db.Scalar("SELECT ColorClass FROM List_ColorClass WHERE ColorClassId = ?", Convert.ToInt64(row[2], CultureInfo.InvariantCulture));
                            if (cls != null)
                            {
                                label += " " + Convert.ToString(cls, CultureInfo.InvariantCulture);
                            }
                        }
                        catch (Exception)
                        {
                        }
                        list.Add(new KeyValuePair<string, Color>(label, GameColor.ToColor(Convert.ToInt64(row[1], CultureInfo.InvariantCulture))));
                    }
                }
            }
            catch (Exception)
            {
            }
            return list;
        }

        private void PickCarColor()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                if (this.scene != null && this.scene.BodyColorOverride.HasValue)
                {
                    Vector3 c = this.scene.BodyColorOverride.Value;
                    dialog.Color = Color.FromArgb((int) (c.X * 255f + 0.5f), (int) (c.Y * 255f + 0.5f), (int) (c.Z * 255f + 0.5f));
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    this.SetCarColor(dialog.Color);
                }
            }
        }

        private void SetCarColor(Color? color)
        {
            if (this.scene == null)
            {
                return;
            }
            this.scene.BodyColorOverride = color.HasValue ? new Vector3(color.Value.R / 255f, color.Value.G / 255f, color.Value.B / 255f) : (Vector3?) null;
            this.scene.Update(true);
        }
    }
}
