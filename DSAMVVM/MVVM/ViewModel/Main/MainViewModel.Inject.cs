using DSAMVVM.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public partial class MainViewModel
    {
        private T InjectStatus<T>(Func<IStatusReporter, T> factory) where T : class
        {
            return factory(StatusBar);
        }
    }
}
