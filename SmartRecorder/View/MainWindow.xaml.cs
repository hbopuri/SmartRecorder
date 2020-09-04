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
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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
        string _projectUid = Guid.Empty.ToString();
        SmartRecorderSettings settings = new SmartRecorderSettings();
        bool printTimeStamp = false;
        //private long? _startTick = null;
        //private Stopwatch _stopWatch;

        private string _fileBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigurationManager.AppSettings["VideoStoragePath"].ToString());
        public MainWindow()
        {
            Init();
        }
        public MainWindow(Guid projectUid)
        {
            _projectUid = projectUid.ToString();
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
                string configFile = $@"C:\Users\Public\Smart Structures\Smart Recorder\Settings\{_projectUid}.json";
                if (!File.Exists(configFile))
                {
                    ErrorLogger.LogError(null, "Config file not available!", false, false);
                    return;
                }
                string appconfig = File.ReadAllText(configFile);
                settings = JsonConvert.DeserializeObject<SmartRecorderSettings>(appconfig);

                printTimeStamp = settings.PrintTimeStamp;

                _videoCaptureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (_videoCaptureDevices.Count == 0)
                {
                    ErrorLogger.LogError(null, "No camera Attached.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(settings.OutputPath))
                {
                    ErrorLogger.LogError(null, "Video file path not configured!", false, false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(settings.Camera))
                {
                    ErrorLogger.LogError(null, "Camera not configured!", false, false);
                    return;
                }

                if (settings.SessionKey == Guid.Empty)
                {
                    ErrorLogger.LogError(null, "SessionKey not configured!", false, false);
                    return;
                }

                string ssnFileName = settings.SessionKey.ToString();
                try
                {
                    if (!Directory.Exists(settings.OutputPath))
                        Directory.CreateDirectory(settings.OutputPath);
                }
                catch (Exception ex)
                {
                    settings.OutputPath = _fileBasePath;
                    if (!Directory.Exists(settings.OutputPath))
                        Directory.CreateDirectory(settings.OutputPath);
                }
                for (int i = 0; i < _videoCaptureDevices.Count; i++)
                {
                    if (_videoCaptureDevices[i].Name == settings.Camera)
                        _videoCaptureDevice = new VideoCaptureDevice(_videoCaptureDevices[i].MonikerString);
                }

                if (_videoCaptureDevice == null)
                {
                    ErrorLogger.LogError(null, "No camera with name " + settings.Camera + " attached!");
                }

                _videoCaptureDevice.VideoResolution = _videoCaptureDevice.VideoCapabilities.FirstOrDefault(o => o.FrameSize.Width == 320 && o.FrameSize.Height == 240);

                if (_videoCaptureDevice.VideoResolution == null)
                {
                    ErrorLogger.LogError(null, "The camera must support 0.08MP, " + settings.Camera, true, true);
                }

                _videoCaptureDevice.NewFrame += new NewFrameEventHandler(video_NewFrame);
                _videoCaptureDevice.Start();

                var rsystemWorkArea = SystemParameters.WorkArea;

                switch (settings.Position)
                {
                    case SmartRecorderPosition.TopRight:
                        Left = rsystemWorkArea.Right - ActualWidth;
                        Top = 0;
                        break;
                    case SmartRecorderPosition.TopLeft:
                        Left = 0;
                        Top = 0;
                        break;
                    case SmartRecorderPosition.TopCenter:
                        Left = rsystemWorkArea.Right - (ActualWidth / 2) - (rsystemWorkArea.Right / 2);
                        Top = 0;
                        break;
                    case SmartRecorderPosition.BottomCenter:
                        Left = rsystemWorkArea.Right - (ActualWidth / 2) - (rsystemWorkArea.Right / 2);
                        Top = rsystemWorkArea.Bottom - ActualHeight;
                        break;
                    case SmartRecorderPosition.BottomLeft:
                        Left = 0;
                        Top = rsystemWorkArea.Bottom - ActualHeight;
                        break;
                    case SmartRecorderPosition.BottomRight:
                        Left = rsystemWorkArea.Right - ActualWidth;
                        Top = rsystemWorkArea.Bottom - ActualHeight;
                        break;
                }

                ssnFileName = ssnFileName + "_" + DateTimeOffset.Now.ToString("yyyyMMddHHmmss") + ".wmv";
                string filePath = Path.Combine(settings.OutputPath, ssnFileName);

                int bitCount = 100000;
                switch (settings.Quality)
                {
                    case SmartVideoQuality.Best:
                        bitCount = 550000;
                        break;
                    case SmartVideoQuality.Medium:
                        bitCount = 350000;
                        break;
                    case SmartVideoQuality.Low:
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
                if (printTimeStamp)
                    img = CameraHelper.Stamp(img, DateTime.Now, "MM/dd/yyyy HH:mm:ss");
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

                    _fileWriter.WriteVideoFrame(img);

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
