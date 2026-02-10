using System;
using Microsoft.Win32;

namespace FlowWatch.Services
{
    public static class AutoLaunchService
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "FlowWatch";

        public static void SetAutoLaunch(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;

                    if (enabled)
                    {
                        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue(AppName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch
            {
                // Registry access may fail in some environments
            }
        }

        public static bool IsAutoLaunchEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return key?.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
