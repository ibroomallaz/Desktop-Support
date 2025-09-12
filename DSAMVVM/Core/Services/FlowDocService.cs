using DSAMVVM.Core.Interfaces;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

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

        private static readonly IReadOnlyDictionary<string, Brush> DefaultColors =
            new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase)
            {
                ["red"] = (Brush)new BrushConverter().ConvertFrom("#E57373"),
                ["green"] = (Brush)new BrushConverter().ConvertFrom("#66BB6A"),
                ["yellow"] = (Brush)new BrushConverter().ConvertFrom("#FFD54F"),
                ["cyan"] = (Brush)new BrushConverter().ConvertFrom("#81D4FA"),
                ["white"] = (Brush)new BrushConverter().ConvertFrom("#D0D3D6"),
                ["lightgray"] = (Brush)new BrushConverter().ConvertFrom("#B0B3B8")
            };

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

            // --- Placeholder when no content yet ---
            if (string.IsNullOrWhiteSpace(fullText))
            {
                var placeholderBrush =
                    (colors.TryGetValue("lightgray", out var b) ? b : SystemColors.GrayTextBrush);

                var placeholder = (viewName != null && PlaceholderByView.TryGetValue(viewName, out var msg))
                    ? msg
                    : DefaultPlaceholder;

                p.TextAlignment = TextAlignment.Center;
                p.Inlines.Clear();
                p.Inlines.Add(new Run(placeholder) { Foreground = placeholderBrush });

                doc.Blocks.Add(p);
                return doc;
            }

            // --------------------------------------

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
                if (open < 0) { p.Inlines.Add(new Run(line[index..])); break; }

                if (open > index) p.Inlines.Add(new Run(line[index..open]));

                int closeBracket = line.IndexOf(']', open + 1);
                if (closeBracket < 0) { p.Inlines.Add(new Run(line[open..])); break; }

                string colorName = line[(open + 1)..closeBracket];
                string endTag = $"[/{colorName}]";
                int end = line.IndexOf(endTag, closeBracket + 1, StringComparison.Ordinal);
                if (end < 0) { p.Inlines.Add(new Run(line[open..])); break; }

                string inner = line[(closeBracket + 1)..end];
                var run = new Run(inner)
                {
                    Foreground = colors.TryGetValue(colorName.Trim(), out var brush) ? brush : Brushes.White
                };
                p.Inlines.Add(run);

                index = end + endTag.Length;
            }
        }
    }
}
