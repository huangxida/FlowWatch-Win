namespace FlowWatch.Models
{
    public class AppTrafficRecord
    {
        public string ProcessName { get; set; }
        public long DownloadBytes { get; set; }
        public long UploadBytes { get; set; }
    }
}
