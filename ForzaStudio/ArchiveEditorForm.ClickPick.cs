namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Windows.Forms;

    // A click on the model in the viewport selects the mesh under the pointer in the model tree (outside the
    // MESH tab, where a click picks a face instead).
    public partial class ArchiveEditorForm
    {
        private System.Drawing.Point clickDown;

        private void SelectMeshAt(System.Drawing.Point p, bool add, bool keepIfSelected)
        {
            Vector3 origin, direction;
            if (this.scene == null || !this.PixelRay(p, out origin, out direction))
            {
                return;
            }
            ForzaMesh best = null;
            float nearest = float.MaxValue;
            foreach (ForzaMesh mesh in GetMeshes(this.tvModel.Nodes, true))
            {
                if (mesh.Hidden || mesh.Frozen || mesh.Vertices == null || mesh.Indices == null)
                {
                    continue;
                }
                for (int i = 0; i + 2 < mesh.Indices.Length; i += 3)
                {
                    float distance = RayTriangle(origin, direction, mesh.Vertices[mesh.Indices[i]].position, mesh.Vertices[mesh.Indices[i + 1]].position, mesh.Vertices[mesh.Indices[i + 2]].position);
                    if (distance > 0f && distance < nearest)
                    {
                        nearest = distance;
                        best = mesh;
                    }
                }
            }
            if (best == null)
            {
                // Nothing under the pointer: a plain click puts the selection away.
                if (!add && !keepIfSelected)
                {
                    this.tvModel.SelectedNode = null;
                    this.meshSelection.Clear();
                    this.HighlightSelection();
                }
                return;
            }
            if (keepIfSelected && this.meshSelection.Contains(best))
            {
                return;
            }
            TreeNode node = FindMeshNode(this.tvModel.Nodes, best);
            if (node == null)
            {
                return;
            }
            if (add)
            {
                // Ctrl: the mesh joins the selection (or leaves it when it was in); the tree shows it as current.
                if (!this.meshSelection.Remove(best))
                {
                    this.meshSelection.Add(best);
                }
                this.keepMeshSelection = true;
                this.tvModel.SelectedNode = node;
                this.keepMeshSelection = false;
            }
            else
            {
                this.tvModel.SelectedNode = node;
                if (!this.meshSelection.Contains(best) || this.meshSelection.Count != 1)
                {
                    // The node was already the selected one, so the tree raised no event.
                    this.TreeSelected();
                }
            }
            node.EnsureVisible();
            this.HighlightSelection();
        }
        private static TreeNode FindMeshNode(TreeNodeCollection nodes, ForzaMesh mesh)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag == mesh)
                {
                    return node;
                }
                TreeNode found = FindMeshNode(node.Nodes, mesh);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
