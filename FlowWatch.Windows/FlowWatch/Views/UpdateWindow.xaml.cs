using System;
using System.Threading;
using System.Windows;
using FlowWatch.Models;
using FlowWatch.Services;

namespace FlowWatch.Views
{
    public partial class UpdateWindow : Window
    {
        private UpdateInfo _updateInfo;
        private CancellationTokenSource _downloadCts;
        private bool _isDownloading;

        public UpdateWindow()
        {
            InitializeComponent();
        }

        public void SetUpdateInfo(UpdateInfo info)
        {
            _updateInfo = info;

            var loc = LocalizationService.Instance;
            HeaderText.Text = loc.Get("Update.Header");
            CurrentVersionText.Text = UpdateService.Instance.GetCurrentVersion().ToString(3);
            LatestVersionText.Text = info.Version.ToString(3);
            PublishedAtText.Text = info.PublishedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                ? loc.Get("Update.NoNotes")
                : info.ReleaseNotes;
        }

        private void OnSkipClick(object sender, RoutedEventArgs e)
        {
            if (_updateInfo != null)
            {
                SettingsService.Instance.Update(s => s.SkippedVersion = _updateInfo.TagName);
            }
            Close();
        }

        private void OnLaterClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnUpdateClick(object sender, RoutedEventArgs e)
        {
            if (_isDownloading || _updateInfo == null) return;
            _isDownloading = true;

            // Update UI state
            SkipButton.IsEnabled = false;
            LaterButton.IsEnabled = false;
            UpdateButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;

            _downloadCts = new CancellationTokenSource();

            UpdateService.Instance.DownloadProgressChanged += OnDownloadProgress;

            try
            {
                var zipPath = await UpdateService.Instance.DownloadUpdateAsync(_updateInfo, _downloadCts.Token);

                // Launch update script and exit
                UpdateService.Instance.LaunchUpdateAndExit(zipPath);

                // Exit application
                var app = Application.Current as App;
                app?.ExitForUpdate();
            }
            catch (OperationCanceledException)
            {
                LogService.Info("下载已取消");
                ResetDownloadState();
            }
            catch (Exception ex)
            {
                LogService.Error("下载更新失败", ex);
                MessageBox.Show(
                    LocalizationService.Instance.Get("Update.DownloadFailed"),
                    "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Warning);
                ResetDownloadState();
            }
            finally
            {
                UpdateService.Instance.DownloadProgressChanged -= OnDownloadProgress;
            }
        }

        private void OnDownloadProgress(double progress)
        {
            Dispatcher.Invoke(() =>
            {
                var percent = (int)(progress * 100);
                DownloadProgress.Value = percent;
                ProgressPercentText.Text = $"{percent}%";
            });
        }

        private void ResetDownloadState()
        {
            _isDownloading = false;
            SkipButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            UpdateButton.IsEnabled = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosed(EventArgs e)
        {
            _downloadCts?.Cancel();
            _downloadCts?.Dispose();
            base.OnClosed(e);
        }
    }
}
