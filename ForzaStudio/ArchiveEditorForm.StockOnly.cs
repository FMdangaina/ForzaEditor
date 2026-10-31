namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;

    // List under the model tree: which version of the car's variant parts to draw. A part family is a name with its
    // stock `a` part (bumperFa) and variants named with another letter (bumperFb, bumperFc...) or `race`
    // (bumperFrace). The list is built from the letters the open model really has: All parts, Stock (the `a`
    // parts), Race, Kit B, Kit C... Parts with no `a` sibling (trunk, wheel, body, glassR...) are always shown.
    public partial class ArchiveEditorForm
    {
        private ComboBox cmbVariant;
        private const string AllParts = "All parts";

        private void AddStockOnlyButton(Control parent)
        {
            this.cmbVariant = new ComboBox();
            this.cmbVariant.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbVariant.Dock = DockStyle.Bottom;
            this.cmbVariant.Items.Add(AllParts);
            this.cmbVariant.SelectedIndex = 0;
            this.cmbVariant.DropDown += delegate { this.FillVariantList(); };
            this.cmbVariant.SelectionChangeCommitted += delegate { this.ApplyVariant(Convert.ToString(this.cmbVariant.SelectedItem)); };
            parent.Controls.Add(this.cmbVariant);
        }

        private static readonly Regex VariantName = new Regex("^(.*?[A-Za-z])(race|[b-z])$");

        // base name -> (suffix -> part node), for the names that have a stock `a` part.
        private static Dictionary<string, Dictionary<string, TreeNode>> Families(TreeNode lod)
        {
            Dictionary<string, TreeNode> byName = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
            foreach (TreeNode part in lod.Nodes)
            {
                byName[part.Text] = part;
            }
            Dictionary<string, Dictionary<string, TreeNode>> families = new Dictionary<string, Dictionary<string, TreeNode>>(StringComparer.OrdinalIgnoreCase);
            foreach (TreeNode part in lod.Nodes)
            {
                Match m = VariantName.Match(part.Text);
                TreeNode stock = null;
                // a race part is a variant even when the car has no stock sibling (the 8C's wingrace)
                if (!m.Success || (!byName.TryGetValue(m.Groups[1].Value + "a", out stock) && m.Groups[2].Value != "race"))
                {
                    continue;
                }
                Dictionary<string, TreeNode> family;
                if (!families.TryGetValue(m.Groups[1].Value, out family))
                {
                    family = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
                    if (stock != null) { family["a"] = stock; }
                    families[m.Groups[1].Value] = family;
                }
                family[m.Groups[2].Value] = part;
            }
            return families;
        }

        private void FillVariantList()
        {
            SortedDictionary<string, bool> letters = new SortedDictionary<string, bool>(StringComparer.Ordinal);
            bool race = false;
            foreach (TreeNode file in this.tvModel.Nodes)
            {
                foreach (TreeNode lod in file.Nodes)
                {
                    foreach (Dictionary<string, TreeNode> family in Families(lod).Values)
                    {
                        foreach (string suffix in family.Keys)
                        {
                            if (suffix == "race") { race = true; }
                            else if (suffix != "a") { letters[suffix.ToLowerInvariant()] = true; }
                        }
                    }
                }
            }
            string current = Convert.ToString(this.cmbVariant.SelectedItem);
            this.cmbVariant.Items.Clear();
            this.cmbVariant.Items.Add(AllParts);
            this.cmbVariant.Items.Add("Stock");
            if (race) { this.cmbVariant.Items.Add("Race"); }
            foreach (string letter in letters.Keys)
            {
                this.cmbVariant.Items.Add("Kit " + letter.ToUpperInvariant());
            }
            int index = this.cmbVariant.Items.IndexOf(current);
            this.cmbVariant.SelectedIndex = (index >= 0) ? index : 0;
        }

        // Every family shows the chosen variant (the stock one where it has none); All parts shows everything.
        private void ApplyVariant(string choice)
        {
            string wanted = (choice == "Stock") ? "a" : (choice == "Race") ? "race" : choice.StartsWith("Kit ") ? choice.Substring(4).ToLowerInvariant() : null;
            this.tvModel.BeginUpdate();
            foreach (TreeNode file in this.tvModel.Nodes)
            {
                foreach (TreeNode lod in file.Nodes)
                {
                    foreach (Dictionary<string, TreeNode> family in Families(lod).Values)
                    {
                        TreeNode show;
                        if (wanted == null || !family.TryGetValue(wanted, out show))
                        {
                            family.TryGetValue("a", out show);
                        }
                        foreach (TreeNode part in family.Values)
                        {
                            this.SetSubtreeChecked(part, wanted == null || part == show);
                        }
                    }
                }
            }
            this.tvModel.EndUpdate();
            this.ShownChanged();
        }
    }
}
