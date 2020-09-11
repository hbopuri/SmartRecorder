//using Accord.Audio;
//using Accord.Audio.Formats;
//using Accord.DirectSound;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Video.FFMPEG;
using NAudio.CoreAudioApi;
using NAudio.Wave;
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
using System.Threading.Tasks;
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

        static WaveIn _waveIn;
        private WaveFileWriter _waveWriter;
        string _projectUid = "3fef12bc-51f2-4944-aedc-9f9cefc209fa";
        SmartRecorderSettings settings = new SmartRecorderSettings();
        bool printTimeStamp = false;
        private string _fileBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigurationManager.AppSettings["VideoStoragePath"].ToString());
        private string _videoFilePath;
        private string _audioFilePath;
        private bool _isAudioRecording;
        private BufferedWaveProvider _bufferedWaveProvider;
        private Stopwatch _stopwatch;

        public long? StartTick { get; private set; }

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
                var configDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments).Replace("Documents", "Smart Structures");
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
                _isAudioRecording = settings.CaptureAudio;
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

                ssnFileName = ssnFileName + "_" + DateTimeOffset.Now.ToString("yyyyMMddHHmmss") + ".wmv";
                _videoFilePath = Path.Combine(settings.OutputPath, ssnFileName);
                _videoCaptureDevice.NewFrame += new NewFrameEventHandler(Video_NewFrame);
                _stopwatch = new Stopwatch();
                _stopwatch.Start();
                _videoCaptureDevice.Start();


                if (_isAudioRecording)
                {
                    _audioFilePath = Path.Combine(settings.OutputPath, ssnFileName.Replace("wmv", "mp3"));
                    _waveIn = new WaveIn();
                    _waveWriter = new WaveFileWriter(_audioFilePath, _waveIn.WaveFormat);
                    _waveIn.DataAvailable += new EventHandler<WaveInEventArgs>(SendCaptureAudio);
                    _bufferedWaveProvider = new BufferedWaveProvider(_waveIn.WaveFormat)
                    {
                        DiscardOnBufferOverflow = true
                    };
                    _waveIn.StartRecording();
                }

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

                _fileWriter.Open(_videoFilePath, _videoCaptureDevice.VideoResolution.FrameSize.Width, _videoCaptureDevice.VideoResolution.FrameSize.Height, 25, VideoCodec.WMV2, bitCount);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, null, true, true);
            }
        }

        private void SendCaptureAudio(object sender, WaveInEventArgs e)
        {
            try
            {
                foreach (byte _byte in e.Buffer)
                {
                    _waveWriter.WriteByte(_byte);
                }
            }
            catch (Exception ex)
            {

            }
        }

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
                    if (_videoCaptureDevice.IsRunning)
                    {
                        long currentTick = DateTime.Now.Ticks;
                        StartTick = StartTick ?? currentTick;
                        var frameOffset = new TimeSpan(currentTick - StartTick.Value);

                        double elapsedTimeInSeconds = _stopwatch.ElapsedTicks / (double)Stopwatch.Frequency;
                        double timeBetweenFramesInSeconds = 1.0 / 25;
                        if (elapsedTimeInSeconds >= timeBetweenFramesInSeconds)
                        {
                            _stopwatch.Restart();
                            _fileWriter.WriteVideoFrame(img, frameOffset);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(null, ex.Message, false, false);
            }
        }

        private void Mergefile(string wavefilepath, string videofilepath)
        {
            string Path_FFMPEG = Path.Combine(CameraHelper.AssemblyDirectory, "ffmpeg.exe");
            string Wavefile = wavefilepath;
            string video1 = videofilepath;
            //string file = @"D:\Developments\video\text.txt";

            System.Diagnostics.Process proc = new System.Diagnostics.Process();

            try
            {
                if (_audioFilePath != null)
                    proc.StartInfo.Arguments = string.Format("-i \"{0}\" -i \"{1}\" -shortest \"{2}\"", Wavefile, video1, video1.Replace(".wmv", ".mp4"));
                else
                    proc.StartInfo.Arguments = string.Format("-i \"{0}\" -shortest \"{1}\"", video1, video1.Replace(".wmv", ".mp4"));
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.FileName = Path_FFMPEG;
                proc.Start();
                
            }
            finally
            {
                proc.WaitForExit();
                proc.Close();

                if (_audioFilePath != null)
                    File.Delete(wavefilepath);
                File.Delete(videofilepath);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_videoCaptureDevice != null)
                {
                    _videoCaptureDevice.Stop();
                    _videoCaptureDevice.WaitForStop();
                    _fileWriter.Close();

                    if (_isAudioRecording)
                    {
                        _waveIn.StopRecording();
                        _waveIn.Dispose();
                        _waveIn = null;
                        _waveWriter.Close();
                        _waveWriter = null;
                    }

                    Mergefile(_audioFilePath, _videoFilePath);
                }
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex);
            }
        }
    }
}