using System.Collections.Generic;

namespace FlowWatch.Models
{
    /// <summary>
    /// 流量历史数据容器
    /// </summary>
    public class TrafficHistory
    {
        /// <summary>
        /// 每日流量记录列表，按时间顺序排列
        /// </summary>
        public List<DailyTrafficRecord> Records { get; set; } = new List<DailyTrafficRecord>();
    }
}
