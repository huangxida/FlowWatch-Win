namespace FlowWatch.Helpers
{
    public static class FormatHelper
    {
        private static readonly string[] SpeedUnits = { "KB/s", "MB/s", "GB/s" };
        private static readonly string[] UsageUnits = { "KB", "MB", "GB", "TB" };

        public static (string Num, string Unit) FormatSpeed(double bytesPerSecond, bool alwaysShowDecimal = false)
        {
            if (bytesPerSecond < 1024)
                return (FormatZero(alwaysShowDecimal), "KB/s");

            double value = bytesPerSecond / 1024;
            int idx = 0;
            while (value >= 1024 && idx < SpeedUnits.Length - 1)
            {
                value /= 1024;
                idx++;
            }

            string num = FormatNumber(value, alwaysShowDecimal);
            return (num, SpeedUnits[idx]);
        }

        public static (string Num, string Unit) FormatUsage(long bytes, bool alwaysShowDecimal = false)
        {
            if (bytes < 1024)
                return (FormatZero(alwaysShowDecimal), "KB");

            double value = bytes / 1024.0;
            int idx = 0;
            while (value >= 1024 && idx < UsageUnits.Length - 1)
            {
                value /= 1024;
                idx++;
            }

            string num = FormatNumber(value, alwaysShowDecimal);
            return (num, UsageUnits[idx]);
        }

        private static string FormatZero(bool alwaysShowDecimal)
        {
            return alwaysShowDecimal ? "0.0" : "0";
        }

        private static string FormatNumber(double value, bool alwaysShowDecimal)
        {
            return alwaysShowDecimal || value < 10
                ? value.ToString("F1")
                : value.ToString("F0");
        }
    }
}
