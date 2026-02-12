using System.ComponentModel;
using System.Windows;
using FlowWatch.ViewModels;

namespace FlowWatch.Views
{
    public partial class StatisticsWindow : Window
    {
        private StatisticsViewModel _vm;
        private bool _allowClose;

        public StatisticsWindow()
        {
            InitializeComponent();
            _vm = (StatisticsViewModel)DataContext;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
            }
        }

        public void ForceClose()
        {
            _allowClose = true;
            Close();
        }

        public new void Show()
        {
            _vm?.Refresh();
            base.Show();
            Activate();
        }
    }
}
