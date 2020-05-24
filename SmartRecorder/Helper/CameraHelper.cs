using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartRecorder.Helper
{
    public static class CameraHelper
    {
        public static string GetAllInstalledSoftware(string exeName)
        {
            try
            {
                RegistryKey objValue = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}");
                var path = objValue.GetValue("path")?.ToString();
                if (!string.IsNullOrWhiteSpace(path))
                    return System.IO.Path.Combine(path, "app.config");
                return path;
            }
            catch(Exception ex)
            {
                ErrorLogger.LogError(null, "ConfigFile Not Found.");
                return null;
            }
        }

    }
}
