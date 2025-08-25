
namespace DSAMVVM.MVVM.Model.AD
{
    public class ADGroupInfo
    {
        public bool Exists { get; set; }
        public List<string>? GroupMembers { get; set; }
        public int? MemberCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

