using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

namespace FlowWatch.Services
{
    public static class AutoLaunchService
    {
        private const string TaskName = "FlowWatch_AutoLaunch";
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "FlowWatch";

        public static void SetAutoLaunch(bool enabled)
        {
            // 先清理旧的注册表 Run 键（从之前版本迁移）
            CleanLegacyRunKey();

            try
            {
                if (enabled)
                    CreateScheduledTask();
                else
                    DeleteScheduledTask();
            }
            catch (Exception ex)
            {
                LogService.Error("设置任务计划程序自启动失败", ex);
            }
        }

        public static bool IsAutoLaunchEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{TaskName}\" /FO CSV /NH",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit(5000);
                    return proc.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void CreateScheduledTask()
        {
            var exePath = Assembly.GetExecutingAssembly().Location;

            // 先删除旧任务（如果存在）
            DeleteScheduledTask();

            // 创建以最高权限运行的登录触发任务
            var args = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST /F /DELAY 0000:05";
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var proc = Process.Start(psi))
            {
                proc.WaitForExit(10000);
                if (proc.ExitCode != 0)
                {
                    var error = proc.StandardError.ReadToEnd();
                    LogService.Error($"创建计划任务失败 (exit={proc.ExitCode}): {error}");
                }
                else
                {
                    LogService.Info("计划任务创建成功");
                }
            }
        }

        private static void DeleteScheduledTask()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Delete /TN \"{TaskName}\" /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var proc = Process.Start(psi))
            {
                proc.WaitForExit(5000);
            }
        }

        private static void CleanLegacyRunKey()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key?.GetValue(AppName) != null)
                    {
                        key.DeleteValue(AppName, false);
                        LogService.Info("已清理旧版注册表 Run 键自启动项");
                    }
                }
            }
            catch
            {
                // 清理失败不影响主流程
            }
        }
    }
}
