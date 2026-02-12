using System.Collections.Generic;

namespace FlowWatch.Models
{
    public class AppTrafficHistory
    {
        public List<DailyAppTrafficRecord> Records { get; set; } = new List<DailyAppTrafficRecord>();
    }
}
