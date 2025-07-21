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
    public static class StatusMessageFactory
    {

        // Creates a plain text status message.

        public static StatusMessage Plain(string message, int priority = 0, bool sticky = false, string? key = null)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            };

            return new StatusMessage(textBlock, priority, sticky, key);
        }



        // Creates a hyperlink that opens an external URI.

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


        // Creates a hyperlink that runs an internal Action when clicked.

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


        // Creates a rich message with format text and external links (e.g., "Click {0} or {1}").

        public static StatusMessage CreateRichExternalMessage(
     string format,
     Inline[] inlines,
     int priority = 0,
     bool sticky = false,
     string? key = null)
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            };

            foreach (var inline in FormatWithInlines(format, inlines))
                tb.Inlines.Add(inline);

            return new StatusMessage(tb, priority, sticky, key);
        }



        // Creates a rich message with format text and internal method links.

        public static StatusMessage CreateRichInternalMessage(
            string format,
            Inline[] inlines,
            int priority = 0,
            bool sticky = false,
            string? key = null)
        {
            return CreateRichExternalMessage(format, inlines, priority, sticky, key);
        }


        private static IEnumerable<Inline> FormatWithInlines(string format, Inline[] args)
        {
            var regex = new Regex(@"\{(\d+)\}");
            int last = 0;

            foreach (Match match in regex.Matches(format))
            {
                if (match.Index > last)
                    yield return new Run(format.Substring(last, match.Index - last));

                int index = int.Parse(match.Groups[1].Value);
                if (index >= 0 && index < args.Length)
                    yield return args[index];

                last = match.Index + match.Length;
            }

            if (last < format.Length)
                yield return new Run(format.Substring(last));
        }

        /* Stylization for later usage if needed*/
        // Bold text
        public static Inline Bold(string text)
        {
            return new Bold(new Run(text));
        }

        // Italic text
        public static Inline Italic(string text)
        {
            return new Italic(new Run(text));
        }

        // Underlined text
        public static Inline Underlined(string text)
        {
            return new Run(text)
            {
                TextDecorations = TextDecorations.Underline
            };
        }

        // Colored text
        public static Inline Colored(string text, Brush color)
        {
            return new Run(text)
            {
                Foreground = color
            };
        }

        // Bold + Colored text
        public static Inline BoldColored(string text, Brush color)
        {
            return new Span(new Run(text))
            {
                Foreground = color,
                FontWeight = FontWeights.Bold
            };
        }

        // Bold + Underlined text
        public static Inline BoldUnderlined(string text)
        {
            return new Span(new Run(text))
            {
                FontWeight = FontWeights.Bold,
                TextDecorations = TextDecorations.Underline
            };
        }
    }
}
