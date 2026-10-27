namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;

    // Archive > Delete: a file, or a folder with everything below it, goes out of the open zip or folder. Like the other
    // edits it is pending: the entry shows struck through until Save, Undo Delete (or Revert) takes it back. A zip is
    // written without the files; in a folder the files go to the Recycle Bin (in a DLC package's work folder, which
    // the next Open DLC Package extracts again, they are deleted for good).
    public partial class ArchiveEditorForm
    {
        // The entries a delete or an undo works on: the selected file, or every entry below the selected folder.
        // null when nothing is selected or the top node is (the archive itself is not deleted from inside).
        private List<ForzaZip.Entry> EntriesOfSelection(out string what)
        {
            what = null;
            TreeNode node = (this.zip == null) ? null : this.tvArchive.SelectedNode;
            if (node == null)
            {
                return null;
            }
            ForzaZip.Entry single = node.Tag as ForzaZip.Entry;
            if (single != null)
            {
                what = single.Name;
                return new List<ForzaZip.Entry>(new ForzaZip.Entry[] { single });
            }
            if (node.Parent == null)
            {
                return null;
            }
            string dir = this.SelectedArchiveFolder();
            if (dir.Length == 0)
            {
                return null;
            }
            what = dir;
            return this.zip.Entries.FindAll(delegate(ForzaZip.Entry e) { return e.Name.StartsWith(dir, StringComparison.OrdinalIgnoreCase); });
        }

        private bool CanDeleteSelection
        {
            get
            {
                string what;
                List<ForzaZip.Entry> entries = this.EntriesOfSelection(out what);
                return entries != null && entries.Exists(delegate(ForzaZip.Entry e) { return !e.IsDirectory && !e.IsDeleted; });
            }
        }

        private bool CanUndoDeleteSelection
        {
            get
            {
                string what;
                List<ForzaZip.Entry> entries = this.EntriesOfSelection(out what);
                return entries != null && entries.Exists(delegate(ForzaZip.Entry e) { return e.IsDeleted; });
            }
        }

        // What a DLC package cannot do without: the offer file and the mount list.
        private bool IsPackageEssential(ForzaZip.Entry entry)
        {
            bool packageRoot = this.package != null && this.zip.IsFolder && string.Equals(this.zip.FilePath.TrimEnd('\\'), this.packageFolder, StringComparison.OrdinalIgnoreCase);
            return packageRoot && (entry.Name.EndsWith(".puboffer", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith("/zipmount.xml", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCarCore(string name)
        {
            string file = name.Substring(name.LastIndexOf('/') + 1);
            return file.EndsWith(".carbin", StringComparison.OrdinalIgnoreCase) || file.Equals("carattribs.xml", StringComparison.OrdinalIgnoreCase)
                || file.Equals("versiondata.xml", StringComparison.OrdinalIgnoreCase) || file.Equals("gamedb.slt", StringComparison.OrdinalIgnoreCase);
        }

        // Other files of the zip that name a file about to go (a dash names its textures, a materials file its images):
        // "stem  <-  file". The names are looked for as text, 8-bit and 16-bit; only small files of the kinds that hold
        // names are read.
        private List<string> ReferencesTo(List<ForzaZip.Entry> doomed)
        {
            List<string> hits = new List<string>();
            if (this.zip.IsFolder)
            {
                return hits;
            }
            string[] named = { ".xds", ".tga", ".dds", ".bgf", ".bsg", ".fbf", ".fsb" };
            List<string> stems = new List<string>();
            foreach (ForzaZip.Entry entry in doomed)
            {
                string extension = Path.GetExtension(entry.Name).ToLowerInvariant();
                string stem = Path.GetFileNameWithoutExtension(entry.Name);
                if (!entry.IsDirectory && Array.IndexOf(named, extension) >= 0 && stem.Length >= 6 && !stems.Contains(stem) && stems.Count < 200)
                {
                    stems.Add(stem);
                }
            }
            if (stems.Count == 0)
            {
                return hits;
            }
            string[] readable = { ".xml", ".fbf", ".bgf", ".bsg", ".txt" };
            long budget = 40L * 1024 * 1024;
            foreach (ForzaZip.Entry entry in this.zip.Entries)
            {
                if (entry.IsDirectory || entry.IsDeleted || doomed.Contains(entry) || entry.UncompressedSize > 2 * 1024 * 1024
                    || Array.IndexOf(readable, Path.GetExtension(entry.Name).ToLowerInvariant()) < 0)
                {
                    continue;
                }
                budget -= entry.UncompressedSize;
                if (budget < 0 || hits.Count >= 12)
                {
                    break;
                }
                byte[] data = this.zip.Read(entry);
                string narrow = Encoding.ASCII.GetString(data);
                string wide = Encoding.Unicode.GetString(data, 0, data.Length & ~1);
                foreach (string stem in stems)
                {
                    if (narrow.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0 || wide.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hits.Add(stem + "   <-   " + entry.Name);
                        if (hits.Count >= 12)
                        {
                            break;
                        }
                    }
                }
            }
            return hits;
        }

        // Archive > Delete Selected File or Folder (Del in the tree).
        private void DeleteSelected()
        {
            string what;
            List<ForzaZip.Entry> entries = this.EntriesOfSelection(out what);
            if (entries == null)
            {
                MessageBox.Show(this, "Select a file or a folder in the tree (not the top node).", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<ForzaZip.Entry> files = entries.FindAll(delegate(ForzaZip.Entry e) { return !e.IsDirectory && !e.IsDeleted; });
            if (files.Count == 0)
            {
                this.SetStatus(what + " is already marked for deletion.");
                return;
            }
            foreach (ForzaZip.Entry entry in files)
            {
                if (this.IsPackageEssential(entry))
                {
                    MessageBox.Show(this, entry.Name + " is what makes the folder a DLC package (the offer and the list of zips to mount); the package would not load without it.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            bool folder = this.SelectedEntry == null;
            long bytes = 0;
            bool core = false;
            bool archives = false;
            foreach (ForzaZip.Entry entry in files)
            {
                bytes += entry.UncompressedSize;
                core |= IsCarCore(entry.Name);
                archives |= this.zip.IsFolder && IsArchive(entry.Name);
            }
            List<string> references = this.ReferencesTo(files);

            StringBuilder intro = new StringBuilder();
            intro.AppendFormat("Delete {0} {1}: {2} file(s), {3:N0} bytes.\n", folder ? "the folder" : "the file", what, files.Count, bytes);
            intro.Append(this.zip.IsFolder
                ? (this.InPackage(this.zip.FilePath) ? "They are deleted for good when you save (this is the package's work folder)." : "They go to the Recycle Bin when you save.")
                : "They are left out of the zip when you save.");
            intro.Append("\nUntil then Undo Delete (or Revert) takes them back.");
            if (archives)
            {
                intro.Append("\nZips listed in zipmanifest.xml also lose their line there.");
            }
            StringBuilder detail = new StringBuilder();
            if (core)
            {
                detail.AppendLine("WARNING: this includes model or car data files (.carbin, carattribs.xml, versiondata.xml, gamedb.slt).");
                detail.AppendLine();
            }
            if (references.Count > 0)
            {
                detail.AppendLine("Other files that mention what you delete:");
                foreach (string line in references)
                {
                    detail.AppendLine("  " + line);
                }
                detail.AppendLine();
            }
            detail.AppendLine("Files:");
            for (int i = 0; i < files.Count && i < 80; i++)
            {
                detail.AppendLine("  " + files[i].Name);
            }
            if (files.Count > 80)
            {
                detail.AppendLine("  ... and " + (files.Count - 80) + " more");
            }
            if (files.Count == 1 && !core && references.Count == 0)
            {
                if (MessageBox.Show(this, intro.ToString() + "\n\nDelete?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }
            }
            else if (!this.ConfirmText("Delete", intro.ToString(), detail.ToString()))
            {
                return;
            }

            this.ClearPreviewCache();
            bool removedNew = false;
            foreach (ForzaZip.Entry entry in entries)
            {
                removedNew |= entry.IsAdded;
                this.zip.MarkDeleted(entry);
            }
            if (removedNew)
            {
                this.currentEntry = null;
                this.PopulateTree();
            }
            else
            {
                foreach (ForzaZip.Entry entry in entries)
                {
                    TreeNode node = this.FindEntryNode(entry);
                    if (node != null)
                    {
                        this.SetNodeText(node, entry);
                    }
                }
            }
            this.UpdateTitle();
            this.SetStatus(string.Format("{0}: {1} file(s) marked for deletion - save to apply (Undo Delete takes them back)", what, files.Count));
        }

        // Archive > Undo Delete: the deletion marks of the selected file or folder go.
        private void UndoDeleteSelected()
        {
            string what;
            List<ForzaZip.Entry> entries = this.EntriesOfSelection(out what);
            if (entries == null)
            {
                return;
            }
            int count = 0;
            foreach (ForzaZip.Entry entry in entries)
            {
                if (entry.IsDeleted)
                {
                    this.zip.Undelete(entry);
                    count++;
                    TreeNode node = this.FindEntryNode(entry);
                    if (node != null)
                    {
                        this.SetNodeText(node, entry);
                    }
                }
            }
            this.UpdateTitle();
            this.SetStatus(count == 0 ? "Nothing to undo." : string.Format("{0}: {1} file(s) back", what, count));
        }

        // Folder mode, at Save: the marked files go (Recycle Bin, or for good in a package's work folder), a zip
        // listed in zipmanifest.xml loses its line, and the folders left empty go too. Returns the entries removed.
        private List<ForzaZip.Entry> RemoveDeletedFiles(string stamp)
        {
            List<string> folders = new List<string>();
            List<ForzaZip.Entry> removed = this.zip.RemoveDeleted(delegate(string path)
            {
                if (IsArchive(path))
                {
                    string root = GamePaths.FindRoot(path);
                    string manifest = (root == null) ? null : ZipManifest.PathFor(root);
                    string gamePath = (root == null) ? null : ZipManifest.GamePath(root, Path.GetFullPath(path));
                    if (gamePath != null && ZipManifest.Lists(manifest, gamePath))
                    {
                        BackupFile(manifest, "zipmanifest.xml", stamp);
                        ZipManifest.Remove(manifest, gamePath);
                    }
                }
                if (this.InPackage(path))
                {
                    File.Delete(path);
                }
                else
                {
                    // Dialogs on: a file the Recycle Bin cannot take (too big) is asked about, never deleted silently.
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                folders.Add(Path.GetDirectoryName(path));
            });
            string top = this.zip.FilePath.TrimEnd('\\');
            folders.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
            foreach (string folder in folders)
            {
                for (string dir = folder; dir.Length > top.Length && dir.StartsWith(top + "\\", StringComparison.OrdinalIgnoreCase) && Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0; dir = Path.GetDirectoryName(dir))
                {
                    Directory.Delete(dir);
                }
            }
            return removed;
        }
    }
}
