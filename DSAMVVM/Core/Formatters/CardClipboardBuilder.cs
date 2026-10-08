using System.Net;
using System.Text;
using DSAMVVM.Core.Utilities;

namespace DSAMVVM.Core.Formatters
{
    public class CardClipboardBuilder
    {
        private readonly StringBuilder _html = new();
        private readonly StringBuilder _plain = new();
        private readonly List<string> _currentBadgeHtml = [];
        private readonly List<string> _currentBadgePlain = [];
        private bool _isInsideFieldsBlock;

        public CardClipboardBuilder()
        {
            _html.Append("<div style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; font-size: 12px; line-height: 1.5;\">");
        }

        public CardClipboardBuilder AddHeader(string title, string? subtitle = null)
        {
            FlushBlock();

            string fullText = string.IsNullOrWhiteSpace(subtitle)
                ? title
                : $"{title} - ({subtitle})";

            _html.Append($"<p style=\"font-size: 14px; margin: 0 0 4px 0;\">{WebUtility.HtmlEncode(fullText)}</p>");
            _plain.AppendLine(fullText);
            return this;
        }

        public CardClipboardBuilder AddBadge(string label, bool isBold = false)
        {
            if (string.IsNullOrWhiteSpace(label)) return this;

            string encodedLabel = WebUtility.HtmlEncode(label);
            string htmlInner = isBold ? $"<strong>{encodedLabel}</strong>" : encodedLabel;

            _currentBadgeHtml.Add($"[{htmlInner}]");
            _currentBadgePlain.Add($"[{label}]");
            return this;
        }

        public CardClipboardBuilder AddWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return this;

            FlushBlock();

            _html.Append("<p style=\"margin: 4px 0 6px 0; font-size: 11px;\">" +
                         $"<strong>[WARNING]</strong>&nbsp;<u>{WebUtility.HtmlEncode(message)}</u></p>");
            _plain.AppendLine($"[WARNING] {message}");
            return this;
        }

        public CardClipboardBuilder AddField(string label, string? value, bool isBoldValue = false, bool isItalic = false)
        {
            if (string.IsNullOrWhiteSpace(value)) return this;

            EnsureFieldsBlockOpen();

            string encodedValue = WebUtility.HtmlEncode(value);
            if (isBoldValue) encodedValue = $"<strong>{encodedValue}</strong>";
            if (isItalic) encodedValue = $"<em>{encodedValue}</em>";

            _html.Append($"<strong>{WebUtility.HtmlEncode(label)}:</strong>&nbsp; {encodedValue}<br/>");
            _plain.AppendLine($"{label}: {value}");
            return this;
        }

        public CardClipboardBuilder AddCodeBlock(string title, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return this;

            FlushBlock();

            _html.Append($"<p style=\"margin: 4px 0 2px 0;\"><strong>{WebUtility.HtmlEncode(title)}:</strong></p>" +
                         $"<pre style=\"margin: 2px 0; font-family: Cascadia Code, Consolas, monospace; font-size: 11px;\">{WebUtility.HtmlEncode(content)}</pre>");
            _plain.AppendLine($"{title}:");
            _plain.AppendLine(content);
            return this;
        }

        public CardClipboardBuilder AddList(string title, IEnumerable<string>? items)
        {
            var itemList = items?.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            if (itemList == null || itemList.Count == 0) return this;

            FlushBlock();

            _html.Append($"<p style=\"margin: 4px 0 2px 0;\"><strong>{WebUtility.HtmlEncode(title)} ({itemList.Count}):</strong></p>" +
                         "<ul style=\"margin: 2px 0; padding-left: 20px; font-size: 11px;\">");
            _plain.AppendLine($"{title} ({itemList.Count}):");

            foreach (var item in itemList)
            {
                _html.Append($"<li>{WebUtility.HtmlEncode(item)}</li>");
                _plain.AppendLine($"  - {item}");
            }

            _html.Append("</ul>");
            return this;
        }

        public (string Html, string PlainText) Build()
        {
            FlushBlock();

            string fullHtml = _html.ToString().TrimEnd() + "</div>";
            string fullPlain = _plain.ToString().TrimEnd();

            return (fullHtml, fullPlain);
        }

        public bool CopyToClipboard()
        {
            (string html, string plain) = Build();
            return ClipboardHelper.CopyRichAndPlainText(html, plain);
        }

        private void FlushBlock()
        {
            CloseFieldsBlockIfNeeded();
            FlushBadges();
        }

        private void EnsureFieldsBlockOpen()
        {
            FlushBadges();
            if (_isInsideFieldsBlock) return;
            _html.Append("<p style=\"margin: 0;\">");
            _isInsideFieldsBlock = true;
        }

        private void CloseFieldsBlockIfNeeded()
        {
            if (!_isInsideFieldsBlock) return;
            _html.Append("</p>");
            _isInsideFieldsBlock = false;
        }

        private void FlushBadges()
        {
            if (_currentBadgeHtml.Count <= 0) return;
            _html.Append($"<p style=\"margin: 0 0 6px 0;\">{string.Join("&nbsp;&nbsp;", _currentBadgeHtml)}</p>");
            _plain.AppendLine(string.Join("  ", _currentBadgePlain));
            _currentBadgeHtml.Clear();
            _currentBadgePlain.Clear();
        }
    }
}
