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
        public static int Export(CarbinModel model, string sectionName, string path, string comment)
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
            CultureInfo inv = CultureInfo.InvariantCulture;
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
                    if (sub.Lod != lod)
                    {
                        continue;
                    }
                    // Own vertex list per material, so materials stay separable in the editor.
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
                    if (!materials.Contains(sub.Name))
                    {
                        materials.Add(sub.Name);
                    }
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
                    triangles += faces.Count;
                }
                using (StreamWriter mtl = new StreamWriter(Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".mtl")))
                {
                    foreach (string material in materials)
                    {
                        Vector3 color = Utilities.GetRandomColor(material.GetHashCode()).ToVector3();
                        mtl.WriteLine("newmtl " + material);
                        mtl.WriteLine(string.Format(inv, "Kd {0} {1} {2}", color.X, color.Y, color.Z));
                    }
                }
            }
            return triangles;
        }

        #endregion
    }
}
