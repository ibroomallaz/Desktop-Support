namespace DSAMVVM.MVVM.Model.AD
{
    public class ADUserInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public string? DepartmentNumber { get; set; }
        public string? DisplayName { get; set; }
        public string? EduAffiliation { get; set; }
        public string? License { get; set; }
        public string? RawLicense { get; set; }
        public string? Division { get; set; }
        public string? ErrorMessage { get; set; }

        public bool Exists { get; set; }
        public bool? Enabled { get; set; }
        public bool? Locked { get; set; }
        public bool? MimGroupExists { get; set; }

        public List<string>? MimGroupsList { get; set; }
    }
}
