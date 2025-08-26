using DSAMVVM.Core.Interfaces;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DSAMVVM.Core.Services
{
    public sealed class FlowDocService : IFlowDocService
    {
        private readonly IOutputTextSettingsProvider _textSettings;
        private const double DefaultLineHeightRatio = 1.08;

        private static readonly IReadOnlyDictionary<string, Brush> DefaultColors =
            new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase)
            {

                ["red"] = (Brush)new BrushConverter().ConvertFrom("#E57373"),   // softer red
                ["green"] = (Brush)new BrushConverter().ConvertFrom("#66BB6A"),   // softer green
                ["yellow"] = (Brush)new BrushConverter().ConvertFrom("#FFD54F"),   // soft amber
                ["cyan"] = (Brush)new BrushConverter().ConvertFrom("#81D4FA"),   // soft cyan
                ["white"] = (Brush)new BrushConverter().ConvertFrom("#D0D3D6"),   // muted “white”
                ["lightgray"] = (Brush)new BrushConverter().ConvertFrom("#B0B3B8")   //light gray
            };

        public FlowDocService(IOutputTextSettingsProvider textSettings)
        {
            _textSettings = textSettings ?? throw new ArgumentNullException(nameof(textSettings));
        }

        public FlowDocument BuildDocument(
            string fullText,
            double? fontSize = null,
            double? lineHeightRatio = null,
            IReadOnlyDictionary<string, Brush>? colorMap = null,
            string? viewName = null)
        {
            double fs = fontSize ?? _textSettings.GetFontSize(viewName); // null => default/global
            double lhr = lineHeightRatio ?? DefaultLineHeightRatio;
            var colors = colorMap ?? DefaultColors;

            var doc = new FlowDocument
            {
                Foreground = Brushes.White,
                Background = (Brush)new BrushConverter().ConvertFrom("#181818"),
                PagePadding = new Thickness(10,10,0,0),
                ColumnWidth = double.PositiveInfinity,
                FontSize = fs
            };

            var p = new Paragraph
            {
                Margin = new Thickness(0),
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = Math.Round(fs * lhr)
            };

            if (!string.IsNullOrEmpty(fullText))
            {
                string[] lines = fullText.Split('\n');
                bool endsWithNewline = fullText.EndsWith("\n", StringComparison.Ordinal);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Length > 0) AddColoredRuns(p, line, colors);

                    bool isLast = i == lines.Length - 1;
                    if (!isLast || (isLast && endsWithNewline)) p.Inlines.Add(new LineBreak());
                }
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
                if (open < 0) { p.Inlines.Add(new Run(line.Substring(index))); break; }

                if (open > index) p.Inlines.Add(new Run(line.Substring(index, open - index)));

                int closeBracket = line.IndexOf(']', open + 1);
                if (closeBracket < 0) { p.Inlines.Add(new Run(line.Substring(open))); break; }

                string colorName = line.Substring(open + 1, closeBracket - (open + 1));
                string endTag = $"[/{colorName}]";
                int end = line.IndexOf(endTag, closeBracket + 1, StringComparison.Ordinal);
                if (end < 0) { p.Inlines.Add(new Run(line.Substring(open))); break; }

                string inner = line.Substring(closeBracket + 1, end - (closeBracket + 1));
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
