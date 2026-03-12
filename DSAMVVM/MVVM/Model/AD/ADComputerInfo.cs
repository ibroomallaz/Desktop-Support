namespace DSAMVVM.MVVM.Model.AD
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
        public string? LastLogonDate { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
