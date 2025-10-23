namespace DSAMVVM.Core.Models
{
    using DSAMVVM.Core.Enums;
    using System.Windows.Input;

    public sealed class NavItem
    {
        public string Title { get; init; } = "";
        public string Glyph { get; init; } = "";   // MDL2 char, e.g. "\uE80F"
        public AppView View { get; init; }
        public ICommand? Command { get; init; }
    }
}
