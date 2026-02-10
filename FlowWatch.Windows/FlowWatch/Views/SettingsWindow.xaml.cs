using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
            _vm?.Cleanup();
            _allowClose = true;
            Close();
        }

        public new void Show()
        {
            base.Show();
            Activate();
        }
    }
}
