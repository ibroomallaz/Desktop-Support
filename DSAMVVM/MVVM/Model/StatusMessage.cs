using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.Model
{
    public class StatusMessage
    {
        public string Message { get; set; }
        public int Priority { get; set; } = 0;
        public bool Sticky { get; set; } = false;

        public StatusMessage(string message, int priority = 0, bool sticky = false)
        {
            Message = message;
            Priority = priority;
            Sticky = sticky;
        }
    }


}
