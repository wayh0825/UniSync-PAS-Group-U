namespace UniSync.Web.ViewModels
{
    public class DatabaseLogItem
    {
        public string Name { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Ref { get; set; } = string.Empty;
    }
}
