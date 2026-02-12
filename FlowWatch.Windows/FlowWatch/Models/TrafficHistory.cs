using System.Collections.Generic;

namespace FlowWatch.Models
{
    public class TrafficHistory
    {
        public List<DailyTrafficRecord> Records { get; set; } = new List<DailyTrafficRecord>();
    }
}
