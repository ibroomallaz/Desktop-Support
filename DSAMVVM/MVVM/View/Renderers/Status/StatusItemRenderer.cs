using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;

namespace DSAMVVM.MVVM.View.Renderers.Status
{
    public static class StatusItemRenderer
    {
        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.RegisterAttached(
                "Item",
                typeof(StatusItem),
                typeof(StatusItemRenderer),
                new PropertyMetadata(null, OnChanged));

        public static void SetItem(DependencyObject obj, StatusItem value) => obj.SetValue(ItemProperty, value);
        public static StatusItem GetItem(DependencyObject obj) => (StatusItem)obj.GetValue(ItemProperty);

        static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock tb) return;
            tb.Inlines.Clear();

            var item = e.NewValue as StatusItem;
            if (item == null || item.Spans == null || item.Spans.Count == 0) return;

            IReadOnlyList<StatusSpan> spans = item.Spans;

            for (int i = 0; i < spans.Count; i++)
            {
                var s = spans[i];
                Inline inline;

                // hyperlink for external links or internal commands
                if (s.ExternalLink != null || !string.IsNullOrEmpty(s.Command))
                {
                    var hl = new Hyperlink(new Run(s.Text));

                    if (s.ExternalLink != null)
                    {
                        hl.NavigateUri = s.ExternalLink;
                        hl.RequestNavigate += (_, args) =>
                        {
                            try { Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
                            args.Handled = true;
                        };
                    }

                    if (!string.IsNullOrEmpty(s.Command))
                        hl.Click += (_, __) => UiNotify.TryExecute(s.Command!);

                    inline = hl;
                }
                else
                {
                    inline = new Run(s.Text);
                }

                if (s.Bold) inline.FontWeight = FontWeights.Bold;
                if (s.Underline) inline.TextDecorations = TextDecorations.Underline;
                if (!string.IsNullOrWhiteSpace(s.Color))
                {
                    try { inline.Foreground = (Brush)new BrushConverter().ConvertFromString(s.Color)!; } catch { /* ignore bad colors */ }
                }
                if (!string.IsNullOrWhiteSpace(s.Tooltip))
                    ToolTipService.SetToolTip(inline, s.Tooltip);

                tb.Inlines.Add(inline);
                if (i != spans.Count - 1) tb.Inlines.Add(new Run(" "));
            }
        }
    }
}
