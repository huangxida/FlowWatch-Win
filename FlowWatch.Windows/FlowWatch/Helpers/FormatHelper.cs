namespace FlowWatch.Helpers
{
    public static class FormatHelper
    {
        private static readonly string[] SpeedUnits = { "KB/s", "MB/s", "GB/s" };
        private static readonly string[] UsageUnits = { "KB", "MB", "GB", "TB" };

        public static (string Num, string Unit) FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return ("0", "KB/s");

            double value = bytesPerSecond / 1024;
            int idx = 0;
            while (value >= 1024 && idx < SpeedUnits.Length - 1)
            {
                value /= 1024;
                idx++;
            }

            string num = value >= 10 ? value.ToString("F0") : value.ToString("F1");
            return (num, SpeedUnits[idx]);
        }

        public static (string Num, string Unit) FormatUsage(long bytes)
        {
            if (bytes < 1024)
                return ("0", "KB");

            double value = bytes / 1024.0;
            int idx = 0;
            while (value >= 1024 && idx < UsageUnits.Length - 1)
            {
                value /= 1024;
                idx++;
            }

            string num = value >= 10 ? value.ToString("F0") : value.ToString("F1");
            return (num, UsageUnits[idx]);
        }
    }
}
