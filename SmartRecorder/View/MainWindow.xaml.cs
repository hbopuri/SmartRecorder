//using Accord.Audio;
//using Accord.Audio.Formats;
//using Accord.DirectSound;
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
        private FilterInfoCollection _audioCapturingDevices;
        private VideoCaptureDevice _videoCaptureDevice = null;
        private VideoFileWriter _fileWriter = new VideoFileWriter();
        //string _projectUid = Guid.Empty.ToString();
        string _projectUid = "3fef12bc-51f2-4944-aedc-9f9cefc209fa";
        SmartRecorderSettings settings = new SmartRecorderSettings();
        bool printTimeStamp = false;
        private string _fileBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigurationManager.AppSettings["VideoStoragePath"].ToString());
        //private AudioCaptureDevice _audioCaptureDevice;
        //private float[] _audioBuffer;
        //private MemoryStream _audioStream;
        //private WaveEncoder _audioEncoder;

        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int MciSendString(string lpstrCommand, string lpstrReturnString, int uReturnLength, int hwndCallback);

        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", ExactSpelling = true, CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int Record(string lpstrCommand, string lpstrReturnString, int uReturnLength, int hwndCallback);

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
                var configDirectory  = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments).Replace("Public Documents", "Smart Structures");
                configDirectory = Path.Combine(configDirectory, "Smart Recorder", "Settings");
                string configFile = Path.Combine(configDirectory, _projectUid + ".json");
                if (!File.Exists(configFile))
                {
                    ErrorLogger.LogError(null, "Config file not available!", true, true);
                    return;
                }
                string appconfig = File.ReadAllText(configFile);
                settings = JsonConvert.DeserializeObject<SmartRecorderSettings>(appconfig);

                printTimeStamp = settings.PrintTimeStamp;

                _videoCaptureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                _audioCapturingDevices = new FilterInfoCollection(FilterCategory.AudioInputDevice);

                if (_videoCaptureDevices.Count == 0)
                {
                    ErrorLogger.LogError(null, "No camera Attached.");
                    return;
                }

                if (_audioCapturingDevices.Count == 0 && settings.CaptureAudio)
                {
                    ErrorLogger.LogError(null, "No Mic Attached.");
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

                //for (int i = 0; i < _audioCapturingDevices.Count; i++)
                //{
                //    if (_audioCapturingDevices[i].Name == settings.Mic)
                //    {
                //        _audioCaptureDevice = new AudioCaptureDevice()
                //        {
                //            DesiredFrameSize = 4096,
                //            SampleRate = 22050,
                //            Format = SampleFormat.Format16Bit
                //        };
                //        _audioCaptureDevice.NewFrame += _audioCaptureDevice_NewFrame;
                //        _audioBuffer = new float[_audioCaptureDevice.DesiredFrameSize];
                //        _audioStream = new MemoryStream();
                //        _audioEncoder = new WaveEncoder(_audioStream);
                //        _audioCaptureDevice.Start();
                //        break;
                //    }
                //}

                if (_videoCaptureDevice == null)
                {
                    ErrorLogger.LogError(null, "No camera with name " + settings.Camera + " attached!");
                }

                _videoCaptureDevice.VideoResolution = _videoCaptureDevice.VideoCapabilities.FirstOrDefault(o => o.FrameSize.Width == 320 && o.FrameSize.Height == 240);

                if (_videoCaptureDevice.VideoResolution == null)
                {
                    ErrorLogger.LogError(null, "The camera must support 0.08MP, " + settings.Camera, true, true);
                }

                _videoCaptureDevice.NewFrame += new NewFrameEventHandler(Video_NewFrame);
                Record("open new Type waveaudio Alias recsound", "", 0, 0);
                Record("record recsound", "", 0, 0);
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

        //private void _audioCaptureDevice_NewFrame(object sender, Accord.Audio.NewFrameEventArgs e)
        //{
        //    throw new NotImplementedException();
        //}

        private void Video_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
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
                    // merge audio with video
                    //https://stackoverflow.com/a/53605040/942855

                    Record("save recsound d:\\mic.wav", "", 0, 0);
                    Record("close recsound", "", 0, 0);
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