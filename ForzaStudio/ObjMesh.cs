namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // Wavefront OBJ for part round trips: export a .carbin part (exact positions, UVs and normals,
    // one group per material) and read an edited part back. UVs follow the OBJ convention (v = 1 - Forza
    // v), positions are model-space meters, as in the game files.
    public class ObjMesh
    {
        public class Corner
        {
            public Vector3 Position;
            public Vector2 Uv;
            public Vector3 Normal;
            public bool HasUv;
            public bool HasNormal;
        }

        public class Material
        {
            public string Name;
            public List<Corner[]> Triangles = new List<Corner[]>();
            // UVs already in the car's own texture region (read from the model): never refitted.
            public bool Exact;
        }

        public List<Material> Materials = new List<Material>();

        public int TriangleCount
        {
            get
            {
                int count = 0;
                foreach (Material material in this.Materials)
                {
                    count += material.Triangles.Count;
                }
                return count;
            }
        }

        #region Reading

        public static ObjMesh Load(string path)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            List<Vector3> positions = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            ObjMesh mesh = new ObjMesh();
            Dictionary<string, Material> byName = new Dictionary<string, Material>();
            Material current = null;
            string group = null;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                int comment = line.IndexOf('#');
                if (comment >= 0)
                {
                    line = line.Substring(0, comment).Trim();
                }
                if (line.Length == 0)
                {
                    continue;
                }
                string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "v":
                        positions.Add(new Vector3(float.Parse(parts[1], inv), float.Parse(parts[2], inv), float.Parse(parts[3], inv)));
                        break;
                    case "vt":
                        uvs.Add(new Vector2(float.Parse(parts[1], inv), (parts.Length > 2) ? float.Parse(parts[2], inv) : 0f));
                        break;
                    case "vn":
                        normals.Add(new Vector3(float.Parse(parts[1], inv), float.Parse(parts[2], inv), float.Parse(parts[3], inv)));
                        break;
                    case "g":
                    case "o":
                        group = (parts.Length > 1) ? parts[1] : null;
                        break;
                    case "usemtl":
                        current = Get(mesh, byName, (parts.Length > 1) ? line.Substring(line.IndexOf(' ') + 1).Trim() : "default");
                        break;
                    case "f":
                        if (current == null)
                        {
                            current = Get(mesh, byName, group ?? "default");
                        }
                        List<Corner> polygon = new List<Corner>();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            polygon.Add(ParseCorner(parts[i], positions, uvs, normals));
                        }
                        // Fan triangulation of polygons.
                        for (int i = 1; i + 1 < polygon.Count; i++)
                        {
                            current.Triangles.Add(new Corner[] { polygon[0], polygon[i], polygon[i + 1] });
                        }
                        break;
                }
            }
            mesh.Materials.RemoveAll(delegate(Material m) { return m.Triangles.Count == 0; });
            return mesh;
        }

        private static Material Get(ObjMesh mesh, Dictionary<string, Material> byName, string name)
        {
            Material material;
            if (!byName.TryGetValue(name, out material))
            {
                material = new Material();
                material.Name = name;
                byName[name] = material;
                mesh.Materials.Add(material);
            }
            return material;
        }

        private static int Resolve(string token, int count)
        {
            int index = int.Parse(token, CultureInfo.InvariantCulture);
            return (index < 0) ? count + index : index - 1;
        }

        private static Corner ParseCorner(string token, List<Vector3> positions, List<Vector2> uvs, List<Vector3> normals)
        {
            string[] refs = token.Split('/');
            Corner corner = new Corner();
            corner.Position = positions[Resolve(refs[0], positions.Count)];
            if (refs.Length > 1 && refs[1].Length > 0)
            {
                corner.Uv = uvs[Resolve(refs[1], uvs.Count)];
                corner.HasUv = true;
            }
            if (refs.Length > 2 && refs[2].Length > 0)
            {
                corner.Normal = normals[Resolve(refs[2], normals.Count)];
                corner.HasNormal = corner.Normal.LengthSquared() > 1e-12f;
                if (corner.HasNormal)
                {
                    corner.Normal.Normalize();
                }
            }
            return corner;
        }

        #endregion

        #region Writing

        // Writes one part of a model: the lod0 buffer's subsections when it has them, else LOD 1.
        // 'onlyMaterial' (null = all) limits it to that material of the part.
        public static int Export(CarbinModel model, string sectionName, string path, string comment, string onlyMaterial)
        {
            CarbinModel.Section section = model.Find(sectionName);
            if (section == null)
            {
                throw new ArgumentException("No part " + sectionName + ".");
            }
            bool lod0 = section.Lod0.Count > 0;
            int lod = lod0 ? 0 : int.MaxValue;
            if (!lod0)
            {
                foreach (CarbinModel.SubSection sub in section.SubSections)
                {
                    lod = Math.Min(lod, (int) sub.Lod);
                }
            }
            CarbinModel.VertexBuffer buffer = lod0 ? section.Lod0 : section.Lod;
            double[] positions = CarbinModel.Positions(section, buffer);
            int triangles = 0;
            using (StreamWriter obj = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                obj.WriteLine("# " + comment);
                obj.WriteLine("# part " + sectionName + ", LOD " + lod + "; units: meters, model space; one group per material");
                obj.WriteLine("mtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl");
                int baseIndex = 1;
                List<string> materials = new List<string>();
                foreach (CarbinModel.SubSection sub in section.SubSections)
                {
                    if (sub.Lod != lod || (onlyMaterial != null && sub.Name != onlyMaterial))
                    {
                        continue;
                    }
                    if (!materials.Contains(sub.Name))
                    {
                        materials.Add(sub.Name);
                    }
                    triangles += WriteGroup(obj, sectionName, sub, buffer, positions, ref baseIndex);
                }
                WriteMtl(Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".mtl"), materials);
            }
            return triangles;
        }

        // Writes one part at one LOD (one group per material) for the model folder export. 'mtllib'
        // is the material file next to it. Returns the triangle count.
        public static int ExportLod(CarbinModel.Section section, uint lod, string path, string comment, string mtllib)
        {
            CarbinModel.VertexBuffer buffer = (lod == 0) ? section.Lod0 : section.Lod;
            double[] positions = CarbinModel.Positions(section, buffer);
            int triangles = 0;
            using (StreamWriter obj = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                obj.WriteLine("# " + comment);
                obj.WriteLine("# part " + section.Name + ", LOD " + lod + "; units: meters, model space; one group per material (usemtl = car material)");
                obj.WriteLine("mtllib " + mtllib);
                int baseIndex = 1;
                foreach (CarbinModel.SubSection sub in section.SubSections)
                {
                    if (sub.Lod == lod)
                    {
                        triangles += WriteGroup(obj, section.Name, sub, buffer, positions, ref baseIndex);
                    }
                }
            }
            return triangles;
        }

        // One material group: its own vertex list (materials stay separable in the editor), UVs in
        // the OBJ convention and the stored normals.
        private static int WriteGroup(StreamWriter obj, string sectionName, CarbinModel.SubSection sub, CarbinModel.VertexBuffer buffer, double[] positions, ref int baseIndex)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            Dictionary<int, int> local = new Dictionary<int, int>();
            List<int> order = new List<int>();
            List<int[]> faces = CarbinModel.StripTriangles(sub);
            foreach (int[] face in faces)
            {
                foreach (int v in face)
                {
                    if (!local.ContainsKey(v))
                    {
                        local[v] = order.Count;
                        order.Add(v);
                    }
                }
            }
            obj.WriteLine("g " + sectionName + "_" + sub.Name);
            obj.WriteLine("usemtl " + sub.Name);
            foreach (int v in order)
            {
                obj.WriteLine(string.Format(inv, "v {0:R} {1:R} {2:R}", (float) positions[v * 3], (float) positions[(v * 3) + 1], (float) positions[(v * 3) + 2]));
            }
            foreach (int v in order)
            {
                Vector2 uv = CarbinModel.VertexUv(buffer, v, sub, 0);
                obj.WriteLine(string.Format(inv, "vt {0:R} {1:R}", uv.X, 1f - uv.Y));
            }
            foreach (int v in order)
            {
                Vector3 n = CarbinModel.VertexNormal(buffer, v);
                obj.WriteLine(string.Format(inv, "vn {0:R} {1:R} {2:R}", n.X, n.Y, n.Z));
            }
            foreach (int[] face in faces)
            {
                int a = local[face[0]] + baseIndex;
                int b = local[face[1]] + baseIndex;
                int c = local[face[2]] + baseIndex;
                obj.WriteLine("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}", a, b, c);
            }
            baseIndex += order.Count;
            return faces.Count;
        }

        // A part at one LOD in memory: the same corners ExportLod writes and Load reads back
        // (one Material per material name, normals normalized).
        public static ObjMesh FromLod(CarbinModel.Section section, uint lod)
        {
            CarbinModel.VertexBuffer buffer = (lod == 0) ? section.Lod0 : section.Lod;
            double[] positions = CarbinModel.Positions(section, buffer);
            ObjMesh mesh = new ObjMesh();
            Dictionary<string, Material> byName = new Dictionary<string, Material>();
            foreach (CarbinModel.SubSection sub in section.SubSections)
            {
                if (sub.Lod != lod)
                {
                    continue;
                }
                Material material = Get(mesh, byName, sub.Name);
                material.Exact = true;
                foreach (int[] face in CarbinModel.StripTriangles(sub))
                {
                    Corner[] triangle = new Corner[3];
                    for (int i = 0; i < 3; i++)
                    {
                        int v = face[i];
                        Corner corner = new Corner();
                        corner.Position = new Vector3((float) positions[v * 3], (float) positions[(v * 3) + 1], (float) positions[(v * 3) + 2]);
                        Vector2 uv = CarbinModel.VertexUv(buffer, v, sub, 0);
                        corner.Uv = new Vector2(uv.X, 1f - uv.Y);
                        corner.HasUv = true;
                        corner.Normal = CarbinModel.VertexNormal(buffer, v);
                        corner.HasNormal = corner.Normal.LengthSquared() > 1e-12f;
                        if (corner.HasNormal)
                        {
                            corner.Normal.Normalize();
                        }
                        triangle[i] = corner;
                    }
                    material.Triangles.Add(triangle);
                }
            }
            mesh.Materials.RemoveAll(delegate(Material m) { return m.Triangles.Count == 0; });
            return mesh;
        }

        // True when both have the same materials (by name) with the same triangles, corner by corner,
        // within the tolerances (a round trip through another program changes the last digits).
        public static bool Same(ObjMesh a, ObjMesh b, float position, float uv, float normal)
        {
            if (a.Materials.Count != b.Materials.Count)
            {
                return false;
            }
            foreach (Material ma in a.Materials)
            {
                Material mb = null;
                foreach (Material candidate in b.Materials)
                {
                    if (candidate.Name == ma.Name)
                    {
                        mb = candidate;
                        break;
                    }
                }
                if (mb == null || mb.Triangles.Count != ma.Triangles.Count)
                {
                    return false;
                }
                for (int t = 0; t < ma.Triangles.Count; t++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        Corner ca = ma.Triangles[t][c];
                        Corner cb = mb.Triangles[t][c];
                        if (ca.HasUv != cb.HasUv || ca.HasNormal != cb.HasNormal
                            || Math.Abs(ca.Position.X - cb.Position.X) > position || Math.Abs(ca.Position.Y - cb.Position.Y) > position || Math.Abs(ca.Position.Z - cb.Position.Z) > position
                            || (ca.HasUv && (Math.Abs(ca.Uv.X - cb.Uv.X) > uv || Math.Abs(ca.Uv.Y - cb.Uv.Y) > uv))
                            || (ca.HasNormal && Vector3.Dot(ca.Normal, cb.Normal) < 1f - normal))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private struct CornerKey
        {
            public Corner Corner;

            public override bool Equals(object other)
            {
                Corner a = this.Corner;
                Corner b = ((CornerKey) other).Corner;
                return a.Position == b.Position && a.Uv == b.Uv && a.Normal == b.Normal;
            }

            public override int GetHashCode()
            {
                return this.Corner.Position.GetHashCode() ^ (this.Corner.Uv.GetHashCode() * 31) ^ (this.Corner.Normal.GetHashCode() * 17);
            }
        }

        // Writes the mesh as an OBJ in the model folder layout (one group per material, shared
        // vertices by exact value). A material without UVs or normals writes none.
        public void Save(string path, string partName, string comment, string mtllib)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            using (StreamWriter obj = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                obj.WriteLine("# " + comment);
                obj.WriteLine("mtllib " + mtllib);
                int baseV = 1;
                int baseVt = 1;
                int baseVn = 1;
                foreach (Material material in this.Materials)
                {
                    bool uv = true;
                    bool normal = true;
                    Dictionary<CornerKey, int> index = new Dictionary<CornerKey, int>();
                    List<Corner> order = new List<Corner>();
                    List<int> faces = new List<int>();
                    foreach (Corner[] triangle in material.Triangles)
                    {
                        foreach (Corner corner in triangle)
                        {
                            CornerKey key = new CornerKey();
                            key.Corner = corner;
                            int at;
                            if (!index.TryGetValue(key, out at))
                            {
                                at = order.Count;
                                index[key] = at;
                                order.Add(corner);
                            }
                            faces.Add(at);
                            uv &= corner.HasUv;
                            normal &= corner.HasNormal;
                        }
                    }
                    obj.WriteLine("g " + partName + "_" + material.Name);
                    obj.WriteLine("usemtl " + material.Name);
                    foreach (Corner corner in order)
                    {
                        obj.WriteLine(string.Format(inv, "v {0:R} {1:R} {2:R}", corner.Position.X, corner.Position.Y, corner.Position.Z));
                    }
                    if (uv)
                    {
                        foreach (Corner corner in order)
                        {
                            obj.WriteLine(string.Format(inv, "vt {0:R} {1:R}", corner.Uv.X, corner.Uv.Y));
                        }
                    }
                    if (normal)
                    {
                        foreach (Corner corner in order)
                        {
                            obj.WriteLine(string.Format(inv, "vn {0:R} {1:R} {2:R}", corner.Normal.X, corner.Normal.Y, corner.Normal.Z));
                        }
                    }
                    for (int i = 0; i < faces.Count; i += 3)
                    {
                        string a = Ref(faces[i], baseV, uv ? baseVt : 0, normal ? baseVn : 0);
                        string b = Ref(faces[i + 1], baseV, uv ? baseVt : 0, normal ? baseVn : 0);
                        string c = Ref(faces[i + 2], baseV, uv ? baseVt : 0, normal ? baseVn : 0);
                        obj.WriteLine("f " + a + " " + b + " " + c);
                    }
                    baseV += order.Count;
                    if (uv)
                    {
                        baseVt += order.Count;
                    }
                    if (normal)
                    {
                        baseVn += order.Count;
                    }
                }
            }
        }

        // One face corner; a base of 0 means that attribute is not written.
        private static string Ref(int i, int baseV, int baseVt, int baseVn)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            string text = (i + baseV).ToString(inv);
            if (baseVt > 0)
            {
                text += "/" + (i + baseVt).ToString(inv);
            }
            if (baseVn > 0)
            {
                text += ((baseVt > 0) ? "/" : "//") + (i + baseVn).ToString(inv);
            }
            return text;
        }

        // Materials with a stable random color each, so parts read apart in the editor.
        public static void WriteMtl(string path, IEnumerable<string> materials)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            using (StreamWriter mtl = new StreamWriter(path))
            {
                foreach (string material in materials)
                {
                    Vector3 color = Utilities.GetRandomColor(material.GetHashCode()).ToVector3();
                    mtl.WriteLine("newmtl " + material);
                    mtl.WriteLine(string.Format(inv, "Kd {0} {1} {2}", color.X, color.Y, color.Z));
                }
            }
        }

        #endregion
    }
}
