using System.ComponentModel;
using System.Windows;
using FlowWatch.Services;
using FlowWatch.ViewModels;

namespace FlowWatch.Views
{
    public partial class AppTrafficWindow : Window
    {
        private AppTrafficViewModel _vm;
        private bool _allowClose;

        public AppTrafficWindow()
        {
            InitializeComponent();
            _vm = (AppTrafficViewModel)DataContext;

            // 恢复上次窗口尺寸
            var settings = SettingsService.Instance.Settings;
            if (settings.AppTrafficWindowWidth.HasValue)
                Width = settings.AppTrafficWindowWidth.Value;
            if (settings.AppTrafficWindowHeight.HasValue)
                Height = settings.AppTrafficWindowHeight.Value;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                SaveWindowSize();
                _vm?.StopRealtimeTimer();
                Hide();
            }
        }

        public void ForceClose()
        {
            SaveWindowSize();
            _vm?.StopRealtimeTimer();
            _allowClose = true;
            Close();
        }

        public new void Show()
        {
            // 先刷新 ETW 增量数据，消除延迟感
            ProcessTrafficService.Instance.FlushDeltas();
            _vm?.StartRealtimeTimerIfNeeded();
            _vm?.Refresh();
            base.Show();
            Activate();
        }

        private void SaveWindowSize()
        {
            SettingsService.Instance.Update(s =>
            {
                s.AppTrafficWindowWidth = Width;
                s.AppTrafficWindowHeight = Height;
            });
        }
    }
}
