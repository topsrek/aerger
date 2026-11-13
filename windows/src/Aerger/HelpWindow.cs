using System.Text;
using System.Text.RegularExpressions;

namespace Aerger;

internal sealed class HelpWindow : Form
{
    internal sealed record Hotspot(int Start, int Length, string Target);

    internal sealed record Topic(string Id, string Title, string Rtf, string Text, string[] Links, Hotspot[] Hotspots)
    {
        public override string ToString() => Title;
    }

    private readonly TextBox search = new() { Dock = DockStyle.Top, PlaceholderText = "Hilfethemen durchsuchen …", AccessibleName = "Hilfethemen durchsuchen" };
    private readonly ListBox contents = new() { Dock = DockStyle.Fill, IntegralHeight = false, AccessibleName = "Hilfethemen" };
    private readonly RichTextBox article = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, DetectUrls = false, AccessibleName = "Hilfetext" };
    private readonly Button back = new() { Text = "&Zurück", AutoSize = true, Enabled = false };
    private readonly Label resultStatus = new() { Dock = DockStyle.Bottom, Height = 24, Padding = new(3), AutoEllipsis = true };
    private readonly Stack<Topic> history = new();
    private Topic? current;
    private bool selecting;
    internal IReadOnlyList<Topic> Topics { get; }
    internal Topic? CurrentTopic => current;
    internal string ArticleText => article.Text;
    internal RichTextBox Article => article;
    internal int ResultCount => contents.Items.Count;

    internal HelpWindow()
    {
        Text = "M.ä.D.N. Hilfe";
        Size = new(900, 650);
        MinimumSize = new(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        using var reader = new StreamReader(typeof(HelpWindow).Assembly.GetManifestResourceStream("Aerger.Reference.ERGWIN.rtf")!);
        Topics = ReadTopics(reader.ReadToEnd());

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new(4) };
        var home = new Button { Text = "&Inhalt", AutoSize = true };
        toolbar.Controls.AddRange([back, home]);
        var split = new SplitContainer { Size = new(850, 550), Dock = DockStyle.Fill, SplitterDistance = 250, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(contents);
        split.Panel1.Controls.Add(resultStatus);
        split.Panel1.Controls.Add(search);
        split.Panel1.Controls.Add(new Label { Text = "&Suche in Titel und Text:", Dock = DockStyle.Top, Height = 22, Padding = new(3) });
        split.Panel2.Controls.Add(article);
        Controls.Add(split);
        Controls.Add(toolbar);
        search.TextChanged += (_, _) => Filter(search.Text);
        contents.SelectedIndexChanged += (_, _) =>
        {
            if (!selecting && contents.SelectedItem is Topic topic) Navigate(topic);
        };
        article.MouseMove += (_, e) => article.Cursor = HotspotAt(e.Location) is null ? Cursors.IBeam : Cursors.Hand;
        article.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && article.SelectionLength == 0 && HotspotAt(e.Location) is { } hotspot) Follow(hotspot);
        };
        back.Click += (_, _) => GoBack();
        home.Click += (_, _) => OpenTopic("Inhalt");
        Filter("");
        OpenTopic("Inhalt");
    }

    // Helpdeco's RTF contains one $ title and # context footnote per topic.
    // Split at these markers instead of page breaks: some topics have extra pages.
    private static Topic[] ReadTopics(string rtf)
    {
        const string titlePattern = @"\{\\up \$\}\{\\footnote\\pard\\plain\{\\up \$\} (?<title>[^{}]*)\}";
        // Helpdeco writes every jump as \uldb text\plain...{\v target}.
        const string jumpPattern = @"\\uldb ?(?<text>(?:(?!\\plain)[^{}])*)\\plain(?<after>[^{}]*)\{\\v (?<id>[^{}]*)\}";
        var markers = Regex.Matches(rtf, titlePattern);
        string header = rtf[..rtf.IndexOf(@"\pard\plain", StringComparison.Ordinal)];
        // WinHelp 3.1 draws jump hotspots green with a solid underline (colour 2).
        const string colours = @"{\colortbl;\red0\green0\blue0;}";
        if (!header.Contains(colours, StringComparison.Ordinal)) throw new InvalidDataException("Unerwartete Farbtabelle der Hilfe.");
        header = header.Replace(colours, @"{\colortbl;\red0\green0\blue0;\red0\green128\blue0;}", StringComparison.Ordinal);
        using var decoder = new RichTextBox();
        string Decode(string value)
        {
            decoder.Rtf = header + @"\pard\plain\f2\fs20 " + value + "}";
            return decoder.Text.Trim();
        }
        string Document(string value)
        {
            value = Regex.Replace(value, @"\\page\b", "");
            // WinHelp used fs0 as its default text size; RichEdit needs a real size.
            value = Regex.Replace(value, @"\\fs0\b", @"\fs20");
            value = Regex.Replace(value, @"\\cb1\b", @"\cb0");
            return header + @"\pard\plain\f2\fs20 " + value.Trim() + "}";
        }
        var topics = new List<Topic>();
        for (int index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            int end = index + 1 < markers.Count ? markers[index + 1].Index : rtf.LastIndexOf('}');
            string body = rtf[(marker.Index + marker.Length)..end];
            var context = Regex.Match(body, @"\{\\up #\}\{\\footnote\\pard\\plain\{\\up #\} (?<id>[^{}]*)\}");
            string id = context.Groups["id"].Value.Trim();
            if (id.Length == 0) throw new InvalidDataException("Hilfethema ohne Kontextkennung.");
            string[] targets = Regex.Matches(body, @"\{\\v (?<id>[^{}]*)\}")
                .Select(match => match.Groups["id"].Value.Trim()).Distinct().ToArray();
            body = Regex.Replace(body, @"\{\\up [#$K]\}\{\\footnote\\pard\\plain\{\\up [#$K]\} [^{}]*\}", "");

            // Private-use marker characters locate each hotspot's visible text after
            // RichEdit has decoded fonts, escapes and paragraphs exactly as displayed.
            var jumpTargets = new List<string>();
            string Jump(Match match, bool marked)
            {
                if (marked) jumpTargets.Add(match.Groups["id"].Value.Trim());
                string open = marked ? @"{\u-8192?}" : "";
                string close = marked ? @"{\u-8191?}" : "";
                return @"\cf2\ul " + open + match.Groups["text"].Value + close + @"\plain" + match.Groups["after"].Value;
            }
            string markedBody = Regex.Replace(body, jumpPattern, match => Jump(match, true));
            body = Regex.Replace(body, jumpPattern, match => Jump(match, false));
            if (body.Contains(@"{\v ", StringComparison.Ordinal) || body.Contains(@"\uldb", StringComparison.Ordinal))
                throw new InvalidDataException($"Unbekanntes Hotspot-Format im Hilfethema {id}.");

            decoder.Rtf = Document(markedBody);
            string markedText = decoder.Text;
            var hotspots = new List<Hotspot>();
            var plain = new StringBuilder(markedText.Length);
            int start = -1;
            foreach (char character in markedText)
            {
                if (character == '\uE000') start = plain.Length;
                else if (character == '\uE001')
                {
                    if (start < 0 || hotspots.Count >= jumpTargets.Count) throw new InvalidDataException($"Hotspot ohne Anfang im Hilfethema {id}.");
                    hotspots.Add(new(start, plain.Length - start, jumpTargets[hotspots.Count]));
                    start = -1;
                }
                else plain.Append(character);
            }
            if (start >= 0 || hotspots.Count != jumpTargets.Count) throw new InvalidDataException($"Unvollständige Hotspots im Hilfethema {id}.");
            string document = Document(body);
            decoder.Rtf = document;
            string text = decoder.Text;
            if (text != plain.ToString()) throw new InvalidDataException($"Hotspot-Positionen im Hilfethema {id} weichen ab.");
            topics.Add(new(id, Decode(marker.Groups["title"].Value), document, text, targets, hotspots.ToArray()));
        }
        return topics.ToArray();
    }

    internal void Filter(string query)
    {
        selecting = true;
        contents.BeginUpdate();
        try
        {
            contents.Items.Clear();
            foreach (var topic in Topics.Where(topic => topic.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                         topic.Text.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
                contents.Items.Add(topic);
            contents.SelectedItem = current;
            resultStatus.Text = contents.Items.Count == 0 ? "Keine passenden Themen" : $"{contents.Items.Count} Themen";
        }
        finally { contents.EndUpdate(); selecting = false; }
    }

    internal void OpenTopic(string id)
    {
        var topic = Topics.FirstOrDefault(topic => topic.Id == id);
        if (topic is not null) Navigate(topic);
    }

    private void Navigate(Topic topic, bool remember = true)
    {
        if (current == topic) return;
        if (remember && current is not null) history.Push(current);
        current = topic;
        article.Rtf = topic.Rtf;
        article.Select(0, 0);
        article.ScrollToCaret();
        selecting = true;
        contents.SelectedItem = topic;
        selecting = false;
        back.Enabled = history.Count > 0;
        Text = $"{topic.Title} – M.ä.D.N. Hilfe";
    }

    internal Hotspot? HotspotAt(Point point)
    {
        if (current is null || article.TextLength == 0) return null;
        int index = article.GetCharIndexFromPosition(point);
        Point character = article.GetPositionFromCharIndex(index);
        // RichEdit reports the nearest caret. In the right half of a glyph it
        // can return the following caret, while the pointer is still inside it.
        if(point.X<character.X&&index>0)
        {
            Point previous=article.GetPositionFromCharIndex(index-1);
            if(previous.Y==character.Y) {index--;character=previous;}
        }
        var hotspot = current.Hotspots.FirstOrDefault(candidate => index >= candidate.Start && index < candidate.Start + candidate.Length);
        if (hotspot is null) return null;
        // GetCharIndexFromPosition returns the nearest character, even beside a line.
        Point next = article.GetPositionFromCharIndex(index + 1);
        bool sameLine = index + 1 < article.TextLength && next.Y == character.Y && next.X > character.X;
        int width = sameLine ? next.X - character.X : article.Font.Height;
        int height = next.Y > character.Y ? next.Y - character.Y : article.Font.Height + 4;
        if (point.X < character.X || point.X >= character.X + width || point.Y < character.Y || point.Y >= character.Y + height) return null;
        return hotspot;
    }

    internal Hotspot? SelectedHotspot => current?.Hotspots.FirstOrDefault(hotspot =>
        article.SelectionStart == hotspot.Start && article.SelectionLength == hotspot.Length);

    internal void Follow(Hotspot hotspot) => OpenTopic(hotspot.Target);

    // WinHelp moves between hotspots with Tab/Shift+Tab and follows the selected one with Enter.
    internal bool SelectHotspot(bool forward)
    {
        if (current is null) return false;
        int caret = article.SelectionStart;
        bool onHotspot = SelectedHotspot is not null;
        var target = forward
            ? current.Hotspots.FirstOrDefault(hotspot => onHotspot ? hotspot.Start > caret : hotspot.Start >= caret)
            : current.Hotspots.LastOrDefault(hotspot => hotspot.Start < caret);
        if (target is null) return false;
        article.Select(target.Start, target.Length);
        article.ScrollToCaret();
        return true;
    }

    internal void GoBack()
    {
        if (history.TryPop(out var topic)) Navigate(topic, false);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        if (keyData == (Keys.Alt | Keys.Left)) { GoBack(); return true; }
        if (keyData == (Keys.Control | Keys.F)) { search.Focus(); search.SelectAll(); return true; }
        if (article.Focused)
        {
            if (keyData == Keys.Tab && SelectHotspot(true)) return true;
            if (keyData == (Keys.Shift | Keys.Tab) && SelectHotspot(false)) return true;
            if (keyData == Keys.Enter && SelectedHotspot is { } hotspot) { Follow(hotspot); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
