using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Video.FFMPEG;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Smart.Core.Utilities;
using SmartRecorder.Helper;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SmartRecorder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private FilterInfoCollection _videoCaptureDevices;

        private VideoCaptureDevice _videoCaptureDevice = null;
        private VideoFileWriter _fileWriter = new VideoFileWriter();
        //private long? _startTick = null;
        //private Stopwatch _stopWatch;

        private string _fileBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigurationManager.AppSettings["VideoStoragePath"].ToString());
        private int? _quality;
        public MainWindow()
        {
            _quality = null;
            Init();
        }
        public MainWindow(int qualityId)
        {
            _quality = qualityId;
            Init();
        }

        void Init()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var productAppExeName = "SmartPileInspector.exe";
                var appConfigPath = CameraHelper.GetAllInstalledSoftware(productAppExeName);
                if (appConfigPath != null && !File.Exists(appConfigPath))
                    return;
                var appConfig = JObject.Parse(CryptAES.DecryptString("030DA110-A9D1-4258-940F-CDC904313F7F", File.ReadAllText(Path.Combine(Path.GetDirectoryName(appConfigPath),"App.config"))));
                string cameraName = appConfig["Camera"]["Name"].ToString();
                var sqlConnectionString = string.Format("Data Source={0};Initial Catalog={1};Integrated Security=True", appConfig["Database"]["Instance"].ToString(), appConfig["Database"]["Catalog"].ToString());
                string fileDirPath = null;
                string ssnFileName = "NoSession";
                _videoCaptureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (!Directory.Exists(_fileBasePath))
                {
                    ErrorLogger.LogError(null, "Video file path not configured! - " + _fileBasePath, false, false);
                    fileDirPath = Path.Combine(Path.GetTempPath(), ConfigurationManager.AppSettings["VideoStoragePath"].ToString());
                    if (!Directory.Exists(fileDirPath))
                        Directory.CreateDirectory(fileDirPath);
                }
                else
                {
                    fileDirPath = _fileBasePath;
                }

                SqlConnection conn = new SqlConnection(sqlConnectionString);
                try
                {
                    string selectSql = "select top 1 * from Sessions order by LastModifiedOnUtc desc ";
                    SqlCommand cmd = new SqlCommand(selectSql, conn);
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ssnFileName = reader["SessionKey"].ToString();
                            break;
                        }
                    }
                }
                catch
                {
                    ErrorLogger.LogError(null, "Sql Connection Failed! - " + _fileBasePath, false, false);
                }
                finally
                {
                    conn.Close();
                }

                if (_videoCaptureDevices.Count == 0)
                {
                    ErrorLogger.LogError(null, "No camera Attached.");
                }

                for (int i = 0; i < _videoCaptureDevices.Count; i++)
                {
                    if (_videoCaptureDevices[i].Name == cameraName)
                        _videoCaptureDevice = new VideoCaptureDevice(_videoCaptureDevices[i].MonikerString);
                }

                if (_videoCaptureDevice == null)
                {
                    ErrorLogger.LogError(null, "No camera with name " + cameraName + " attached!");
                }

                //if(_quality == null)
                //{
                //    _videoCaptureDevice.VideoResolution = _videoCaptureDevice.VideoCapabilities[0];
                //}
                //else
                //{
                //    _videoCaptureDevice.VideoResolution = _videoCaptureDevice.VideoCapabilities[_quality.Value];
                //}

                _videoCaptureDevice.VideoResolution = _videoCaptureDevice.VideoCapabilities.FirstOrDefault(o => o.FrameSize.Width == 320 && o.FrameSize.Height == 240);

                if (_videoCaptureDevice.VideoResolution == null)
                {
                    ErrorLogger.LogError(null, "The camera must support 0.08MP, " + cameraName, true, true);
                }

                _videoCaptureDevice.NewFrame += new NewFrameEventHandler(video_NewFrame);
                //_stopWatch = new Stopwatch();
                //_stopWatch.Start();
                _videoCaptureDevice.Start();


                ssnFileName = ssnFileName + "_";

                string filePath = Path.Combine(fileDirPath, ssnFileName + DateTimeOffset.Now.ToString("yyyyMMddHHmmss") + ".wmv");
                int bitCount = 100000;
                switch (_quality)
                {
                    case 1:
                        bitCount = 550000;
                        break;
                    case 2:
                        bitCount = 350000;
                        break;
                    case 3:
                        bitCount = 100000;
                        break;
                }
                _fileWriter.Open(filePath, _videoCaptureDevice.VideoResolution.FrameSize.Width, _videoCaptureDevice.VideoResolution.FrameSize.Height, 25, VideoCodec.WMV2, bitCount);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, null, true, true);
            }
        }

        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                Bitmap img = (Bitmap)eventArgs.Frame.Clone();
                MemoryStream ms = new MemoryStream();
                {
                    img.Save(ms, ImageFormat.Bmp);
                    ms.Seek(0, SeekOrigin.Begin);
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = ms;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    Dispatcher.BeginInvoke(new ThreadStart(delegate
                    {
                        imgVideoFrameHolder.Source = bitmapImage;
                    }));

                    _fileWriter.WriteVideoFrame(eventArgs.Frame);

                    //long currentTick = DateTime.Now.Ticks;
                    //_startTick = _startTick ?? currentTick;
                    //var frameOffset = new TimeSpan(currentTick - _startTick.Value);

                    //double elapsedTimeInSeconds = _stopWatch.ElapsedTicks / (double)Stopwatch.Frequency;
                    //double timeBetweenFramesInSeconds = 1.0 / 25;
                    //if (elapsedTimeInSeconds >= timeBetweenFramesInSeconds)
                    //{
                    //    _stopWatch.Restart();
                    //    _fileWriter.WriteVideoFrame(eventArgs.Frame, frameOffset);
                    //}
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_videoCaptureDevice != null)
                {
                    _videoCaptureDevice.Stop();
                    _fileWriter.Close();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex);
            }
        }
    }
}
