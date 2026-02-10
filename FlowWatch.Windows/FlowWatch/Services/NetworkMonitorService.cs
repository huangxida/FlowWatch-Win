using System;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using FlowWatch.Helpers;
using FlowWatch.Models;

namespace FlowWatch.Services
{
    public class NetworkMonitorService
    {
        private static readonly Lazy<NetworkMonitorService> _instance = new Lazy<NetworkMonitorService>(() => new NetworkMonitorService());
        public static NetworkMonitorService Instance => _instance.Value;

        private DispatcherTimer _timer;
        private NetworkInterface _currentInterface;
        private TrafficUsage _traffic;
        private bool _firstPoll = true;
        private DateTime _trafficStartTime;

        public event Action<NetworkStats> StatsUpdated;

        public DateTime TrafficStartTime => _trafficStartTime;

        private NetworkMonitorService()
        {
            _trafficStartTime = DateTime.Now;
        }

        public void Start(int intervalMs)
        {
            Stop();
            _firstPoll = true;
            _currentInterface = NetworkInterfaceHelper.GetActiveInterface();

            if (_currentInterface != null && _traffic == null)
            {
                InitBaseline();
            }

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(300, intervalMs))
            };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
                _timer = null;
            }
        }

        public void Restart(int intervalMs)
        {
            Start(intervalMs);
        }

        public void ResetTraffic()
        {
            _trafficStartTime = DateTime.Now;
            _currentInterface = NetworkInterfaceHelper.GetActiveInterface();
            _traffic = null;
            _firstPoll = true;

            if (_currentInterface != null)
            {
                InitBaseline();
            }
        }

        private void InitBaseline()
        {
            try
            {
                var stats = _currentInterface.GetIPv4Statistics();
                _traffic = new TrafficUsage
                {
                    BaselineReceived = stats.BytesReceived,
                    BaselineSent = stats.BytesSent,
                    LastReceived = stats.BytesReceived,
                    LastSent = stats.BytesSent
                };
            }
            catch
            {
                _traffic = null;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_currentInterface == null)
            {
                _currentInterface = NetworkInterfaceHelper.GetActiveInterface();
                if (_currentInterface == null) return;
                InitBaseline();
            }

            try
            {
                // Check if interface is still up
                if (_currentInterface.OperationalStatus != OperationalStatus.Up)
                {
                    _currentInterface = NetworkInterfaceHelper.GetActiveInterface();
                    if (_currentInterface == null) return;
                    _traffic = null;
                    _firstPoll = true;
                    InitBaseline();
                }

                var ipStats = _currentInterface.GetIPv4Statistics();
                var currentRx = ipStats.BytesReceived;
                var currentTx = ipStats.BytesSent;

                if (_traffic == null)
                {
                    _traffic = new TrafficUsage
                    {
                        BaselineReceived = currentRx,
                        BaselineSent = currentTx,
                        LastReceived = currentRx,
                        LastSent = currentTx
                    };
                    return;
                }

                var intervalSec = _timer.Interval.TotalSeconds;

                double downSpeed = 0;
                double upSpeed = 0;

                if (!_firstPoll)
                {
                    var downBytes = Math.Max(0, currentRx - _traffic.LastReceived);
                    var upBytes = Math.Max(0, currentTx - _traffic.LastSent);
                    downSpeed = downBytes / intervalSec;
                    upSpeed = upBytes / intervalSec;
                }

                _firstPoll = false;

                var totalDown = Math.Max(0, currentRx - _traffic.BaselineReceived);
                var totalUp = Math.Max(0, currentTx - _traffic.BaselineSent);

                _traffic.LastReceived = currentRx;
                _traffic.LastSent = currentTx;

                StatsUpdated?.Invoke(new NetworkStats
                {
                    DownloadSpeed = downSpeed,
                    UploadSpeed = upSpeed,
                    TotalDownload = totalDown,
                    TotalUpload = totalUp,
                    InterfaceName = _currentInterface.Name
                });
            }
            catch
            {
                // Interface may have disconnected - try to pick a new one next tick
                _currentInterface = null;
            }
        }
    }
}
