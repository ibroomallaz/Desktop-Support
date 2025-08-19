using System.Windows.Documents;
using System.Windows.Media;

namespace DSAMVVM.Core.Interfaces
{
    public interface IFlowDocService
    {
        FlowDocument BuildDocument(
            string fullText,
            double? fontSize = null,
            double? lineHeightRatio = null,
            IReadOnlyDictionary<string, Brush>? colorMap = null,
            string? viewName = null);
    }
}
