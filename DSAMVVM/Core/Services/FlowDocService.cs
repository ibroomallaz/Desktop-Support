using DSAMVVM.Core.Interfaces;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace DSAMVVM.Core.Services
{
    public sealed class FlowDocService(IOutputTextSettingsProvider textSettings) : IFlowDocService
    {
        private readonly IOutputTextSettingsProvider _textSettings = textSettings ?? throw new ArgumentNullException(nameof(textSettings));
        private const double DefaultLineHeightRatio = 1.08;
        private const string DefaultPlaceholder = "Type in information in the search bar above and press enter to search";

        private static readonly IReadOnlyDictionary<string, string> PlaceholderByView = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UserView"] = "Enter a NetID above, then press Enter to search.",
            ["ComputerView"] = "Enter a computer name above, then press Enter to search.",
            ["GroupView"] = "Select \"User's MIM Groups\" to search by NetID, or \"Group Members\" for department number, then press Enter to search.",
            ["EntraView"] = "Enter an Entra user or device above, then press Enter to search.",
        };

        private static SolidColorBrush FrozenBrush(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
            if (b.CanFreeze) b.Freeze();
            return b;
        }

        private static readonly IReadOnlyDictionary<string, Brush> DefaultColors =
            new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase)
            {
                ["red"] = FrozenBrush("#E57373"),
                ["green"] = FrozenBrush("#66BB6A"),
                ["yellow"] = FrozenBrush("#FFD54F"),
                ["cyan"] = FrozenBrush("#81D4FA"),
                ["white"] = FrozenBrush("#D0D3D6"),
                ["lightgray"] = FrozenBrush("#B0B3B8")
            };

        private static readonly Regex UrlRegex =
            new(@"(?:https?|file)://[^\s)\]}>,\""]*[^\s)\]}>,\.\""]",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MarkdownLinkRegex =
            new(@"\[(?<label>[^\]]+)\]\((?<url>(?:https?|file)://[^\s)\]}>,\""]*[^\s)\]}>,\.\""])\)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static TextDecoration BuildUnderlineForBrush(Brush brush)
        {
            var pen = new Pen(brush, 1.0);
            if (pen.CanFreeze) pen.Freeze();
            var deco = new TextDecoration
            {
                Location = TextDecorationLocation.Underline,
                Pen = pen,
                PenThicknessUnit = TextDecorationUnit.FontRecommended
            };
            if (deco.CanFreeze) deco.Freeze();
            return deco;
        }

        public FlowDocument BuildDocument(
            string fullText,
            double? fontSize = null,
            double? lineHeightRatio = null,
            IReadOnlyDictionary<string, Brush>? colorMap = null,
            string? viewName = null)
        {
            double fs = fontSize ?? _textSettings.GetFontSize(viewName);
            double lhr = lineHeightRatio ?? DefaultLineHeightRatio;
            var colors = colorMap ?? DefaultColors;

            var doc = new FlowDocument
            {
                Foreground = Brushes.White,
                Background = (Brush)new BrushConverter().ConvertFrom("#181818"),
                PagePadding = new Thickness(10, 10, 0, 0),
                ColumnWidth = double.PositiveInfinity,
                FontSize = fs
            };

            var p = new Paragraph
            {
                Margin = new Thickness(0),
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = Math.Round(fs * lhr)
            };

            if (string.IsNullOrWhiteSpace(fullText))
            {
                var placeholderBrush = (colors.TryGetValue("lightgray", out var b) ? b : SystemColors.GrayTextBrush);
                var placeholder = (viewName != null && PlaceholderByView.TryGetValue(viewName, out var msg)) ? msg : DefaultPlaceholder;
                p.TextAlignment = TextAlignment.Center;
                p.Inlines.Clear();
                p.Inlines.Add(new Run(placeholder) { Foreground = placeholderBrush });
                doc.Blocks.Add(p);
                return doc;
            }

            string[] lines = fullText.Split('\n');
            bool endsWithNewline = fullText.EndsWith('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length > 0) AddColoredRuns(p, line, colors);

                bool isLast = i == lines.Length - 1;
                if (!isLast || (isLast && endsWithNewline)) p.Inlines.Add(new LineBreak());
            }

            doc.Blocks.Add(p);
            return doc;
        }

        private static void AddColoredRuns(Paragraph p, string line, IReadOnlyDictionary<string, Brush> colors)
        {
            int index = 0;
            while (index < line.Length)
            {
                int open = line.IndexOf('[', index);
                if (open < 0)
                {
                    foreach (var inline in SplitTextIntoInlines(line[index..], null))
                        p.Inlines.Add(inline);
                    break;
                }

                if (open > index)
                {
                    foreach (var inline in SplitTextIntoInlines(line[index..open], null))
                        p.Inlines.Add(inline);
                }

                int closeBracket = line.IndexOf(']', open + 1);
                if (closeBracket < 0)
                {
                    foreach (var inline in SplitTextIntoInlines(line[open..], null))
                        p.Inlines.Add(inline);
                    break;
                }

                string colorName = line[(open + 1)..closeBracket];
                string endTag = $"[/{colorName}]";
                int end = line.IndexOf(endTag, closeBracket + 1, StringComparison.Ordinal);
                if (end < 0)
                {
                    foreach (var inline in SplitTextIntoInlines(line[open..], null))
                        p.Inlines.Add(inline);
                    break;
                }

                string inner = line[(closeBracket + 1)..end];
                var brush = colors.TryGetValue(colorName.Trim(), out var b) ? b : Brushes.White;

                foreach (var inline in SplitTextIntoInlines(inner, brush))
                    p.Inlines.Add(inline);

                index = end + endTag.Length;
            }
        }

        private static Inline[] SplitTextIntoInlines(string text, Brush? foreground)
        {
            var parts = new List<Inline>();
            int pos = 0;

            while (pos < text.Length)
            {
                var md = MarkdownLinkRegex.Match(text, pos);
                var raw = UrlRegex.Match(text, pos);

                Match? next = null;
                if (md.Success && (!raw.Success || md.Index <= raw.Index)) next = md;
                else if (raw.Success) next = raw;

                if (next is null || !next.Success)
                {
                    if (pos < text.Length)
                    {
                        var tail = new Run(text[pos..]);
                        if (foreground != null) tail.Foreground = foreground;
                        parts.Add(tail);
                    }
                    break;
                }

                if (next.Index > pos)
                {
                    var lead = new Run(text[pos..next.Index]);
                    if (foreground != null) lead.Foreground = foreground;
                    parts.Add(lead);
                }

                if (ReferenceEquals(next, md))
                {
                    var label = md.Groups["label"].Value;
                    var url = md.Groups["url"].Value;
                    parts.Add(MakeLink(url, foreground, visibleText: label));
                    pos = md.Index + md.Length;
                }
                else
                {
                    var url = raw.Value;
                    parts.Add(MakeLink(url, foreground, visibleText: url));
                    pos = raw.Index + raw.Length;
                }
            }

            return parts.ToArray();
        }

        private static Hyperlink MakeLink(string url, Brush? foreground, string? visibleText = null)
        {
            var display = string.IsNullOrWhiteSpace(visibleText) ? url : visibleText;
            var run = new Run(display);
            if (foreground != null) run.Foreground = foreground;

            var baseTextBrush = foreground ?? (Brush)(Application.Current?.Resources["Brush.Text"] ?? Brushes.White);

            var link = new Hyperlink(run)
            {
                NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u : null,
                ToolTip = url,
                Foreground = baseTextBrush
            };

            link.TextDecorations = new TextDecorationCollection { BuildUnderlineForBrush(baseTextBrush) };

            link.RequestNavigate += (_, e) =>
            {
                try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
                e.Handled = true;
            };

            var menu = new ContextMenu();
            var open = new MenuItem { Header = "Open link" };
            open.Click += (_, __) => { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { } };
            menu.Items.Add(open);

            var copy = new MenuItem { Header = "Copy link address" };
            copy.Click += (_, __) => { try { Clipboard.SetText(url); } catch { } };
            menu.Items.Add(copy);

            try
            {
                if (Application.Current?.Resources["DSA.ContextMenu"] is Style cmStyle) menu.Style = cmStyle;
                if (Application.Current?.Resources["DSA.MenuItem"] is Style miStyle)
                {
                    open.Style = miStyle;
                    copy.Style = miStyle;
                }
            }
            catch { }

            link.ContextMenu = menu;
            link.Cursor = Cursors.Hand;

            return link;
        }
    }
}
