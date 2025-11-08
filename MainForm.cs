using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RecallCopy
{
    public partial class MainForm : Form
    {
        ClipboardWatcher watcher;
        HistoryManager history;

        public MainForm()
        {
            InitializeComponent();

            SettingsManager.Load();

            history = new HistoryManager();
            watcher = new ClipboardWatcher();

            watcher.OnNewClipboardText += text =>
            {
                history.Add(text);
                UpdateList();
            };

            searchBox.TextChanged += (s, e) =>
            {
                string cleaned = Regex.Replace(searchBox.Text, @"[\u0000-\u001F\u007F]", "");
                if (searchBox.Text != cleaned)
                    searchBox.Text = cleaned;

                UpdateList();
            };

            searchBox.KeyDown += SearchBox_KeyDown;
            historyList.KeyDown += HistoryList_KeyDown;

            this.FormClosing += (s, e) =>
            {
                HotKeyManager.UnregisterHotKey(this.Handle, HotKeyManager.HOTKEY_ID);
            };

            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            HotKeyManager.UnregisterHotKey(this.Handle, HotKeyManager.HOTKEY_ID);
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotKeyManager.HOTKEY_ID)
            {
                if (this.Visible && this.Opacity > 0)
                {
                    this.Hide();
                    this.Opacity = 0;
                }
                else
                {
                    this.Opacity = 0.9;
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    this.Activate();
                    searchBox.Focus();
                    searchBox.SelectAll();
                }
            }
            base.WndProc(ref m);
        }

        // OLD OWN VERSION
        //private void UpdateList()
        //{
        //    string query = searchBox.Text.ToLower();
        //    historyList.Items.Clear();

        //    List<string> startsWith = new List<string>();
        //    List<string> contains = new List<string>();

        //    foreach (var entry in history.Entries)
        //    {
        //        string lower = entry.ToLower();
        //        if (lower.StartsWith(query))
        //            startsWith.Add(entry);
        //        else if (lower.Contains(query))
        //            contains.Add(entry);
        //    }

        //    foreach (var entry in startsWith)
        //        historyList.Items.Add(entry);

        //    foreach (var entry in contains)
        //        historyList.Items.Add(entry);

        //    if (historyList.Items.Count > 0)
        //        historyList.SelectedIndex = 0;
        //}

        private void UpdateList()
        {
            string raw = searchBox.Text ?? "";
            string query = raw.Trim().ToLower();
            historyList.Items.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (var entry in history.Entries)
                    historyList.Items.Add(entry);
                return;
            }

            // --- Parse quoted phrases ---
            var exactPhrases = new List<string>();
            var excludedTerms = new List<string>();

            var quoteRegex = new System.Text.RegularExpressions.Regex("\"([^\"]+)\"");
            foreach (System.Text.RegularExpressions.Match m in quoteRegex.Matches(query))
                exactPhrases.Add(m.Groups[1].Value.ToLower());

            // remove quoted phrases from the query string
            query = quoteRegex.Replace(query, "").Trim();

            // --- Build OR groups ---
            var orGroups = new List<List<string>>();

            if (!string.IsNullOrWhiteSpace(query))
            {
                // split by " or " (case-insensitive was handled by lowercasing)
                var orParts = query.Split(new[] { " or " }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in orParts)
                {
                    var group = new List<string>();
                    // split by spaces into tokens using a char[] overload (compatible)
                    var tokens = part.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var token in tokens)
                    {
                        if (token.StartsWith("-") && token.Length > 1)
                            excludedTerms.Add(token.Substring(1));
                        else
                            group.Add(token);
                    }

                    if (group.Count > 0)
                        orGroups.Add(group);
                }
            }

            // If query produced no OR groups and we have no quoted phrases, treat as single group of all positive tokens
            if (orGroups.Count == 0 && exactPhrases.Count == 0)
            {
                // fallback: split original raw (no quotes) into tokens, excluding negatives
                var tokens = raw.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var group = new List<string>();
                foreach (var t in tokens)
                {
                    if (t.StartsWith("-") && t.Length > 1)
                        excludedTerms.Add(t.Substring(1));
                    else if (t.ToLower() != "or")
                        group.Add(t);
                }
                if (group.Count > 0)
                    orGroups.Add(group);
            }

            // --- Determine a "starts-with" token (first positive term if any) ---
            string startsWithToken = null;
            // prefer the first term from the first OR group if available
            if (orGroups.Count > 0 && orGroups[0].Count > 0)
                startsWithToken = orGroups[0][0];
            else if (exactPhrases.Count > 0)
                startsWithToken = exactPhrases[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            // --- Filter entries ---
            var startsWith = new List<string>();
            var contains = new List<string>();

            foreach (var entry in history.Entries)
            {
                string lower = entry.ToLower();

                // excluded terms: reject immediately
                if (excludedTerms.Any(ex => lower.Contains(ex)))
                    continue;

                // exact phrases: all quoted phrases must be present
                if (exactPhrases.Any() && !exactPhrases.All(p => lower.Contains(p)))
                    continue;

                // OR groups: at least one group must fully match (all terms in that group present)
                bool orMatch = (orGroups.Count == 0); // if no groups, treat as matched (because maybe only quotes were used)
                foreach (var group in orGroups)
                {
                    if (group.All(t => lower.Contains(t)))
                    {
                        orMatch = true;
                        break;
                    }
                }
                if (!orMatch)
                    continue;

                // entry passes filters => bucket it
                if (!string.IsNullOrEmpty(startsWithToken) && lower.StartsWith(startsWithToken))
                    startsWith.Add(entry);
                else
                    contains.Add(entry);
            }

            // --- Display results (starts-with first) ---
            foreach (var e in startsWith.Concat(contains))
                historyList.Items.Add(e);

            if (historyList.Items.Count > 0)
                historyList.SelectedIndex = 0;
        }



        private void RestoreSelected()
        {
            if (historyList.SelectedItem != null)
            {
                string selectedText = historyList.SelectedItem.ToString();
                Clipboard.SetText(selectedText);
                history.Add(selectedText); // Move to top
                UpdateList();
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                Application.Exit();
            }

            // CHATGPT START
            if (e.Control && e.KeyCode == Keys.Back)
            {
                int pos = searchBox.SelectionStart;
                string text = searchBox.Text;

                if (pos > 0)
                {
                    int start = pos - 1;
                    while (start > 0 && char.IsWhiteSpace(text[start])) start--;
                    while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;

                    searchBox.Text = text.Remove(start, pos - start);
                    searchBox.SelectionStart = start;
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            // CHATGPT END

            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.S)
            {
                if (historyList.Items.Count > 0 && historyList.SelectedIndex < historyList.Items.Count - 1)
                    historyList.SelectedIndex++;
                e.Handled = true;
                //e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.W)
            {
                if (historyList.SelectedIndex > 0)
                    historyList.SelectedIndex--;
                e.Handled = true;
                //e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                RestoreSelected();
                Hide();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Hide();
                e.SuppressKeyPress = true;
            }
        }




        private void HistoryList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                RestoreSelected();
                Hide();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Hide();
                e.SuppressKeyPress = true;
            }
        }

        private void searchBox_TextChanged(object sender, EventArgs e)
        {
            if (searchBox.Text == "")
            {
                placeholderLabel.Visible = true;
            }
            else { placeholderLabel.Visible = false; }
        }

        private void historyList_KeyPress(object sender, KeyPressEventArgs e)
        {
            searchBox.Text += e.KeyChar;
            searchBox.Focus();
        }
    }
}
