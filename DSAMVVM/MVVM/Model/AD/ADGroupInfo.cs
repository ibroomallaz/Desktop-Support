using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.Model
{
    public class ADGroupInfo
    {
        public bool Exists { get; set; }
        public List<string>? GroupMembers { get; set; }
        public int? MemberCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

