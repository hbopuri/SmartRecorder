using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Video.FFMPEG;
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
        private bool _recording = false;
        private long? _startTick = null;
        private Stopwatch _stopWatch;
        private string _fileBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigurationManager.AppSettings["AppDataSubFolderPath"].ToString());

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string cameraName = ConfigurationManager.AppSettings["CameraName"].ToString();
                string fileDirPath = null;
                string ssnFileName = "NoSession";
                _videoCaptureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (!Directory.Exists(_fileBasePath))
                {
                    WriteErrorLog(null, "Video file path not configured! - " + _fileBasePath, false, false);
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
                    if(Directory.Exists(_fileBasePath))
                        ssnFileName = Path.GetFileNameWithoutExtension(
                            new DirectoryInfo(_fileBasePath)
                            .GetFiles("*.ssn")
                            .OrderByDescending(o => o.LastWriteTime)
                            .FirstOrDefault()?.Name) ?? "NoSession";
                }
                catch
                {
                    WriteErrorLog(null, ".ssn file not exists! - " + _fileBasePath, false, false);
                }

                if (_videoCaptureDevices.Count == 0)
                {
                    WriteErrorLog(null, "No camera Attached.");
                }

                for (int i = 0; i < _videoCaptureDevices.Count; i++)
                {
                    if (_videoCaptureDevices[i].Name == cameraName)
                        _videoCaptureDevice = new VideoCaptureDevice(_videoCaptureDevices[i].MonikerString);
                }

                if (_videoCaptureDevice == null)
                {
                    WriteErrorLog(null, "No camera with name " + cameraName + " attached!");
                }
                _videoCaptureDevice.VideoResolution = _videoCaptureDevice.VideoCapabilities[8];
                _videoCaptureDevice.NewFrame += new NewFrameEventHandler(video_NewFrame);
                _recording = true;
                _stopWatch = new Stopwatch();
                _stopWatch.Start();
                _videoCaptureDevice.Start();


                ssnFileName = ssnFileName + "_";

                string filePath = Path.Combine(fileDirPath, ssnFileName + DateTimeOffset.Now.ToString("yyyyMMddHHmmss") + ".avi");
                _fileWriter.Open(filePath, _videoCaptureDevice.VideoResolution.FrameSize.Width,
                    _videoCaptureDevice.VideoResolution.FrameSize.Height,
                    _videoCaptureDevice.VideoResolution.AverageFrameRate, VideoCodec.MSMPEG4v3, 30);
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex);
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

                    if (_recording)
                    {
                        long currentTick = DateTime.Now.Ticks;
                        _startTick = _startTick ?? currentTick;
                        var frameOffset = new TimeSpan(currentTick - _startTick.Value);

                        double elapsedTimeInSeconds = _stopWatch.ElapsedTicks / (double)Stopwatch.Frequency;
                        double timeBetweenFramesInSeconds = 1.0 / 25;
                        if (elapsedTimeInSeconds >= timeBetweenFramesInSeconds)
                        {
                            _stopWatch.Restart();
                            _fileWriter.WriteVideoFrame(eventArgs.Frame, frameOffset);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex);                
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
                WriteErrorLog(ex);
            }
        }

        private void WriteErrorLog(Exception ex, string nonExceptionMessage = null, bool messageRequired = true, bool shutDownRequired = true)
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
                        MessageBox.Show(DateTime.UtcNow + " - " + "exception" + " - " + nonExceptionMessage, "Smart Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
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
