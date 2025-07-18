using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DSAMVVM.MVVM.Model
{
    public class StatusMessage
    {
        public UIElement? Content { get; set; }
        public int Priority { get; set; } = 0;
        public bool Sticky { get; set; } = false;

        public StatusMessage(UIElement content, int priority = 0, bool sticky = false)
        {
            Content = content;
            Priority = priority;
            Sticky = sticky;
        }
    }

}
/* Priority message levels (WiP):
 * 0 - Standard message
 * 1 - 
 * 2 - 
 * 3 - Failure Message
 * 4 - Failure Overcome
 * 5 - Error
*/

/* 
 * Status Message Usage:

Plain:
 *      StatusBar.Report(StatusMessageFactory.Plain("Text here."));
 * 
 * 
External:
 *
    StatusBar.Report(StatusMessageFactory.CreateRichExternalMessage(
    "An update is available. {0} or {1}.",
    new[]
    {
        StatusMessageFactory.Link("Download here", new Uri("https://example.com/download")),
        StatusMessageFactory.Link("View changelog", new Uri("https://example.com/changelog"))
    },
    priority: 2,
    sticky: true
    ));


Internal with formatting:
 *
        StatusBar.Report(StatusMessageFactory.CreateRichInternalMessage(
            "{0} {1} or {2}.",
            new[]
            {
            StatusMessageFactory.BoldColored("Warning:", Brushes.OrangeRed),
            StatusMessageFactory.Colored("Network error", Brushes.Red),
            StatusMessageFactory.ActionLink("Try again", () => RefreshDepartments())
            },
            priority: 3,
            sticky: true
            ));

*/