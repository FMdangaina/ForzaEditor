namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    // A car's whole model as a work folder of OBJs, one per material of each part at each LOD:
    //   lod0-bumperFa-body.obj      _lod0.carbin, LOD 0
    //   lod3-bumperFa-body.obj      main .carbin, LOD 3
    //   cockpit-seatL-cloth.obj     _cockpit.carbin
    // plus materials.mtl and model.txt (what was exported, with a hash per OBJ). Importing the folder
    // back rebuilds only what changed: edited, added or deleted OBJs. An OBJ with a new part name adds
    // that part (template: the nearest part of the same file), and a part whose OBJs were all deleted
    // is removed.
    public static class CarModelFolder
    {
        public const string ManifestName = "model.txt";
        public const string MaterialsName = "materials.mtl";

        // Model file kinds as CarModelFiles names them.
        public const string Main = "";
        public const string Lod0 = "_lod0";
        public const string Cockpit = "_cockpit";

        private static readonly Regex NamePattern = new Regex(@"^(lod\d+|cockpit)-(.+)-([^-]+)\.obj$", RegexOptions.IgnoreCase);

        public static string FileName(string kind, uint lod, string part, string material)
        {
            return ((kind == Cockpit) ? "cockpit" : ("lod" + lod)) + "-" + part + "-" + material + ".obj";
        }

        // Splits "lod0-bumperFa-body.obj" into model file kind, LOD, part and material.
        public static bool ParseName(string fileName, out string kind, out uint lod, out string part, out string material)
        {
            kind = null;
            lod = 0;
            part = null;
            material = null;
            Match match = NamePattern.Match(fileName);
            if (!match.Success)
            {
                return false;
            }
            string prefix = match.Groups[1].Value.ToLowerInvariant();
            if (prefix == "cockpit")
            {
                kind = Cockpit;
            }
            else
            {
                lod = uint.Parse(prefix.Substring(3), CultureInfo.InvariantCulture);
                kind = (lod == 0) ? Lod0 : Main;
            }
            part = match.Groups[2].Value;
            material = match.Groups[3].Value;
            return true;
        }

        private static string Hash(string path)
        {
            using (MD5 md5 = MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
            }
        }

        #region Export

        // Writes every part of every model file. Returns the number of OBJs written. OBJs of an earlier
        // export in the folder are deleted first, so the folder always matches the manifest.
        public static int Export(Dictionary<string, CarbinModel> models, Dictionary<string, string> fileNames, string folder, string source)
        {
            Directory.CreateDirectory(folder);
            foreach (string old in Directory.GetFiles(folder, "*.obj"))
            {
                string kind;
                uint lod;
                string part;
                string material;
                if (ParseName(Path.GetFileName(old), out kind, out lod, out part, out material))
                {
                    File.Delete(old);
                }
            }
            List<string> materials = new List<string>();
            StringBuilder manifest = new StringBuilder();
            manifest.AppendLine("# ForzaEditor model folder, exported from " + source);
            manifest.AppendLine("# One OBJ per material of each part at each LOD: <lod0|lod1..|cockpit>-<part>-<material>.obj");
            manifest.AppendLine("# Edit, add or delete OBJs, then Import Model From Folder: only changed OBJs are rebuilt.");
            manifest.AppendLine("# A new part name adds a part; deleting all OBJs of a part removes it. Do not edit the lines below.");
            foreach (string kind in new string[] { Main, Lod0, Cockpit })
            {
                if (models.ContainsKey(kind))
                {
                    manifest.AppendLine("file " + ((kind.Length == 0) ? "main" : kind.Substring(1)) + " " + fileNames[kind]);
                }
            }
            int count = 0;
            foreach (string kind in new string[] { Main, Lod0, Cockpit })
            {
                CarbinModel model;
                if (!models.TryGetValue(kind, out model))
                {
                    continue;
                }
                foreach (CarbinModel.Section section in model.Sections)
                {
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        string name = FileName(kind, sub.Lod, section.Name, sub.Name);
                        string path = Path.Combine(folder, name);
                        ObjMesh.ExportSubSection(section, sub, path, "ForzaEditor: " + section.Name + " / " + sub.Name + " from " + fileNames[kind], MaterialsName);
                        manifest.AppendLine("obj " + Hash(path) + " " + name);
                        if (!materials.Contains(sub.Name))
                        {
                            materials.Add(sub.Name);
                        }
                        count++;
                    }
                }
            }
            materials.Sort(StringComparer.OrdinalIgnoreCase);
            ObjMesh.WriteMtl(Path.Combine(folder, MaterialsName), materials);
            File.WriteAllText(Path.Combine(folder, ManifestName), manifest.ToString(), new UTF8Encoding(false));
            return count;
        }

        #endregion

        #region Import plan

        // What an import does to one part of one model file.
        public class PartPlan
        {
            public string Kind;
            public string Part;
            public bool IsNew;
            public bool Remove;
            public string Template;                     // new parts: the part copied for materials, damage data and draw lists
            public List<string> Templates = new List<string>();   // the choices (parts of the same file)
            public string BorrowedFrom;                 // new part with no OBJ in this file: geometry of the other file
            public SortedDictionary<uint, List<string>> Rebuild = new SortedDictionary<uint, List<string>>();   // LOD -> OBJ paths
            public List<uint> Keep = new List<uint>();
            public List<uint> Drop = new List<uint>();

            public string Describe()
            {
                string file = (this.Kind == Main) ? "main" : this.Kind.Substring(1);
                if (this.Remove)
                {
                    return string.Format("{0} {1}: remove (all its OBJs deleted)", file, this.Part);
                }
                List<string> parts = new List<string>();
                if (this.BorrowedFrom != null)
                {
                    parts.Add("built at every LOD of its template");
                }
                else if (this.Rebuild.Count > 0)
                {
                    parts.Add("rebuild LOD " + Join(this.Rebuild.Keys));
                }
                if (this.Drop.Count > 0)
                {
                    parts.Add("drop LOD " + Join(this.Drop));
                }
                if (this.Keep.Count > 0 && !this.IsNew)
                {
                    parts.Add("keep LOD " + Join(this.Keep));
                }
                string what = string.Join(", ", parts.ToArray());
                if (this.IsNew)
                {
                    return string.Format("{0} {1}: NEW part, {2}{3}", file, this.Part, what, (this.BorrowedFrom != null) ? " (geometry from the " + this.BorrowedFrom + " OBJs)" : "");
                }
                return string.Format("{0} {1}: {2}", file, this.Part, what);
            }
        }

        public class Plan
        {
            public string Folder;
            public bool HasManifest;
            public List<PartPlan> Parts = new List<PartPlan>();
            public List<string> Ignored = new List<string>();     // OBJs whose name does not follow the scheme
            public List<string> Errors = new List<string>();      // problems that block the import
            public List<string> Warnings = new List<string>();

            public bool IsEmpty
            {
                get { return this.Parts.Count == 0; }
            }
        }

        private class Found
        {
            public string Kind;
            public uint Lod;
            public string Part;
            public string Material;
            public string Path;
            public string Name;
        }

        private static string Join(IEnumerable<uint> values)
        {
            List<string> text = new List<string>();
            foreach (uint value in values)
            {
                text.Add(value.ToString(CultureInfo.InvariantCulture));
            }
            return string.Join(",", text.ToArray());
        }

        // Compares the folder with the models (and the manifest of the export, when there is one).
        // Without a manifest every OBJ counts as changed and nothing is removed.
        public static Plan Prepare(string folder, Dictionary<string, CarbinModel> models)
        {
            Plan plan = new Plan();
            plan.Folder = folder;

            // Exported OBJs and their hashes.
            Dictionary<string, string> exported = null;
            string manifestPath = Path.Combine(folder, ManifestName);
            if (File.Exists(manifestPath))
            {
                exported = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(manifestPath))
                {
                    string[] fields = line.Split(new char[] { ' ' }, 3);
                    if (fields.Length == 3 && fields[0] == "obj")
                    {
                        exported[fields[2].Trim()] = fields[1];
                    }
                }
                plan.HasManifest = true;
            }

            // Car materials (any model file): an OBJ must use one of them.
            Dictionary<string, bool> carMaterials = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (CarbinModel model in models.Values)
            {
                foreach (CarbinModel.Section section in model.Sections)
                {
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        carMaterials[sub.Name] = true;
                    }
                }
            }

            // OBJs in the folder, by kind|part (part spelled as the model spells it when it has it).
            Dictionary<string, List<Found>> byPart = new Dictionary<string, List<Found>>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(folder, "*.obj"))
            {
                Found found = new Found();
                found.Path = path;
                found.Name = Path.GetFileName(path);
                if (!ParseName(found.Name, out found.Kind, out found.Lod, out found.Part, out found.Material))
                {
                    plan.Ignored.Add(found.Name);
                    continue;
                }
                if (!models.ContainsKey(found.Kind))
                {
                    plan.Errors.Add(found.Name + ": the car has no " + ((found.Kind == Main) ? "main" : found.Kind.Substring(1)) + " model file");
                    continue;
                }
                if (!carMaterials.ContainsKey(found.Material))
                {
                    plan.Errors.Add(found.Name + ": the car has no material \"" + found.Material + "\" (names are case-sensitive)");
                    continue;
                }
                CarbinModel.Section existing = FindIgnoreCase(models[found.Kind], found.Part);
                if (existing != null)
                {
                    found.Part = existing.Name;
                }
                string key = found.Kind + "|" + found.Part;
                if (!byPart.ContainsKey(key))
                {
                    byPart[key] = new List<Found>();
                }
                byPart[key].Add(found);
            }

            // Exported names by kind|part|lod.
            Dictionary<string, List<string>> exportedByLod = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (exported != null)
            {
                foreach (string name in exported.Keys)
                {
                    string kind;
                    uint lod;
                    string part;
                    string material;
                    if (ParseName(name, out kind, out lod, out part, out material))
                    {
                        string key = kind + "|" + part + "|" + lod;
                        if (!exportedByLod.ContainsKey(key))
                        {
                            exportedByLod[key] = new List<string>();
                        }
                        exportedByLod[key].Add(name);
                    }
                }
            }

            // Parts the model has.
            foreach (string kind in new string[] { Main, Lod0, Cockpit })
            {
                CarbinModel model;
                if (!models.TryGetValue(kind, out model))
                {
                    continue;
                }
                foreach (CarbinModel.Section section in model.Sections)
                {
                    List<Found> present;
                    byPart.TryGetValue(kind + "|" + section.Name, out present);
                    PartPlan part = new PartPlan();
                    part.Kind = kind;
                    part.Part = section.Name;
                    List<uint> lods = new List<uint>();
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        if (!lods.Contains(sub.Lod))
                        {
                            lods.Add(sub.Lod);
                        }
                    }
                    if (present != null)
                    {
                        foreach (Found found in present)
                        {
                            if (!lods.Contains(found.Lod))
                            {
                                lods.Add(found.Lod);
                            }
                        }
                    }
                    lods.Sort();
                    foreach (uint lod in lods)
                    {
                        List<string> paths = new List<string>();
                        List<string> names = new List<string>();
                        if (present != null)
                        {
                            foreach (Found found in present)
                            {
                                if (found.Lod == lod)
                                {
                                    paths.Add(found.Path);
                                    names.Add(found.Name);
                                }
                            }
                        }
                        List<string> was;
                        exportedByLod.TryGetValue(kind + "|" + section.Name + "|" + lod, out was);
                        if (exported == null)
                        {
                            if (paths.Count > 0)
                            {
                                part.Rebuild[lod] = paths;
                            }
                            else
                            {
                                part.Keep.Add(lod);
                            }
                        }
                        else if (paths.Count == 0)
                        {
                            if (was != null)
                            {
                                part.Drop.Add(lod);
                            }
                            else
                            {
                                part.Keep.Add(lod);
                            }
                        }
                        else if (Unchanged(names, paths, was, exported))
                        {
                            part.Keep.Add(lod);
                        }
                        else
                        {
                            part.Rebuild[lod] = paths;
                        }
                    }
                    if (part.Rebuild.Count == 0 && part.Drop.Count == 0)
                    {
                        continue;
                    }
                    part.Remove = part.Rebuild.Count == 0 && part.Keep.Count == 0;
                    plan.Parts.Add(part);
                }
            }

            // New parts.
            Dictionary<string, PartPlan> added = new Dictionary<string, PartPlan>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<Found>> pair in byPart)
            {
                Found first = pair.Value[0];
                if (models[first.Kind].Find(first.Part) != null)
                {
                    continue;
                }
                PartPlan part = new PartPlan();
                part.Kind = first.Kind;
                part.Part = first.Part;
                part.IsNew = true;
                foreach (Found found in pair.Value)
                {
                    if (!part.Rebuild.ContainsKey(found.Lod))
                    {
                        part.Rebuild[found.Lod] = new List<string>();
                    }
                    part.Rebuild[found.Lod].Add(found.Path);
                }
                plan.Parts.Add(part);
                added[pair.Key] = part;
            }

            // A new part must be in the main file (far LODs) and in the lod0 file (up close): when the
            // OBJs cover only one of them, the other uses the same geometry at every LOD of its template.
            List<PartPlan> borrowed = new List<PartPlan>();
            foreach (PartPlan part in added.Values)
            {
                if (part.Kind == Cockpit)
                {
                    continue;
                }
                string other = (part.Kind == Main) ? Lod0 : Main;
                if (!models.ContainsKey(other) || models[other].Find(part.Part) != null || added.ContainsKey(other + "|" + part.Part))
                {
                    continue;
                }
                PartPlan copy = new PartPlan();
                copy.Kind = other;
                copy.Part = part.Part;
                copy.IsNew = true;
                copy.BorrowedFrom = (part.Kind == Main) ? "lod" + FirstKey(part.Rebuild) : "lod0";
                copy.Rebuild[uint.MaxValue] = part.Rebuild[FirstKey(part.Rebuild)];   // LODs set from the template on import
                borrowed.Add(copy);
            }
            plan.Parts.AddRange(borrowed);

            // Templates: the existing part of the same file nearest to the new geometry.
            foreach (PartPlan part in plan.Parts)
            {
                if (!part.IsNew)
                {
                    continue;
                }
                CarbinModel model = models[part.Kind];
                Vector3 center = Center(part.Rebuild[FirstKey(part.Rebuild)]);
                double best = double.MaxValue;
                foreach (CarbinModel.Section section in model.Sections)
                {
                    if (section.SubSections.Count == 0)
                    {
                        continue;
                    }
                    part.Templates.Add(section.Name);
                    double dx = section.Bounds[0] - center.X;
                    double dy = section.Bounds[1] - center.Y;
                    double dz = section.Bounds[2] - center.Z;
                    double distance = (dx * dx) + (dy * dy) + (dz * dz);
                    if (distance < best)
                    {
                        best = distance;
                        part.Template = section.Name;
                    }
                }
                part.Templates.Sort(StringComparer.OrdinalIgnoreCase);
                if (part.Template == null)
                {
                    plan.Errors.Add(part.Part + ": no part in the file to use as a template");
                }
            }

            if (!plan.HasManifest)
            {
                plan.Warnings.Add("No " + ManifestName + " in the folder: every OBJ is rebuilt and no part is removed.");
            }
            return plan;
        }

        private static uint FirstKey(SortedDictionary<uint, List<string>> map)
        {
            foreach (uint key in map.Keys)
            {
                return key;
            }
            return 0;
        }

        private static bool Unchanged(List<string> names, List<string> paths, List<string> was, Dictionary<string, string> exported)
        {
            if (was == null || was.Count != names.Count)
            {
                return false;
            }
            for (int i = 0; i < names.Count; i++)
            {
                string hash;
                if (!exported.TryGetValue(names[i], out hash) || hash != Hash(paths[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static CarbinModel.Section FindIgnoreCase(CarbinModel model, string name)
        {
            foreach (CarbinModel.Section section in model.Sections)
            {
                if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return section;
                }
            }
            return null;
        }

        // Center of the bounding box of some OBJs (vertex lines only).
        private static Vector3 Center(List<string> paths)
        {
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            foreach (string path in paths)
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    if (!line.StartsWith("v "))
                    {
                        continue;
                    }
                    string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    Vector3 p = new Vector3(float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            return (min.X > max.X) ? Vector3.Zero : (min + max) / 2;
        }

        #endregion

        #region Import

        // Applies the plan to the models; returns the kinds of the models changed and fills the report.
        public static List<string> Apply(Plan plan, Dictionary<string, CarbinModel> models, StringBuilder report)
        {
            List<string> changed = new List<string>();
            Dictionary<string, ObjMesh> loaded = new Dictionary<string, ObjMesh>();
            // New parts first: their templates are copied before any part is rebuilt or removed.
            List<PartPlan> order = new List<PartPlan>();
            foreach (PartPlan part in plan.Parts)
            {
                if (part.IsNew)
                {
                    order.Add(part);
                }
            }
            foreach (PartPlan part in plan.Parts)
            {
                if (!part.IsNew)
                {
                    order.Add(part);
                }
            }
            foreach (PartPlan part in order)
            {
                CarbinModel model = models[part.Kind];
                string file = (part.Kind == Main) ? "main" : part.Kind.Substring(1);
                if (part.Remove)
                {
                    model.RemoveSection(part.Part);
                    report.AppendLine(file + " " + part.Part + ": removed");
                }
                else
                {
                    Dictionary<uint, ObjMesh> meshes = new Dictionary<uint, ObjMesh>();
                    if (part.IsNew)
                    {
                        model.DuplicateSection(part.Template, part.Part);
                    }
                    if (part.Rebuild.ContainsKey(uint.MaxValue))
                    {
                        // Borrowed geometry: one mesh at every LOD of the template.
                        ObjMesh mesh = LoadAs(part.Rebuild[uint.MaxValue], loaded);
                        foreach (CarbinModel.SubSection sub in model.Find(part.Part).SubSections)
                        {
                            meshes[sub.Lod] = mesh;
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<uint, List<string>> pair in part.Rebuild)
                        {
                            meshes[pair.Key] = LoadAs(pair.Value, loaded);
                        }
                        foreach (uint lod in part.Keep)
                        {
                            meshes[lod] = null;
                        }
                    }
                    if (meshes.Count == 0)
                    {
                        model.RemoveSection(part.Part);
                        report.AppendLine(file + " " + part.Part + ": removed (no LOD left)");
                    }
                    else
                    {
                        string done = model.RebuildLods(part.Part, meshes, null, false);
                        report.AppendLine(file + " " + (part.IsNew ? "NEW " : "") + done.Replace("\r\n", "\r\n    "));
                    }
                }
                if (!changed.Contains(part.Kind))
                {
                    changed.Add(part.Kind);
                }
            }
            return changed;
        }

        // The OBJs of one part LOD as one mesh, each file's triangles under the material its name says
        // (whatever usemtl the editor wrote).
        private static ObjMesh LoadAs(List<string> paths, Dictionary<string, ObjMesh> loaded)
        {
            string key = string.Join("|", paths.ToArray());
            ObjMesh mesh;
            if (loaded.TryGetValue(key, out mesh))
            {
                return mesh;
            }
            mesh = new ObjMesh();
            foreach (string path in paths)
            {
                string kind;
                uint lod;
                string part;
                string material;
                ParseName(Path.GetFileName(path), out kind, out lod, out part, out material);
                ObjMesh one;
                try
                {
                    one = ObjMesh.Load(path);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(Path.GetFileName(path) + ": " + exception.Message);
                }
                ObjMesh.Material target = null;
                foreach (ObjMesh.Material existing in mesh.Materials)
                {
                    if (existing.Name == material)
                    {
                        target = existing;
                    }
                }
                if (target == null)
                {
                    target = new ObjMesh.Material();
                    target.Name = material;
                    mesh.Materials.Add(target);
                }
                foreach (ObjMesh.Material group in one.Materials)
                {
                    target.Triangles.AddRange(group.Triangles);
                }
            }
            loaded[key] = mesh;
            return mesh;
        }

        #endregion
    }
}
