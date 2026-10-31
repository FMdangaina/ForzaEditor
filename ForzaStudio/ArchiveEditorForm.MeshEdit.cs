namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // The MESH tab: select faces in the viewer and delete them or detach them into a part of their own.
    // Click picks a face, Shift+click adds, Ctrl+click toggles; a drag draws a box that selects the faces
    // whose centre falls inside it (same modifiers); middle drag turns the view, right drag moves it.
    // Faces are the triangles of the meshes (material + LOD) checked in the tree; an edit touches only
    // the LOD of the mesh the face is in, in this model file, and stays pending until Save.
    public partial class ArchiveEditorForm
    {
        private TabPage tabSideMesh;
        private Label lblFaceSelection;
        private CheckBox chkFrontFaces;

        // One shown mesh: where it is in the carbin, its triangles and the positions they index.
        private sealed class PickMesh
        {
            public int Section, Sub;
            public string Label;
            public List<int[]> Triangles;
            public double[] Positions;
            public int[] Group;               // vertex -> id of the vertices at the same place (UV seams split vertices)
        }

        private CarbinModel faceModel;
        private List<PickMesh> pickMeshes = new List<PickMesh>();
        private bool pickDirty = true;
        private readonly Set<long> faceSelection = new Set<long>();
        private List<VertexPositionColor> faceLines;     // the selected faces' edges, rebuilt when the selection changes
        private System.Drawing.Point dragStart, dragEnd;
        private bool faceDragging;

        private static long FaceKey(int section, int sub, int face)
        {
            return ((long) section << 44) | ((long) sub << 28) | (long) face;
        }

        private static void FaceKeyParts(long key, out int section, out int sub, out int face)
        {
            section = (int) (key >> 44);
            sub = (int) ((key >> 28) & 0xFFFF);
            face = (int) (key & 0xFFFFFFF);
        }

        private void AddMeshEditControls(FlowLayoutPanel side)
        {
            this.lblFaceSelection = SideNote("Select faces in the view.");
            this.lblFaceSelection.ForeColor = this.ForeColor;
            this.chkFrontFaces = new CheckBox();
            this.chkFrontFaces.Text = "Box: only faces turned to the camera";
            this.chkFrontFaces.Checked = true;
            this.chkFrontFaces.Size = new Size(SideWidth, 22);

            side.Controls.Add(SideHeading("Faces", true));
            side.Controls.Add(this.lblFaceSelection);
            side.Controls.Add(this.chkFrontFaces);
            side.Controls.Add(SideNote("The tools (select all / none / linked, delete, detach, UV mapper) are in the tool bar above the view, with their keys in the hints. Click: pick a face. Shift+click: add. Ctrl+click: toggle. Drag: box (same keys). Middle drag: move the view, Alt + middle drag: turn it, wheel: zoom. Delete and Detach touch only the LOD of the mesh the faces are in; pending until File > Save."));
            this.UpdateFaceSelectionLabel();
        }

        private static Button MakeButton(string text, EventHandler click)
        {
            Button button = new Button();
            button.Text = text;
            button.Click += click;
            return button;
        }

        private bool MeshEditActive
        {
            get { return this.scene != null && this.tabSide != null && this.tabSide.SelectedTab == this.tabSideMesh && this.pnlModel.Visible; }
        }

        // The model's vertices change with every edit: the picking data and the selection start over.
        private void MeshEditReset()
        {
            this.CloseUvMapperForModelChange();
            this.meshSelection.Clear();
            this.faceModel = null;
            this.pickMeshes.Clear();
            this.pickDirty = true;
            this.faceSelection.Clear();
            this.faceLines = null;
            this.faceDragging = false;
            if (this.lblFaceSelection != null)
            {
                this.UpdateFaceSelectionLabel();
            }
        }

        // The checked meshes change: the faces to pick from do, the selection (indices) stays.
        private void MeshEditShownChanged()
        {
            this.pickDirty = true;
            this.faceLines = null;
        }

        private bool EnsurePickData()
        {
            if (!this.pickDirty && this.faceModel != null)
            {
                return true;
            }
            this.pickMeshes.Clear();
            this.faceLines = null;
            try
            {
                if (this.faceModel == null)
                {
                    if (this.currentEntry == null || !this.currentEntry.Name.EndsWith(".carbin", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    this.faceModel = new CarbinModel(this.zip.Read(this.currentEntry));
                }
                foreach (ForzaMesh mesh in GetMeshes(this.tvModel.Nodes, true))
                {
                    if (mesh.Hidden || mesh.Frozen || mesh.SectionIndex < 0 || mesh.SectionIndex >= this.faceModel.Sections.Count)
                    {
                        continue;
                    }
                    CarbinModel.Section section = this.faceModel.Sections[mesh.SectionIndex];
                    if (mesh.SubIndex < 0 || mesh.SubIndex >= section.SubSections.Count)
                    {
                        continue;
                    }
                    CarbinModel.SubSection sub = section.SubSections[mesh.SubIndex];
                    PickMesh pick = new PickMesh();
                    pick.Section = mesh.SectionIndex;
                    pick.Sub = mesh.SubIndex;
                    pick.Label = section.Name + " / " + sub.Name;
                    pick.Triangles = CarbinModel.StripTriangles(sub);
                    pick.Positions = CarbinModel.Positions(section, (sub.Lod == 0) ? section.Lod0 : section.Lod);
                    this.pickMeshes.Add(pick);
                }
                this.pickDirty = false;
                return true;
            }
            catch (Exception exception)
            {
                this.faceModel = null;
                this.SetStatus("Mesh edit not available for this file: " + exception.Message);
                return false;
            }
        }

        // ---- picking ----

        private bool PixelRay(System.Drawing.Point p, out Vector3 origin, out Vector3 direction)
        {
            Camera camera = this.scene.Camera;
            Matrix inverse = Matrix.Invert(camera.View * camera.Projection);
            float w = Math.Max(1, this.pnlViewport.ClientSize.Width), h = Math.Max(1, this.pnlViewport.ClientSize.Height);
            float x = (2f * p.X / w) - 1f, y = 1f - (2f * p.Y / h);
            Vector3 near = Unproject(inverse, x, y, 0f), far = Unproject(inverse, x, y, 1f);
            origin = near;
            direction = far - near;
            if (direction.LengthSquared() < 1e-12f)
            {
                return false;
            }
            direction.Normalize();
            return true;
        }

        private static Vector3 Unproject(Matrix inverse, float x, float y, float z)
        {
            Vector4 v = Vector4.Transform(new Vector4(x, y, z, 1f), inverse);
            return new Vector3(v.X / v.W, v.Y / v.W, v.Z / v.W);
        }

        private static Vector3 PointOf(double[] p, int i)
        {
            return new Vector3((float) p[i * 3], (float) p[(i * 3) + 1], (float) p[(i * 3) + 2]);
        }

        // Moller-Trumbore, both sides. Distance along the ray, or -1.
        private static float RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (Math.Abs(det) < 1e-12f)
            {
                return -1f;
            }
            float inv = 1f / det;
            Vector3 t = o - a;
            float u = Vector3.Dot(t, p) * inv;
            if (u < 0f || u > 1f)
            {
                return -1f;
            }
            Vector3 q = Vector3.Cross(t, e1);
            float v = Vector3.Dot(d, q) * inv;
            if (v < 0f || u + v > 1f)
            {
                return -1f;
            }
            float distance = Vector3.Dot(e2, q) * inv;
            return distance > 0f ? distance : -1f;
        }

        private bool PickFace(System.Drawing.Point p, out long key)
        {
            key = -1;
            Vector3 origin, direction;
            if (!this.PixelRay(p, out origin, out direction))
            {
                return false;
            }
            float best = float.MaxValue;
            foreach (PickMesh mesh in this.pickMeshes)
            {
                for (int f = 0; f < mesh.Triangles.Count; f++)
                {
                    int[] t = mesh.Triangles[f];
                    float distance = RayTriangle(origin, direction, PointOf(mesh.Positions, t[0]), PointOf(mesh.Positions, t[1]), PointOf(mesh.Positions, t[2]));
                    if (distance > 0f && distance < best)
                    {
                        best = distance;
                        key = FaceKey(mesh.Section, mesh.Sub, f);
                    }
                }
            }
            return key >= 0;
        }

        private List<long> FacesInBox(System.Drawing.Rectangle box, bool frontOnly)
        {
            List<long> found = new List<long>();
            Camera camera = this.scene.Camera;
            Matrix viewProjection = camera.View * camera.Projection;
            float w = Math.Max(1, this.pnlViewport.ClientSize.Width), h = Math.Max(1, this.pnlViewport.ClientSize.Height);
            Vector3 eye = camera.EyePosition, forward = camera.ForwardDirection;
            foreach (PickMesh mesh in this.pickMeshes)
            {
                for (int f = 0; f < mesh.Triangles.Count; f++)
                {
                    int[] t = mesh.Triangles[f];
                    Vector3 a = PointOf(mesh.Positions, t[0]), b = PointOf(mesh.Positions, t[1]), c = PointOf(mesh.Positions, t[2]);
                    Vector3 centre = (a + b + c) / 3f;
                    Vector4 clip = Vector4.Transform(new Vector4(centre, 1f), viewProjection);
                    if (clip.W <= 1e-6f)
                    {
                        continue;
                    }
                    float sx = ((clip.X / clip.W) + 1f) * 0.5f * w, sy = (1f - (clip.Y / clip.W)) * 0.5f * h;
                    if (!box.Contains((int) sx, (int) sy))
                    {
                        continue;
                    }
                    if (frontOnly)
                    {
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        Vector3 toEye = camera.Orthographic ? -forward : eye - centre;
                        if (Vector3.Dot(normal, toEye) <= 0f)
                        {
                            continue;
                        }
                    }
                    found.Add(FaceKey(mesh.Section, mesh.Sub, f));
                }
            }
            return found;
        }

        // ---- mouse (called from the viewport's handlers while the MESH tab is open) ----

        private void MeshEditMouseDown(MouseEventArgs e)
        {
            this.dragStart = e.Location;
            this.dragEnd = e.Location;
            this.faceDragging = true;
        }

        private void MeshEditMouseMove(MouseEventArgs e)
        {
            if (!this.faceDragging)
            {
                return;
            }
            this.dragEnd = e.Location;
            if (this.DragIsBox)
            {
                this.UpdateViewOverlay();
            }
        }

        private bool DragIsBox
        {
            get { return Math.Abs(this.dragEnd.X - this.dragStart.X) > 4 || Math.Abs(this.dragEnd.Y - this.dragStart.Y) > 4; }
        }

        private void MeshEditMouseUp(MouseEventArgs e)
        {
            if (!this.faceDragging)
            {
                return;
            }
            this.faceDragging = false;
            this.dragEnd = e.Location;
            if (!this.EnsurePickData())
            {
                this.UpdateViewOverlay();
                return;
            }
            bool add = (Control.ModifierKeys & Keys.Shift) != 0, toggle = (Control.ModifierKeys & Keys.Control) != 0;
            List<long> hit = new List<long>();
            if (this.DragIsBox)
            {
                System.Drawing.Rectangle box = System.Drawing.Rectangle.FromLTRB(Math.Min(this.dragStart.X, this.dragEnd.X), Math.Min(this.dragStart.Y, this.dragEnd.Y),
                    Math.Max(this.dragStart.X, this.dragEnd.X), Math.Max(this.dragStart.Y, this.dragEnd.Y));
                hit = this.FacesInBox(box, this.chkFrontFaces.Checked);
            }
            else
            {
                long key;
                if (this.PickFace(e.Location, out key))
                {
                    hit.Add(key);
                }
            }
            if (!add && !toggle)
            {
                this.faceSelection.Clear();
            }
            foreach (long key in hit)
            {
                if (toggle && this.faceSelection.Contains(key))
                {
                    this.faceSelection.Remove(key);
                }
                else
                {
                    this.faceSelection.Add(key);
                }
            }
            this.FaceSelectionChanged();
        }

        // ---- selection ----

        private void FaceSelectionChanged()
        {
            this.faceLines = null;
            this.UpdateFaceSelectionLabel();
            this.UpdateViewOverlay();
        }

        private void UpdateFaceSelectionLabel()
        {
            bool any = this.faceSelection.Count > 0;
            this.lblFaceSelection.Text = any ? string.Format("{0:N0} faces selected", this.faceSelection.Count) : "No face selected. Click or drag in the view.";
            if (this.btnToolDelete != null)
            {
                this.btnToolDelete.Enabled = any;
                this.btnToolDetach.Enabled = any;
                this.btnToolLinked.Enabled = any;
                this.btnToolNone.Enabled = any;
            }
        }

        private void ClearFaceSelection()
        {
            this.faceSelection.Clear();
            this.FaceSelectionChanged();
        }

        private void SelectAllFaces()
        {
            if (!this.EnsurePickData())
            {
                return;
            }
            this.faceSelection.Clear();
            foreach (PickMesh mesh in this.pickMeshes)
            {
                for (int f = 0; f < mesh.Triangles.Count; f++)
                {
                    this.faceSelection.Add(FaceKey(mesh.Section, mesh.Sub, f));
                }
            }
            this.FaceSelectionChanged();
        }

        private int[] GroupsOf(PickMesh mesh)
        {
            if (mesh.Group == null)
            {
                int count = mesh.Positions.Length / 3;
                mesh.Group = new int[count];
                Dictionary<string, int> ids = new Dictionary<string, int>();
                for (int v = 0; v < count; v++)
                {
                    string key = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", Math.Round(mesh.Positions[v * 3], 4), Math.Round(mesh.Positions[(v * 3) + 1], 4), Math.Round(mesh.Positions[(v * 3) + 2], 4));
                    int id;
                    if (!ids.TryGetValue(key, out id))
                    {
                        id = ids.Count;
                        ids[key] = id;
                    }
                    mesh.Group[v] = id;
                }
            }
            return mesh.Group;
        }

        // Grows the selection over every face that shares a point with it, again and again, inside the same mesh.
        private void SelectLinkedFaces()
        {
            if (!this.EnsurePickData() || this.faceSelection.Count == 0)
            {
                return;
            }
            foreach (PickMesh mesh in this.pickMeshes)
            {
                int[] group = this.GroupsOf(mesh);
                Dictionary<int, List<int>> facesAt = new Dictionary<int, List<int>>();
                for (int f = 0; f < mesh.Triangles.Count; f++)
                {
                    foreach (int v in mesh.Triangles[f])
                    {
                        List<int> list;
                        if (!facesAt.TryGetValue(group[v], out list))
                        {
                            list = new List<int>();
                            facesAt[group[v]] = list;
                        }
                        list.Add(f);
                    }
                }
                Queue<int> queue = new Queue<int>();
                for (int f = 0; f < mesh.Triangles.Count; f++)
                {
                    if (this.faceSelection.Contains(FaceKey(mesh.Section, mesh.Sub, f)))
                    {
                        queue.Enqueue(f);
                    }
                }
                while (queue.Count > 0)
                {
                    int f = queue.Dequeue();
                    foreach (int v in mesh.Triangles[f])
                    {
                        foreach (int other in facesAt[group[v]])
                        {
                            if (this.faceSelection.Add(FaceKey(mesh.Section, mesh.Sub, other)))
                            {
                                queue.Enqueue(other);
                            }
                        }
                    }
                }
            }
            this.FaceSelectionChanged();
        }

        // Selected faces' edges (orange), and the box being dragged (white) as lines 0.5 m in front of the camera.
        private List<VertexPositionColor> MeshEditOverlayLines()
        {
            List<VertexPositionColor> lines = new List<VertexPositionColor>();
            Microsoft.Xna.Framework.Graphics.Color orange = new Microsoft.Xna.Framework.Graphics.Color(255, 150, 30, 255);
            if (this.faceLines == null)
            {
                this.faceLines = new List<VertexPositionColor>();
                if (this.faceSelection.Count > 0 && this.EnsurePickData())
                {
                    Dictionary<int, PickMesh> byKey = new Dictionary<int, PickMesh>();
                    foreach (PickMesh mesh in this.pickMeshes)
                    {
                        byKey[(mesh.Section << 16) | mesh.Sub] = mesh;
                    }
                    foreach (long key in this.faceSelection)
                    {
                        int s, k, f;
                        FaceKeyParts(key, out s, out k, out f);
                        PickMesh mesh;
                        if (!byKey.TryGetValue((s << 16) | k, out mesh) || f >= mesh.Triangles.Count)
                        {
                            continue;
                        }
                        int[] t = mesh.Triangles[f];
                        Vector3 a = PointOf(mesh.Positions, t[0]), b = PointOf(mesh.Positions, t[1]), c = PointOf(mesh.Positions, t[2]);
                        this.faceLines.Add(new VertexPositionColor(a, orange));
                        this.faceLines.Add(new VertexPositionColor(b, orange));
                        this.faceLines.Add(new VertexPositionColor(b, orange));
                        this.faceLines.Add(new VertexPositionColor(c, orange));
                        this.faceLines.Add(new VertexPositionColor(c, orange));
                        this.faceLines.Add(new VertexPositionColor(a, orange));
                    }
                }
            }
            lines.AddRange(this.faceLines);
            if (this.faceDragging && this.DragIsBox)
            {
                Microsoft.Xna.Framework.Graphics.Color white = new Microsoft.Xna.Framework.Graphics.Color(255, 255, 255, 255);
                System.Drawing.Point[] corners =
                {
                    this.dragStart, new System.Drawing.Point(this.dragEnd.X, this.dragStart.Y), this.dragEnd, new System.Drawing.Point(this.dragStart.X, this.dragEnd.Y)
                };
                Vector3[] world = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    Vector3 origin, direction;
                    if (!this.PixelRay(corners[i], out origin, out direction))
                    {
                        return lines;
                    }
                    world[i] = origin + (direction * (this.scene.Camera.Orthographic ? 1f : 0.5f));
                }
                for (int i = 0; i < 4; i++)
                {
                    lines.Add(new VertexPositionColor(world[i], white));
                    lines.Add(new VertexPositionColor(world[(i + 1) % 4], white));
                }
            }
            return lines;
        }

        // ---- edits ----

        // Selected faces per mesh, highest section / mesh first: removing a mesh or a part shifts the
        // numbers of the later ones only.
        private List<KeyValuePair<long, List<int>>> SelectedFacesPerMesh()
        {
            Dictionary<long, List<int>> perMesh = new Dictionary<long, List<int>>();
            foreach (long key in this.faceSelection)
            {
                int s, k, f;
                FaceKeyParts(key, out s, out k, out f);
                long mesh = FaceKey(s, k, 0);
                List<int> list;
                if (!perMesh.TryGetValue(mesh, out list))
                {
                    list = new List<int>();
                    perMesh[mesh] = list;
                }
                list.Add(f);
            }
            List<KeyValuePair<long, List<int>>> ordered = new List<KeyValuePair<long, List<int>>>(perMesh);
            ordered.Sort(delegate(KeyValuePair<long, List<int>> x, KeyValuePair<long, List<int>> y) { return y.Key.CompareTo(x.Key); });
            return ordered;
        }

        private void DeleteSelectedFaces()
        {
            if (this.faceSelection.Count == 0 || !this.EnsurePickData())
            {
                return;
            }
            string question = string.Format("Delete {0:N0} selected faces?\n\nThe vertices only they use are removed. Only the LOD of each mesh they are in changes. Pending until you save (Archive > Revert Selected File undoes it).", this.faceSelection.Count);
            if (MessageBox.Show(this, question, "Delete Faces", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            this.ApplyFaceEdit(delegate(CarbinModel model, int section, int sub, List<int> faces) { return model.DeleteFaces(section, sub, faces); });
        }

        // The faces become a mesh of their own in the same part (same material, same LOD).
        private void DetachSelectedFaces()
        {
            if (this.faceSelection.Count == 0 || !this.EnsurePickData())
            {
                return;
            }
            this.ApplyFaceEdit(delegate(CarbinModel model, int section, int sub, List<int> faces) { return model.DetachFaces(section, sub, faces); });
        }

        private delegate string FaceEdit(CarbinModel model, int section, int sub, List<int> faces);

        private void ApplyFaceEdit(FaceEdit edit)
        {
            List<string> report = new List<string>();
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                CarbinModel model = new CarbinModel(this.zip.Read(this.currentEntry));
                foreach (KeyValuePair<long, List<int>> pair in this.SelectedFacesPerMesh())
                {
                    int s, k, f;
                    FaceKeyParts(pair.Key, out s, out k, out f);
                    report.Add(edit(model, s, k, pair.Value));
                }
                this.zip.Replace(this.currentEntry, model.ToBytes());
                this.RefreshEntryNode(this.currentEntry);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Mesh Edit", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
            this.ReloadModelKeepingView();
            this.SetStatus(string.Join("; ", report.ToArray()) + "   (pending: Save to write the archive)");
        }
    }
}
