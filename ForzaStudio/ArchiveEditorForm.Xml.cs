namespace ForzaStudio
{
    using System;
    using System.Drawing;
    using System.Text;
    using System.Windows.Forms;
    using System.Xml;

    // Small XML editor: syntax colors, line/column, find, well-formedness check. Applying writes the
    // text back with the file's original encoding and line endings, so untouched bytes stay the same.
    public partial class ArchiveEditorForm
    {
        private Panel pnlXml;
        private RichTextBox rtbXml;
        private ToolStripButton btnXmlApply;
        private ToolStripTextBox txtXmlFind;
        private ToolStripLabel lblXmlState;
        private ToolStripLabel lblXmlPosition;

        private ForzaZip.Entry xmlEntry;
        private Encoding xmlEncoding;
        private byte[] xmlPreamble;
        private bool xmlCrLf;
        private bool xmlFinalNewline;   // the editor drops a trailing line break, it is added back
        private bool xmlOriginalValid;
        private bool xmlDirty;
        private bool xmlLoading;
        private bool xmlIsXml = true;
        private ToolStripButton btnXmlCheck;

        private void BuildXmlView()
        {
            this.pnlXml = new Panel();
            this.pnlXml.Dock = DockStyle.Fill;

            this.rtbXml = new RichTextBox();
            this.rtbXml.Dock = DockStyle.Fill;
            this.rtbXml.Font = new Font("Consolas", 9.75f);
            this.rtbXml.WordWrap = false;
            this.rtbXml.ScrollBars = RichTextBoxScrollBars.Both;
            this.rtbXml.AcceptsTab = true;
            this.rtbXml.DetectUrls = false;
            this.rtbXml.HideSelection = false;
            this.rtbXml.BorderStyle = BorderStyle.None;
            this.rtbXml.TextChanged += delegate
            {
                if (!this.xmlLoading && !this.xmlDirty)
                {
                    this.xmlDirty = true;
                    this.UpdateXmlState(null);
                }
            };
            this.rtbXml.SelectionChanged += delegate { this.UpdateXmlPosition(); };
            this.rtbXml.KeyDown += new KeyEventHandler(this.rtbXml_KeyDown);

            ToolStrip bar = new ToolStrip();
            bar.GripStyle = ToolStripGripStyle.Hidden;
            this.btnXmlApply = new ToolStripButton("Apply (Ctrl+Enter)", null, delegate { this.ApplyXml(true); });
            bar.Items.Add(this.btnXmlApply);
            bar.Items.Add(new ToolStripButton("Discard Edits", null, delegate { this.DiscardXml(); }));
            this.btnXmlCheck = new ToolStripButton("Check XML", null, delegate { this.CheckXml(true); });
            bar.Items.Add(this.btnXmlCheck);
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripLabel("Find:"));
            this.txtXmlFind = new ToolStripTextBox();
            this.txtXmlFind.Width = 160;
            this.txtXmlFind.ToolTipText = "Enter or F3: find next";
            this.txtXmlFind.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.F3)
                {
                    e.SuppressKeyPress = true;
                    this.FindXml();
                }
            };
            bar.Items.Add(this.txtXmlFind);
            bar.Items.Add(new ToolStripButton("Next", null, delegate { this.FindXml(); }));
            this.lblXmlPosition = new ToolStripLabel();
            this.lblXmlPosition.Alignment = ToolStripItemAlignment.Right;
            this.lblXmlState = new ToolStripLabel();
            this.lblXmlState.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(this.lblXmlPosition);
            bar.Items.Add(this.lblXmlState);

            this.pnlXml.Controls.Add(this.rtbXml);
            this.pnlXml.Controls.Add(bar);
        }

        private void ShowXml(ForzaZip.Entry entry, byte[] data)
        {
            this.ShowTextFile(entry, data, true);
        }

        // Settings files (.ini, .cfg, .txt) use the same editor, with their own colors and no XML check.
        private void ShowTextFile(ForzaZip.Entry entry, byte[] data, bool isXml)
        {
            this.xmlIsXml = isXml;
            this.btnXmlCheck.Visible = isXml;
            this.xmlEntry = entry;
            string text = DecodeText(data, out this.xmlEncoding, out this.xmlPreamble);
            this.xmlCrLf = text.Contains("\r\n");
            text = text.Replace("\r\n", "\n");
            this.xmlOriginalValid = isXml && ParseXml(text) == null;
            this.xmlFinalNewline = text.EndsWith("\n");
            if (this.xmlFinalNewline)
            {
                text = text.Substring(0, text.Length - 1);
            }
            this.LoadXmlText(text, 0);
            this.ShowView(this.pnlXml);
        }

        // Text with its byte order mark, if any (UTF-8 without one otherwise, which covers plain ASCII).
        private static string DecodeText(byte[] data, out Encoding encoding, out byte[] preamble)
        {
            preamble = new byte[0];
            encoding = new UTF8Encoding(false);
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                preamble = new byte[] { 0xEF, 0xBB, 0xBF };
            }
            else if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            {
                preamble = new byte[] { 0xFF, 0xFE };
                encoding = new UnicodeEncoding(false, false);
            }
            else if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            {
                preamble = new byte[] { 0xFE, 0xFF };
                encoding = new UnicodeEncoding(true, false);
            }
            return encoding.GetString(data, preamble.Length, data.Length - preamble.Length);
        }

        private void LoadXmlText(string text, int caret)
        {
            this.xmlLoading = true;
            this.rtbXml.Rtf = this.xmlIsXml ? BuildXmlRtf(text, this.rtbXml.Font) : BuildSettingsRtf(text, this.rtbXml.Font);
            this.rtbXml.ClearUndo();
            this.rtbXml.SelectionStart = Math.Min(caret, this.rtbXml.TextLength);
            this.rtbXml.ScrollToCaret();
            this.xmlLoading = false;
            this.xmlDirty = false;
            this.UpdateXmlState(null);
            this.UpdateXmlPosition();
        }

        private void rtbXml_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Control)
            {
                e.SuppressKeyPress = true;
                this.ApplyXml(true);
            }
            else if (e.KeyCode == Keys.Enter && !e.Shift && !e.Alt)
            {
                // Keep the indentation of the current line.
                e.SuppressKeyPress = true;
                int line = this.rtbXml.GetLineFromCharIndex(this.rtbXml.SelectionStart);
                int start = this.rtbXml.GetFirstCharIndexFromLine(line);
                string text = this.rtbXml.Text;
                int end = start;
                while (end < text.Length && end < this.rtbXml.SelectionStart && (text[end] == ' ' || text[end] == '\t'))
                {
                    end++;
                }
                this.rtbXml.SelectedText = "\n" + text.Substring(start, end - start);
            }
            else if (e.KeyCode == Keys.V && e.Control && !e.Alt)
            {
                // Paste as plain text: rich text from other programs would bring its own fonts.
                e.SuppressKeyPress = true;
                if (Clipboard.ContainsText())
                {
                    this.rtbXml.SelectedText = Clipboard.GetText().Replace("\r\n", "\n");
                }
            }
            else if (e.KeyCode == Keys.F && e.Control)
            {
                e.SuppressKeyPress = true;
                if (this.rtbXml.SelectionLength > 0 && this.rtbXml.SelectionLength < 100 && !this.rtbXml.SelectedText.Contains("\n"))
                {
                    this.txtXmlFind.Text = this.rtbXml.SelectedText;
                }
                this.txtXmlFind.Focus();
                this.txtXmlFind.SelectAll();
            }
            else if (e.KeyCode == Keys.F3)
            {
                e.SuppressKeyPress = true;
                this.FindXml();
            }
        }

        private void FindXml()
        {
            string find = this.txtXmlFind.Text;
            if (find.Length == 0)
            {
                return;
            }
            int from = this.rtbXml.SelectionStart + this.rtbXml.SelectionLength;
            int found = (from < this.rtbXml.TextLength) ? this.rtbXml.Find(find, from, RichTextBoxFinds.None) : -1;
            if (found < 0)
            {
                found = this.rtbXml.Find(find, 0, RichTextBoxFinds.None);
            }
            if (found < 0)
            {
                this.SetStatus("\"" + find + "\" not found");
                return;
            }
            this.rtbXml.Focus();
        }

        private void UpdateXmlPosition()
        {
            int caret = this.rtbXml.SelectionStart;
            int line = this.rtbXml.GetLineFromCharIndex(caret);
            int column = caret - this.rtbXml.GetFirstCharIndexFromLine(line);
            this.lblXmlPosition.Text = string.Format("Ln {0}, Col {1}", line + 1, column + 1);
        }

        private void UpdateXmlState(string error)
        {
            this.btnXmlApply.Enabled = this.xmlDirty;
            if (error != null)
            {
                this.lblXmlState.Text = error;
                this.lblXmlState.ForeColor = ForzaTheme.Red;
            }
            else
            {
                this.lblXmlState.Text = this.xmlDirty ? "Edited - Ctrl+Enter to apply" : "";
                this.lblXmlState.ForeColor = ForzaTheme.Text;
            }
        }

        // Returns null when the text is well formed, otherwise the error with its line and column.
        private static string ParseXml(string text)
        {
            try
            {
                new XmlDocument().LoadXml(text);
                return null;
            }
            catch (XmlException exception)
            {
                return exception.Message;
            }
        }

        private bool CheckXml(bool report)
        {
            string text = this.rtbXml.Text;
            try
            {
                new XmlDocument().LoadXml(text);
                if (report)
                {
                    this.UpdateXmlState(null);
                    this.lblXmlState.Text = "XML is well formed";
                    this.lblXmlState.ForeColor = System.Drawing.Color.FromArgb(110, 200, 110);
                }
                return true;
            }
            catch (XmlException exception)
            {
                this.UpdateXmlState(string.Format("Error at line {0}, column {1}", exception.LineNumber, exception.LinePosition));
                if (exception.LineNumber > 0)
                {
                    int start = this.rtbXml.GetFirstCharIndexFromLine(exception.LineNumber - 1);
                    if (start >= 0)
                    {
                        this.rtbXml.SelectionStart = Math.Min(start + Math.Max(0, exception.LinePosition - 1), this.rtbXml.TextLength);
                        this.rtbXml.SelectionLength = 0;
                        this.rtbXml.ScrollToCaret();
                    }
                }
                if (report)
                {
                    MessageBox.Show(this, exception.Message, "Check XML", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                return false;
            }
        }

        // Stores the edited text in the archive (pending until Save). Returns false if the user cancels.
        private bool ApplyXml(bool interactive)
        {
            if (!this.xmlDirty || this.xmlEntry == null)
            {
                return true;
            }
            if (this.xmlIsXml && !this.CheckXml(false) && this.xmlOriginalValid)
            {
                DialogResult answer = MessageBox.Show(this, "The XML is not well formed (" + this.lblXmlState.Text + "). The game may not load it.\n\nApply anyway?",
                    "Apply XML", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes)
                {
                    return false;
                }
            }
            string text = this.rtbXml.Text.Replace("\r\n", "\n");
            if (this.xmlFinalNewline && !text.EndsWith("\n"))
            {
                text += "\n";
            }
            if (this.xmlCrLf)
            {
                text = text.Replace("\n", "\r\n");
            }
            byte[] body = this.xmlEncoding.GetBytes(text);
            byte[] data = new byte[this.xmlPreamble.Length + body.Length];
            Buffer.BlockCopy(this.xmlPreamble, 0, data, 0, this.xmlPreamble.Length);
            Buffer.BlockCopy(body, 0, data, this.xmlPreamble.Length, body.Length);

            ForzaZip.Entry previous = this.currentEntry;
            this.currentEntry = this.xmlEntry;
            this.ReplaceCurrent(data);
            this.currentEntry = previous;
            if (interactive)
            {
                this.LoadXmlText(this.rtbXml.Text, this.rtbXml.SelectionStart);
                this.SetStatus(string.Format("{0} updated - save to write it to {1}", this.xmlEntry.Name, this.SourceName));
            }
            else
            {
                this.xmlDirty = false;
            }
            return true;
        }

        private void DiscardXml()
        {
            if (this.xmlEntry != null)
            {
                this.ShowTextFile(this.xmlEntry, this.zip.Read(this.xmlEntry), this.xmlIsXml);
            }
        }

        // Before leaving the editor: apply, discard or stay. Returns false to stay.
        private bool ConfirmXmlEdits()
        {
            if (!this.xmlDirty || this.xmlEntry == null)
            {
                return true;
            }
            DialogResult answer = MessageBox.Show(this, "Apply your edits to " + this.xmlEntry.Name + "?", "XML Editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel)
            {
                return false;
            }
            if (answer == DialogResult.No)
            {
                this.xmlDirty = false;
                return true;
            }
            return this.ApplyXml(false);
        }

        #region Syntax colors

        // Text, delimiters, tag/key, attribute, value, comment, declaration (for the dark editor).
        private const string DarkColorTable = @"{\colortbl ;\red220\green220\blue220;\red128\green128\blue128;\red86\green156\blue214;\red156\green220\blue254;\red206\green145\blue120;\red106\green153\blue85;\red150\green150\blue150;}";

        private const int ColorText = 1;
        private const int ColorDelimiter = 2;
        private const int ColorTagName = 3;
        private const int ColorAttribute = 4;
        private const int ColorValue = 5;
        private const int ColorComment = 6;
        private const int ColorDeclaration = 7;

        private static string BuildXmlRtf(string text, Font font)
        {
            StringBuilder rtf = new StringBuilder(text.Length * 2);
            rtf.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0\fmodern " + font.Name + ";}}");
            rtf.Append(DarkColorTable);
            rtf.Append(@"\f0\fs" + (int) Math.Round(font.SizeInPoints * 2) + " ");
            int i = 0;
            while (i < text.Length)
            {
                if (string.CompareOrdinal(text, i, "<!--", 0, 4) == 0)
                {
                    int end = text.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    end = (end < 0) ? text.Length : end + 3;
                    AppendRtf(rtf, ColorComment, text, i, end);
                    i = end;
                }
                else if (string.CompareOrdinal(text, i, "<?", 0, 2) == 0 || string.CompareOrdinal(text, i, "<!", 0, 2) == 0)
                {
                    int end = text.IndexOf('>', i);
                    end = (end < 0) ? text.Length : end + 1;
                    AppendRtf(rtf, ColorDeclaration, text, i, end);
                    i = end;
                }
                else if (text[i] == '<')
                {
                    i = AppendTag(rtf, text, i);
                }
                else
                {
                    int end = text.IndexOf('<', i);
                    end = (end < 0) ? text.Length : end;
                    AppendRtf(rtf, ColorText, text, i, end);
                    i = end;
                }
            }
            rtf.Append('}');
            return rtf.ToString();
        }

        // Game settings ("Section\Key value" per line, also [section], key = value and ; # // comments).
        private static string BuildSettingsRtf(string text, Font font)
        {
            StringBuilder rtf = new StringBuilder(text.Length * 2);
            rtf.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0\fmodern " + font.Name + ";}}");
            rtf.Append(DarkColorTable);
            rtf.Append(@"\f0\fs" + (int) Math.Round(font.SizeInPoints * 2) + " ");
            string[] lines = text.Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n];
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith(";") || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                {
                    AppendRtf(rtf, ColorComment, line, 0, line.Length);
                }
                else if (trimmed.StartsWith("["))
                {
                    AppendRtf(rtf, ColorTagName, line, 0, line.Length);
                }
                else
                {
                    int key = line.Length - trimmed.Length;
                    while (key < line.Length && !char.IsWhiteSpace(line[key]) && line[key] != '=')
                    {
                        key++;
                    }
                    AppendRtf(rtf, ColorTagName, line, 0, key);
                    int value = key;
                    while (value < line.Length && (char.IsWhiteSpace(line[value]) || line[value] == '='))
                    {
                        value++;
                    }
                    AppendRtf(rtf, ColorDelimiter, line, key, value);
                    AppendRtf(rtf, ColorValue, line, value, line.Length);
                }
                if (n < lines.Length - 1)
                {
                    rtf.Append("\\par\r\n");
                }
            }
            rtf.Append('}');
            return rtf.ToString();
        }

        // <name attr="value" ... /> : returns the index after the tag.
        private static int AppendTag(StringBuilder rtf, string text, int i)
        {
            int start = i;
            i++;
            if (i < text.Length && text[i] == '/')
            {
                i++;
            }
            AppendRtf(rtf, ColorDelimiter, text, start, i);
            int name = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '>' && text[i] != '/')
            {
                i++;
            }
            AppendRtf(rtf, ColorTagName, text, name, i);
            while (i < text.Length && text[i] != '>')
            {
                char c = text[i];
                if (c == '"' || c == '\'')
                {
                    int end = text.IndexOf(c, i + 1);
                    end = (end < 0) ? text.Length : end + 1;
                    AppendRtf(rtf, ColorValue, text, i, end);
                    i = end;
                }
                else if (c == '=' || c == '/')
                {
                    AppendRtf(rtf, ColorDelimiter, text, i, i + 1);
                    i++;
                }
                else if (char.IsWhiteSpace(c))
                {
                    int end = i;
                    while (end < text.Length && char.IsWhiteSpace(text[end]))
                    {
                        end++;
                    }
                    AppendRtf(rtf, ColorText, text, i, end);
                    i = end;
                }
                else
                {
                    int end = i;
                    while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '=' && text[end] != '>' && text[end] != '/' && text[end] != '"' && text[end] != '\'')
                    {
                        end++;
                    }
                    AppendRtf(rtf, ColorAttribute, text, i, end);
                    i = end;
                }
            }
            if (i < text.Length)
            {
                AppendRtf(rtf, ColorDelimiter, text, i, i + 1);
                i++;
            }
            return i;
        }

        private static void AppendRtf(StringBuilder rtf, int color, string text, int start, int end)
        {
            if (end <= start)
            {
                return;
            }
            rtf.Append(@"\cf").Append(color).Append(' ');
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '\\':
                    case '{':
                    case '}':
                        rtf.Append('\\').Append(c);
                        break;
                    case '\n':
                        rtf.Append("\\par\r\n");
                        break;
                    case '\r':
                        break;
                    case '\t':
                        rtf.Append("\\tab ");
                        break;
                    default:
                        if (c > 127)
                        {
                            rtf.Append("\\u").Append((int) (short) c).Append('?');
                        }
                        else
                        {
                            rtf.Append(c);
                        }
                        break;
                }
            }
        }

        #endregion
    }
}
