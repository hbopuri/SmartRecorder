using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
            catch (Exception ex)
            {
                ErrorLogger.LogError(null, "ConfigFile Not Found.");
                return null;
            }
        }

        public static Bitmap Stamp(Bitmap bitmap, DateTime date, string dateFormat)
        {
            string stampString;
            if (!string.IsNullOrEmpty(dateFormat))
            {
                stampString = date.ToString(dateFormat);
            }
            else
            {
                stampString = date.ToString();
            }

            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.FillRectangle(System.Drawing.Brushes.Black, 0, 0, 130, 20);

            graphics.DrawString(stampString, new Font("Arial", 8f), System.Drawing.Brushes.White, 2, 2);

            return bitmap;
        }

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

    }
}
