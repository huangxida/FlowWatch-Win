using System;
using System.Windows.Media;

namespace FlowWatch.Helpers
{
    public static class ColorGradient
    {
        // White (#ffffff) -> Yellow (#ffd166) -> Red (#ff3b30)
        private static readonly byte[] White = { 255, 255, 255 };
        private static readonly byte[] Yellow = { 255, 209, 102 };
        private static readonly byte[] Red = { 255, 59, 48 };

        public static Color GetSpeedColor(double bytesPerSecond, int maxMbps)
        {
            // Convert bytes/s to Mbps (decimal)
            double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
            double max = Math.Max(1, maxMbps);
            double t = Math.Min(1.0, Math.Max(0.0, mbps / max));

            const double mid = 0.5;
            byte r, g, b;

            if (t <= mid)
            {
                double ratio = t / mid;
                r = Lerp(White[0], Yellow[0], ratio);
                g = Lerp(White[1], Yellow[1], ratio);
                b = Lerp(White[2], Yellow[2], ratio);
            }
            else
            {
                double ratio = (t - mid) / (1.0 - mid);
                r = Lerp(Yellow[0], Red[0], ratio);
                g = Lerp(Yellow[1], Red[1], ratio);
                b = Lerp(Yellow[2], Red[2], ratio);
            }

            return Color.FromRgb(r, g, b);
        }

        public static SolidColorBrush GetSpeedBrush(double bytesPerSecond, int maxMbps)
        {
            var color = GetSpeedColor(bytesPerSecond, maxMbps);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static byte Lerp(byte a, byte b, double t)
        {
            return (byte)Math.Round(a + (b - a) * t);
        }
    }
}
