using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SmartRecorder
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void App_Startup(object sender, StartupEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            if (e.Args.Count() > 0)
            {
                Guid resolutionId;
                if (Guid.TryParse(e.Args[0], out resolutionId))
                {
                    mainWindow = new MainWindow(resolutionId);
                }
                else
                {
                    ErrorLogger.LogError(null, "Resolution Id for launching SmartRecorder application is not a valied integer!", true, true);
                }
            }
            mainWindow.Show();
        }
    }
}
