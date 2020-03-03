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
            try
            {
                InitializeComponent();
                Loaded += MainWindow_Loaded;
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex);
            }
        }
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string cameraName = ConfigurationManager.AppSettings["CameraName"].ToString();
                string fileDirPath = null;
                _videoCaptureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (!Directory.Exists(_fileBasePath))
                {
                    WriteErrorLog(null, "Video file path not configured! - " + _fileBasePath);
                    return;
                }

                try
                {
                    fileDirPath = new DirectoryInfo(_fileBasePath).GetDirectories().OrderByDescending(d => d.LastWriteTimeUtc).First().FullName;
                }
                catch
                {
                    WriteErrorLog(null, "There is no subfolder inside appdata folder! - " + _fileBasePath);
                    return;
                }

                if (_videoCaptureDevices.Count == 0)
                {
                    WriteErrorLog(null, "No camera Attached.");
                    return;
                }

                for (int i = 0; i < _videoCaptureDevices.Count; i++)
                {
                    if (_videoCaptureDevices[i].Name == cameraName)
                        _videoCaptureDevice = new VideoCaptureDevice(_videoCaptureDevices[i].MonikerString);
                }

                if (_videoCaptureDevice == null)
                {
                    WriteErrorLog(null, "No camera with name " + cameraName + " attached!");
                    return;
                }

                _videoCaptureDevice.NewFrame += new NewFrameEventHandler(video_NewFrame);
                _recording = true;
                _stopWatch = new Stopwatch();
                _stopWatch.Start();
                _videoCaptureDevice.Start();


                string filePath = Path.Combine(fileDirPath, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() + ".avi");
                _fileWriter.Open(filePath, 1280, 720, 25, VideoCodec.Default, 5000000);
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex);
                return;
            }
        }

        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                Image img = (Bitmap)eventArgs.Frame.Clone();
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
                            try
                            {
                                _fileWriter.WriteVideoFrame(eventArgs.Frame, frameOffset);
                            }
                            catch (Exception ex)
                            {
                                WriteErrorLog(ex);
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex);
                return;
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
                return;
            }
        }

        private void WriteErrorLog(Exception ex, string nonExceptionMessage = null)
        {
            if (!File.Exists("Log.txt"))
            {
                File.Create("Log.txt").Dispose();
            }


            using (StreamWriter writer = File.AppendText("Log.txt"))
            {
                if (ex != null)
                {
                    MessageBox.Show(DateTime.UtcNow + " - " + "exception" + " - " + ex.Message, "Smart Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
                    writer.WriteLine(DateTime.UtcNow + " - " + "exception" + " - " + ex.Message);

                }
                if (!string.IsNullOrEmpty(nonExceptionMessage))
                {
                    MessageBox.Show(DateTime.UtcNow + " - " + "exception" + " - " + nonExceptionMessage, "Smart Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
                    writer.WriteLine(DateTime.UtcNow + " - " + "non-exception" + " - " + nonExceptionMessage);
                }

            }

            Application.Current.Shutdown(110);
        }
    }

}
