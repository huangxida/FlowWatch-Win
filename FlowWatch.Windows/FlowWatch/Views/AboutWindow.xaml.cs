using System.Diagnostics;
using System.Windows;
using FlowWatch.Services;

namespace FlowWatch.Views
{
    public partial class AboutWindow : Window
    {
        private const string GitHubUrl = "https://github.com/huangxida/FlowWatch-Win";

        public AboutWindow()
        {
            InitializeComponent();
            var version = UpdateService.Instance.GetCurrentVersion().ToString(3);
            var loc = LocalizationService.Instance;
            VersionText.Text = loc.Format("About.VersionFormat", version);
        }

        private void OnGitHubClick(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubUrl,
                UseShellExecute = true
            });
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
