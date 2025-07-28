using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.Model
{
    public class ADComputerInfo
    {
        public string Name { get; set; } = string.Empty;

        public string? OUs { get; set; }
        public string? Description { get; set; }
        public string? OperatingSystem { get; set; }

        public bool Exists { get; set; }
        public bool? Enabled { get; set; }
        public bool IsHybridGroupMember { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
