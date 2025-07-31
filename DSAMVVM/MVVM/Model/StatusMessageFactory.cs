using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DSAMVVM.MVVM.Model
{
    public static partial class StatusMessageFactory
    {
        // Plain text status message
        public static StatusMessage Plain(string message, int priority = 0, bool sticky = false, string? key = null)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            };

            return new StatusMessage(textBlock, priority, sticky, key);
        }

        // ✅ Success (Green)
        public static StatusMessage Success(string message, int priority = 0, bool sticky = false, string? key = null)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Green,
                TextWrapping = TextWrapping.Wrap
            };

            return new StatusMessage(textBlock, priority, sticky, key);
        }

        // ✅ Error (Red)
        public static StatusMessage Error(string message, int priority = 2, bool sticky = true, string? key = null)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Red,
                TextWrapping = TextWrapping.Wrap
            };

            return new StatusMessage(textBlock, priority, sticky, key);
        }

        // ✅ Warning (Orange)
        public static StatusMessage Warning(string message, int priority = 1, bool sticky = false, string? key = null)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Orange,
                TextWrapping = TextWrapping.Wrap
            };

            return new StatusMessage(textBlock, priority, sticky, key);
        }

        // ✅ Info (Gray)
        public static StatusMessage Info(string message, int priority = 0, bool sticky = false, string? key = null)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
            };

            return new StatusMessage(textBlock, priority, sticky, key);
        }

        // External hyperlink
        public static Inline Link(string text, Uri uri)
        {
            var link = new Hyperlink(new Run(text))
            {
                NavigateUri = uri,
                Foreground = Brushes.DodgerBlue,
                TextDecorations = TextDecorations.Underline
            };

            link.RequestNavigate += (s, e) =>
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
                {
                    UseShellExecute = true
                });
                e.Handled = true;
            };

            return link;
        }

        // Internal action hyperlink
        public static Inline ActionLink(string text, Action onClick)
        {
            var link = new Hyperlink(new Run(text))
            {
                Foreground = Brushes.DodgerBlue,
                TextDecorations = TextDecorations.Underline
            };

            link.RequestNavigate += (s, e) =>
            {
                onClick?.Invoke();
                e.Handled = true;
            };

            return link;
        }

        // Rich message with external links
        public static StatusMessage CreateRichExternalMessage(string format, Inline[] inlines, int priority = 0, bool sticky = false, string? key = null)
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            };

            foreach (var inline in FormatWithInlines(format, inlines))
                tb.Inlines.Add(inline);

            return new StatusMessage(tb, priority, sticky, key);
        }

        // Alias for internal rich messages
        public static StatusMessage CreateRichInternalMessage(string format, Inline[] inlines, int priority = 0, bool sticky = false, string? key = null)
        {
            return CreateRichExternalMessage(format, inlines, priority, sticky, key);
        }

        private static IEnumerable<Inline> FormatWithInlines(string format, Inline[] args)
        {
            var regex = StatusRegex();
            int last = 0;

            foreach (Match match in regex.Matches(format))
            {
                if (match.Index > last)
                    yield return new Run(format[last..match.Index]);

                int index = int.Parse(match.Groups[1].Value);
                if (index >= 0 && index < args.Length)
                    yield return args[index];

                last = match.Index + match.Length;
            }

            if (last < format.Length)
                yield return new Run(format[last..]);
        }

        // Text formatting helpers
        public static Inline Bold(string text)
            => new Bold(new Run(text));

        public static Inline Italic(string text)
            => new Italic(new Run(text));

        public static Inline Underlined(string text)
            => new Run(text) { TextDecorations = TextDecorations.Underline };

        public static Inline Colored(string text, Brush color)
            => new Run(text) { Foreground = color };

        public static Inline BoldColored(string text, Brush color)
            => new Span(new Run(text)) { Foreground = color, FontWeight = FontWeights.Bold };

        public static Inline BoldUnderlined(string text)
            => new Span(new Run(text)) { FontWeight = FontWeights.Bold, TextDecorations = TextDecorations.Underline };

        [GeneratedRegex(@"\{(\d+)\}")]
        private static partial Regex StatusRegex();
    }
}
