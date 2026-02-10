namespace FlowWatch.Helpers
{
    public static class FormatHelper
    {
        private static readonly string[] SpeedUnits = { "B/s", "KB/s", "MB/s", "GB/s" };
        private static readonly string[] UsageUnits = { "B", "KB", "MB", "GB", "TB" };

        public static (string Num, string Unit) FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0)
                return ("0", "B/s");

            double value = bytesPerSecond;
            int idx = 0;
            while (value >= 1024 && idx < SpeedUnits.Length - 1)
            {
                value /= 1024;
                idx++;
            }

            string num = (value >= 10 || idx == 0) ? value.ToString("F0") : value.ToString("F1");
            return (num, SpeedUnits[idx]);
        }

        public static (string Num, string Unit) FormatUsage(long bytes)
        {
            if (bytes <= 0)
                return ("0", "B");

            double value = bytes;
            int idx = 0;
            while (value >= 1024 && idx < UsageUnits.Length - 1)
            {
                value /= 1024;
                idx++;
            }

            string num = (value >= 10 || idx == 0) ? value.ToString("F0") : value.ToString("F1");
            return (num, UsageUnits[idx]);
        }
    }
}
