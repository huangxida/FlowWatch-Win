using System.Collections.Generic;

namespace FlowWatch.Models
{
    public class DailyAppTrafficRecord
    {
        public string Date { get; set; }
        public List<AppTrafficRecord> Apps { get; set; } = new List<AppTrafficRecord>();
    }
}
