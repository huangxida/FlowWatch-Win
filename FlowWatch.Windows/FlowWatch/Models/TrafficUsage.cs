namespace FlowWatch.Models
{
    public class TrafficUsage
    {
        public long BaselineReceived { get; set; }
        public long BaselineSent { get; set; }
        public long LastReceived { get; set; }
        public long LastSent { get; set; }
    }
}
