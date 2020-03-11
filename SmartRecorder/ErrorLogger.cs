using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SmartRecorder
{
  public static  class ErrorLogger
    {
        public static void LogError(Exception ex, string nonExceptionMessage = null, bool messageRequired = true, bool shutDownRequired = true)
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "Smart Structures", "Smart Recorder");
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            var logPath = Path.Combine(directoryPath, "Log.txt");
            if (!File.Exists(logPath))
            {
                File.Create(logPath).Dispose();
            }
            using (StreamWriter writer = File.AppendText(logPath))
            {
                if (ex != null)
                {
                    if (messageRequired)
                        MessageBox.Show(DateTime.UtcNow + " - " + "exception" + " - " + ex.Message, "Smart Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
                    writer.WriteLine(DateTime.UtcNow + " - " + "exception" + " - " + ex.Message);
                }
                if (!string.IsNullOrEmpty(nonExceptionMessage))
                {
                    if (messageRequired)
                        MessageBox.Show(DateTime.UtcNow + " - " + "non-exception" + " - " + nonExceptionMessage, "Smart Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
                    writer.WriteLine(DateTime.UtcNow + " - " + "non-exception" + " - " + nonExceptionMessage);
                }
            }
            if (shutDownRequired)
            {
                Process.GetCurrentProcess().Kill();
            }
        }
    }
}
