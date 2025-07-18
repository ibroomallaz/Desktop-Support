using DSAMVVM.MVVM.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.Core
{
    public interface IStatusReporter
    {
        void Report(StatusMessage message, int timeoutMs = 5000);
    }
}
