namespace FlowWatch.Models
{
    public class DailyTrafficRecord
    {
        public string Date { get; set; }        // "yyyy-MM-dd"
        public long DownloadBytes { get; set; } // 当天累计下载字节
        public long UploadBytes { get; set; }   // 当天累计上传字节
    }
}
