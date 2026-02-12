namespace FlowWatch.Models
{
    /// <summary>
    /// 单日流量记录模型
    /// </summary>
    public class DailyTrafficRecord
    {
        /// <summary>
        /// 日期，ISO 8601 格式：yyyy-MM-dd
        /// </summary>
        public string Date { get; set; }

        /// <summary>
        /// 当天累计下载字节数
        /// </summary>
        public long DownloadBytes { get; set; }

        /// <summary>
        /// 当天累计上传字节数
        /// </summary>
        public long UploadBytes { get; set; }
    }
}
