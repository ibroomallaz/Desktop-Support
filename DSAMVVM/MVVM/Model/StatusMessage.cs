using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DSAMVVM.MVVM.Model
{
    public class StatusMessage
    {
        public string? Message { get; }
        public TextBlock? RichContent { get; }
        public int Priority { get; }
        public bool Sticky { get; }
        public string? Key { get; }

        // Constructor for plain text
        public StatusMessage(string message, int priority = 0, bool sticky = false, string? key = null)
        {
            Message = message;
            Priority = priority;
            Sticky = sticky;
            Key = key; //Allows for typing for clearing of messages caused by failures
        }

        // Constructor for rich TextBlock content
        public StatusMessage(TextBlock richContent, int priority = 0, bool sticky = false, string? key = null)
        {
            RichContent = richContent;
            Priority = priority;
            Sticky = sticky;
            Key = key;
        }
    }
}

/* Priority message levels (WiP):
 * 0 - Standard message
 * 1 - 
 * 2 - 
 * 3 - Failure Message
 * 4 - Higher Error

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