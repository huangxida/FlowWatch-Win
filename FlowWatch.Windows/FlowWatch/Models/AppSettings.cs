namespace FlowWatch.Models
{
    public class AppSettings
    {
        public int RefreshInterval { get; set; } = 1000;
        public bool LockOnTop { get; set; } = true;
        public bool PinToDesktop { get; set; } = false;
        public string FontFamily { get; set; } = "Segoe UI, Microsoft YaHei, sans-serif";
        public int FontSize { get; set; } = 18;
        public string Layout { get; set; } = "horizontal";
        public int SpeedColorMaxMbps { get; set; } = 100;
        public string DisplayMode { get; set; } = "speed";
        public double? OverlayX { get; set; }
        public double? OverlayY { get; set; }
        public bool AutoLaunch { get; set; } = true;
        public double? AppTrafficWindowWidth { get; set; }
        public double? AppTrafficWindowHeight { get; set; }
        public string Language { get; set; } = "auto";
    }
}
