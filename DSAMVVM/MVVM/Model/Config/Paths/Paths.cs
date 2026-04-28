namespace DSAMVVM.MVVM.Model.Config.Paths
{
    public sealed class Paths
    {
        public string DataDir { get; set; } = "data";
        public DataLocation DepartmentData { get; set; } = new();
        public DataLocation LinksData { get; set; } = new();

        public void NormalizeAll()
        {
            DepartmentData?.Normalize();
            LinksData?.Normalize();
        }
    }
}