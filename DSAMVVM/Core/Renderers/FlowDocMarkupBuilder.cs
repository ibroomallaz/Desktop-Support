using System.Text;

namespace DSAMVVM.Core.Renderers
{
    public class FlowDocMarkupBuilder
    {
        private readonly StringBuilder _sb = new();

        private void AppendTight(string text) => _sb.Append(text + "\n");

        public void AddRaw(string? text) => AppendTight(text ?? string.Empty);

        public void AddTitle(string? title)
        {
            if (!string.IsNullOrWhiteSpace(title)) AppendTight($"[yellow]{title}[/yellow]");
        }

        public void AddHeader(string headerText) =>
            AppendTight($"\n[cyan]────────── {headerText} ──────────[/cyan]");

        public void AddError(string message) => AppendTight($"[red]{message}[/red]");

        public void AddSuccess(string message) => AppendTight($"[green]{message}[/green]");

        public void AddDim(string message) => AppendTight($"[gray]{message}[/gray]");

        public void AddLabelValue(string label, string? value, bool treatEmptyAsNone = true)
        {
            var finalValue = value;
            if (string.IsNullOrWhiteSpace(finalValue) && treatEmptyAsNone) finalValue = "None";
            if (finalValue != null) AppendTight($"[cyan]{label}[/cyan][red]{finalValue}[/red]");
        }

        public void AddLink(string label, string linkText, string targetUrl) =>
            AppendTight($"[cyan]{label}[/cyan][red][{linkText}]({targetUrl})[/red]");

        public void AddListItem(string text) =>
                    AppendTight($"[lightgray]    • {text}[/lightgray]");

        public void AddLabeledListItem(string label, string value) =>
            AppendTight($"[lightgray]    • [/lightgray][cyan]{label}[/cyan][red]{value}[/red]");

        public void AddListLink(string label, string linkText, string targetUrl) =>
            AppendTight($"[lightgray]    • [/lightgray][cyan]{label}[/cyan][red][{linkText}]({targetUrl})[/red]");

        public override string ToString() => _sb.ToString();
    }
}