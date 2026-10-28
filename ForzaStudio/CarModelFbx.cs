namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    // The car's model as one FBX scene with the model's own hierarchy:
    //   <car>                       (Null)
    //     main | lod0 | cockpit     (Null: the model file)
    //       LOD1 ... LOD5 | LOD0    (Null)
    //         <part>                (Null)
    //           <part>__<material>  (Mesh: the part's triangles with that material)
    // Edit it in 3ds Max, Blender...; the import takes every mesh below a kind and LOD node, applies the
    // transforms of its ancestors, and hands the parts to the same import as the OBJ folders (CarModelFolder).
    // Only those minimal nodes are needed: a LOD the file has no node for is left alone, a part missing
    // from a LOD the file does have is left alone too (dropped there only when the import is asked to), a new
    // part is a new group (or a mesh) under a LOD node. The export can write chosen model files / LODs only.
    public static class CarModelFbx
    {
        private static readonly Regex LodNode = new Regex(@"^lod(\d+)$", RegexOptions.IgnoreCase);
        private static readonly Regex CopySuffix = new Regex(@"(\.\d{3}|_\d{3}|\s\(\d+\))$");
        private const string Separator = "__";

        // 'Name.001' (Blender) and 'Name_001' (Max) copies are the same name.
        public static string StripCopy(string name)
        {
            return CopySuffix.Replace(name, "");
        }

        // A name the car uses is kept as is (parts can end in _001); any other loses its copy suffix.
        private static string Clean(string name, Dictionary<string, bool> known)
        {
            return known.ContainsKey(name) ? name : StripCopy(name);
        }

        private static string KindOf(string name)
        {
            switch (StripCopy(name).ToLowerInvariant())
            {
                case "main": return CarModelFolder.Main;
                case "lod0": return CarModelFolder.Lod0;
                case "cockpit": return CarModelFolder.Cockpit;
            }
            return null;
        }

        #region Export

        // A model file at one LOD: what the export dialog offers.
        public class Level
        {
            public string Kind;
            public uint Lod;
            public int Parts;

            public string Key
            {
                get { return LevelKey(this.Kind, this.Lod); }
            }

            public string Describe()
            {
                return string.Format("{0} file, LOD {1} ({2} parts)", CarModelFolder.KindName(this.Kind), this.Lod, this.Parts);
            }
        }

        public static string LevelKey(string kind, uint lod)
        {
            return kind + "|" + lod.ToString(CultureInfo.InvariantCulture);
        }

        // Every model file / LOD the car has, main, lod0 and cockpit in that order.
        public static List<Level> Levels(Dictionary<string, CarbinModel> models)
        {
            List<Level> levels = new List<Level>();
            foreach (string kind in new string[] { CarModelFolder.Main, CarModelFolder.Lod0, CarModelFolder.Cockpit })
            {
                CarbinModel model;
                if (!models.TryGetValue(kind, out model))
                {
                    continue;
                }
                SortedDictionary<uint, int> counts = new SortedDictionary<uint, int>();
                foreach (CarbinModel.Section section in model.Sections)
                {
                    List<uint> seen = new List<uint>();
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        if (!seen.Contains(sub.Lod))
                        {
                            seen.Add(sub.Lod);
                            int count;
                            counts.TryGetValue(sub.Lod, out count);
                            counts[sub.Lod] = count + 1;
                        }
                    }
                }
                foreach (KeyValuePair<uint, int> pair in counts)
                {
                    Level level = new Level();
                    level.Kind = kind;
                    level.Lod = pair.Key;
                    level.Parts = pair.Value;
                    levels.Add(level);
                }
            }
            return levels;
        }

        // kind -> .carbin name / parsed model. 'wanted' lists the LevelKeys to write (null = all).
        // Returns the number of mesh nodes written.
        public static int Export(Dictionary<string, string> names, Dictionary<string, CarbinModel> models, string path, string source, List<string> wanted)
        {
            string car = CarModelFolder.FolderName(names.ContainsKey(CarModelFolder.Main) ? names[CarModelFolder.Main] : First(names));
            FbxBuilder builder = new FbxBuilder("ForzaStudio EVO: " + source);
            long root = builder.AddNull(car, 0);
            int count = 0;
            foreach (string kind in new string[] { CarModelFolder.Main, CarModelFolder.Lod0, CarModelFolder.Cockpit })
            {
                CarbinModel model;
                if (!models.TryGetValue(kind, out model))
                {
                    continue;
                }
                SortedDictionary<uint, long> lodNodes = new SortedDictionary<uint, long>();
                foreach (CarbinModel.Section section in model.Sections)
                {
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        if (Wanted(wanted, kind, sub.Lod))
                        {
                            lodNodes[sub.Lod] = 0;
                        }
                    }
                }
                if (lodNodes.Count == 0)
                {
                    continue;
                }
                long kindNode = builder.AddNull(CarModelFolder.KindName(kind), root);
                List<uint> lods = new List<uint>(lodNodes.Keys);
                foreach (uint lod in lods)
                {
                    lodNodes[lod] = builder.AddNull("LOD" + lod.ToString(CultureInfo.InvariantCulture), kindNode);
                }
                foreach (CarbinModel.Section section in model.Sections)
                {
                    List<uint> partLods = new List<uint>();
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        if (!partLods.Contains(sub.Lod))
                        {
                            partLods.Add(sub.Lod);
                        }
                    }
                    foreach (uint lod in partLods)
                    {
                        if (!lodNodes.ContainsKey(lod))
                        {
                            continue;
                        }
                        long part = builder.AddNull(section.Name, lodNodes[lod]);
                        foreach (ObjMesh.Material material in ObjMesh.FromLod(section, lod).Materials)
                        {
                            builder.AddMesh(section.Name + Separator + material.Name, part, material.Name, material);
                            count++;
                        }
                    }
                }
            }
            File.WriteAllBytes(path, builder.ToBytes());
            return count;
        }

        private static bool Wanted(List<string> wanted, string kind, uint lod)
        {
            return wanted == null || wanted.Contains(LevelKey(kind, lod));
        }

        private static string First(Dictionary<string, string> names)
        {
            foreach (string name in names.Values)
            {
                return name;
            }
            return "car";
        }

        #endregion

        #region Import

        private class Found
        {
            public string Kind;
            public uint Lod;
            public string Part;
            public ObjMesh Mesh = new ObjMesh();
        }

        // Reads the FBX and lays it out as OBJ model folders in a new temporary folder: the car's own
        // export, with the OBJs of every part the FBX changed, added or dropped replaced. The result is
        // imported like any OBJ folder (CarModelFolder.Prepare / Apply). 'folders' maps kind -> folder;
        // 'root' is the temporary folder to delete afterwards. Notes are the findings of the node check.
        public static void PrepareFolders(string fbxPath, Dictionary<string, string> names, Dictionary<string, CarbinModel> models,
            bool dropMissing, out Dictionary<string, string> folders, out string root, List<string> notes, List<string> errors)
        {
            root = Path.Combine(Path.GetTempPath(), "ForzaStudio_fbx_" + Guid.NewGuid().ToString("N"));
            folders = new Dictionary<string, string>();
            FbxScene scene = FbxScene.Load(File.ReadAllBytes(fbxPath));

            Dictionary<string, bool> carMaterials = new Dictionary<string, bool>(StringComparer.Ordinal);
            Dictionary<string, bool> knownNames = new Dictionary<string, bool>(StringComparer.Ordinal);   // parts and materials of the car
            foreach (CarbinModel model in models.Values)
            {
                foreach (CarbinModel.Section section in model.Sections)
                {
                    knownNames[section.Name] = true;
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        carMaterials[sub.Name] = true;
                        knownNames[sub.Name] = true;
                    }
                }
            }

            // Every mesh under a kind node and a LOD node, grouped by kind, LOD and part.
            Dictionary<string, Found> found = new Dictionary<string, Found>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> lodsSeen = new Dictionary<string, bool>();
            List<string> outside = new List<string>();
            int meshes = 0;
            foreach (FbxScene.Model node in scene.ById.Values)
            {
                if (node.Children.Count > 0 && !scene.HasMesh(node))
                {
                    // Pure group; only its meshes count. Remember the LOD levels the file has.
                    Match lodMatch = LodNode.Match(StripCopy(node.Name));
                    if (lodMatch.Success)
                    {
                        string kindOfLod = KindAbove(node);
                        if (kindOfLod != null)
                        {
                            lodsSeen[kindOfLod + "|" + uint.Parse(lodMatch.Groups[1].Value, CultureInfo.InvariantCulture)] = true;
                        }
                    }
                }
                if (!scene.HasMesh(node))
                {
                    continue;
                }
                meshes++;
                string kind = null;
                uint lod = 0;
                FbxScene.Model partNode = null;
                for (FbxScene.Model walk = node; walk != null; walk = walk.Parent)
                {
                    Match match = LodNode.Match(StripCopy(walk.Name));
                    if (match.Success && walk.Parent != null && KindOf(walk.Parent.Name) != null && partNode != null)
                    {
                        lod = uint.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                        kind = KindOf(walk.Parent.Name);
                        break;
                    }
                    partNode = walk;
                }
                if (kind == null)
                {
                    outside.Add(node.Name);
                    continue;
                }
                lodsSeen[kind + "|" + lod] = true;

                // The part is the node right below the LOD node; a mesh straight below it is a part of its own.
                string partName = partNode.Name;
                if (partNode == node)
                {
                    int cut = partName.IndexOf(Separator, StringComparison.Ordinal);
                    if (cut > 0)
                    {
                        partName = partName.Substring(0, cut);
                    }
                }
                partName = Clean(partName, knownNames);
                string fallback = null;
                int at = node.Name.IndexOf(Separator, StringComparison.Ordinal);
                if (at >= 0)
                {
                    fallback = Clean(node.Name.Substring(at + Separator.Length), knownNames);
                }
                // A mesh's materials: the ones the file names, unless they are not the car's and the node name has one that is.
                for (int i = 0; i < node.Materials.Count; i++)
                {
                    node.Materials[i] = Clean(node.Materials[i], knownNames);
                    if (!carMaterials.ContainsKey(node.Materials[i]) && fallback != null && carMaterials.ContainsKey(fallback) && node.Materials.Count == 1)
                    {
                        node.Materials[i] = fallback;
                    }
                }
                string key = kind + "|" + lod + "|" + partName;
                Found target;
                if (!found.TryGetValue(key, out target))
                {
                    target = new Found();
                    target.Kind = kind;
                    target.Lod = lod;
                    target.Part = partName;
                    found[key] = target;
                }
                foreach (ObjMesh.Material material in scene.ReadMesh(node, fallback ?? partName))
                {
                    ObjMesh.Material into = null;
                    foreach (ObjMesh.Material have in target.Mesh.Materials)
                    {
                        if (have.Name == material.Name)
                        {
                            into = have;
                        }
                    }
                    if (into == null)
                    {
                        target.Mesh.Materials.Add(material);
                    }
                    else
                    {
                        into.Triangles.AddRange(material.Triangles);
                    }
                }
            }
            if (outside.Count > 0)
            {
                notes.Add(string.Format("{0} mesh nodes are not below a main / lod0 / cockpit node and a LODn node, ignored: {1}", outside.Count, Short(outside)));
            }
            if (found.Count == 0)
            {
                errors.Add("No mesh found under a model file node (main, lod0, cockpit) and a LOD node (LOD0, LOD1...). Export the model to FBX first and edit that scene.");
                return;
            }

            // The car's own export as the base.
            foreach (KeyValuePair<string, CarbinModel> pair in models)
            {
                string folder = Path.Combine(root, CarModelFolder.FolderName(names[pair.Key]));
                CarModelFolder.Export(pair.Value, names[pair.Key], folder, "FBX import");
                folders[pair.Key] = folder;
            }

            int same = 0;
            int changed = 0;
            int added = 0;
            int dropped = 0;
            int absent = 0;
            List<string> droppedNames = new List<string>();
            foreach (KeyValuePair<string, CarbinModel> pair in models)
            {
                string kind = pair.Key;
                string folder = folders[kind];
                // Parts the model has at the LODs the file has.
                Dictionary<string, bool> known = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (CarbinModel.Section section in pair.Value.Sections)
                {
                    known[section.Name] = true;
                    List<uint> lods = new List<uint>();
                    foreach (CarbinModel.SubSection sub in section.SubSections)
                    {
                        if (!lods.Contains(sub.Lod))
                        {
                            lods.Add(sub.Lod);
                        }
                    }
                    foreach (uint lod in lods)
                    {
                        if (!lodsSeen.ContainsKey(kind + "|" + lod))
                        {
                            continue;
                        }
                        string obj = ObjPath(folder, lod, section.Name);
                        Found mine;
                        if (!found.TryGetValue(kind + "|" + lod + "|" + section.Name, out mine))
                        {
                            // A scene with only some parts is the normal way to work; dropping is asked for.
                            if (dropMissing)
                            {
                                File.Delete(obj);
                                dropped++;
                                droppedNames.Add(CarModelFolder.KindName(kind) + " LOD" + lod + " " + section.Name);
                            }
                            else
                            {
                                absent++;
                            }
                            continue;
                        }
                        if (ObjMesh.Same(ObjMesh.FromLod(section, lod), mine.Mesh, 2e-5f, 2e-5f, 1e-3f))
                        {
                            same++;
                        }
                        else
                        {
                            mine.Mesh.Save(obj, section.Name, "ForzaStudio EVO: " + section.Name + " from FBX", CarModelFolder.MaterialsName);
                            changed++;
                        }
                    }
                }
                // Parts only the file has.
                foreach (Found item in found.Values)
                {
                    if (item.Kind != kind || known.ContainsKey(item.Part))
                    {
                        continue;
                    }
                    string directory = Path.Combine(folder, "lod" + item.Lod.ToString(CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(directory);
                    item.Mesh.Save(Path.Combine(directory, item.Part + ".obj"), item.Part, "ForzaStudio EVO: " + item.Part + " from FBX", CarModelFolder.MaterialsName);
                    added++;
                }
            }
            notes.Add(string.Format("FBX: {0} mesh nodes, {1} part LODs unchanged, {2} changed, {3} new, {4} dropped, {5} not in the file (left as they are).", meshes, same, changed, added, dropped, absent));
            if (droppedNames.Count > 0)
            {
                notes.Add("Dropped: " + Short(droppedNames));
            }
        }

        private static string KindAbove(FbxScene.Model node)
        {
            return (node.Parent != null) ? KindOf(node.Parent.Name) : null;
        }

        private static string ObjPath(string folder, uint lod, string part)
        {
            return Path.Combine(Path.Combine(folder, "lod" + lod.ToString(CultureInfo.InvariantCulture)), part + ".obj");
        }

        private static string Short(List<string> items)
        {
            int shown = Math.Min(8, items.Count);
            return string.Join(", ", items.GetRange(0, shown).ToArray()) + ((items.Count > shown) ? string.Format(" ... {0} more", items.Count - shown) : "");
        }

        #endregion
    }
}
