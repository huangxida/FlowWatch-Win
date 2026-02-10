namespace FlowWatch.Models
{
    public class NetworkStats
    {
        public double DownloadSpeed { get; set; }
        public double UploadSpeed { get; set; }
        public long TotalDownload { get; set; }
        public long TotalUpload { get; set; }
        public string InterfaceName { get; set; }
    }
}
