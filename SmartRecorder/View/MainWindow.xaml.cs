using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Video.FFMPEG;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Smart.Core.Utilities;
using System;
using System.Configuration;
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
        private string _fileBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigurationManager.AppSettings["AppDataSubFolderPath"].ToString());
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
                string cameraName = JObject.Parse(CryptAES.DecryptString("030DA110-A9D1-4258-940F-CDC904313F7F", File.ReadAllText(ConfigurationManager.AppSettings["SPIConfigFilePath"].ToString())))["SmartRecorderCamera"].ToString();
                string fileDirPath = null;
                string ssnFileName = "NoSession";
                _videoCaptureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (!Directory.Exists(_fileBasePath))
                {
                    ErrorLogger.LogError(null, "Video file path not configured! - " + _fileBasePath, false, false);
                    fileDirPath = Path.Combine(Path.GetTempPath(), ConfigurationManager.AppSettings["AppDataSubFolderPath"].ToString());
                    if (!Directory.Exists(fileDirPath))
                        Directory.CreateDirectory(fileDirPath);
                }
                else
                {
                    fileDirPath = _fileBasePath;
                }

                try
                {
                    if (Directory.Exists(_fileBasePath))
                        ssnFileName = Path.GetFileNameWithoutExtension(
                            new DirectoryInfo(_fileBasePath)
                            .GetFiles("*.ssn")
                            .OrderByDescending(o => o.LastWriteTime)
                            .FirstOrDefault()?.Name) ?? "NoSession";
                }
                catch
                {
                    ErrorLogger.LogError(null, ".ssn file not exists! - " + _fileBasePath, false, false);
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
