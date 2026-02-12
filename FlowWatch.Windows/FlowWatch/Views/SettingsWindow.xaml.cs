using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FlowWatch.Services;
using FlowWatch.ViewModels;

namespace FlowWatch.Views
{
    public partial class SettingsWindow : Window
    {
        private SettingsViewModel _vm;
        private bool _allowClose;

        public SettingsWindow()
        {
            InitializeComponent();
            _vm = (SettingsViewModel)DataContext;
            _vm.PropertyChanged += OnVmPropertyChanged;
            SettingsService.Instance.SettingsChanged += OnSettingsServiceChanged;
            UpdateSkippedUI();
            UpdateCheckButtonText();
        }

        private void OnSettingsServiceChanged()
        {
            Dispatcher.Invoke(() =>
            {
                _vm.RefreshSkippedVersion();
                UpdateSkippedUI();
            });
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.HasSkippedVersion) ||
                e.PropertyName == nameof(SettingsViewModel.SkippedVersion))
            {
                UpdateSkippedUI();
            }
            else if (e.PropertyName == nameof(SettingsViewModel.IsCheckingUpdate))
            {
                UpdateCheckButtonText();
            }
        }

        private void UpdateSkippedUI()
        {
            if (_vm.HasSkippedVersion)
            {
                var loc = LocalizationService.Instance;
                SkippedPanel.Visibility = Visibility.Visible;
                SkippedText.Text = loc.Get("Settings.SkippedVersionLabel") + _vm.SkippedVersion;
            }
            else
            {
                SkippedPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateCheckButtonText()
        {
            var loc = LocalizationService.Instance;
            CheckUpdateButton.Content = _vm.IsCheckingUpdate
                ? loc.Get("Update.Checking")
                : loc.Get("Tray.CheckUpdate");
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void OnFontPresetChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            var item = combo?.SelectedItem as ComboBoxItem;
            var tag = item?.Tag as string;
            if (!string.IsNullOrEmpty(tag) && _vm != null)
            {
                _vm.FontFamily = tag;
            }
        }

        public void ForceClose()
        {
            SettingsService.Instance.SettingsChanged -= OnSettingsServiceChanged;
            _vm?.Cleanup();
            _allowClose = true;
            Close();
        }

        public new void Show()
        {
            _vm.RefreshSkippedVersion();
            UpdateSkippedUI();
            UpdateCheckButtonText();
            base.Show();
            Activate();
        }
    }
}
