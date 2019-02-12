using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Path = System.IO.Path;
using Garlic;
using VideoConverterLib;

namespace AsycnVideoConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Google Analytics
        private const string DOMAIN = "http://www.visualport.kr";
        private const string GACODE = "UA-99223726-1";
        private static AnalyticsSession Session { get; } = new AnalyticsSession(DOMAIN, GACODE);
        private readonly IAnalyticsPageViewRequest pageAnalytics;
        #endregion

        private BackgroundWorker converter;
        private int sourceWidth;
        private int sourceHeight;
        private FFMpegConverter ffMpeg;

        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        private string sourceFile;
        public string SourceFile
        {
            get { return sourceFile; }
            set
            {
                sourceFile = value;
                RaisePropertyChanged("SourceFile");
            }
        }

        private string resultFile;
        public string ResultFile
        {
            get { return resultFile; }
            set
            {
                resultFile = value;
                RaisePropertyChanged("ResultFile");
            }
        }

        private double progress;
        public double Progress
        {
            get { return progress; }
            set
            {
                progress = value;
                RaisePropertyChanged("Progress");
            }
        }

        private double minimum;
        public double Minimum
        {
            get { return minimum; }
            set
            {
                minimum = value;
                RaisePropertyChanged("Minimum");
            }
        }

        private double maximum;
        public double Maximum
        {
            get { return maximum; }
            set
            {
                maximum = value;
                RaisePropertyChanged("Maximum");
            }
        }

        private Visibility convertButtonVisible;
        public Visibility ConvertButtonVisible
        {
            get { return convertButtonVisible; }
            set
            {
                convertButtonVisible = value;
                RaisePropertyChanged("ConvertButtonVisible");
                ProgressBarVisible = value == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            }
        }

        private Visibility progressBarVisible;
        public Visibility ProgressBarVisible
        {
            get { return progressBarVisible; }
            set
            {
                progressBarVisible = value;
                RaisePropertyChanged("ProgressBarVisible");
            }
        }
        
        public MainWindow()
        {
            InitializeComponent();
            InitConverter();
            DataContext = this;

            pageAnalytics = Session.CreatePageViewRequest(this.Title, "");
            pageAnalytics.Track(this);
        }        

        private void InitConverter()
        {
            converter = new BackgroundWorker();
            converter.DoWork += Converter_DoWork;
            converter.RunWorkerCompleted += Converter_RunWorkerCompleted;

            InitProgressValue();

        }

        private void InitProgressValue()
        {
            Minimum = 0;
            Progress = 0;
            Maximum = 100;
        }

        private void GetSourceResolution()
        {
            sourceWidth = 0;
            sourceHeight = 0;

            try
            {
                var ffprobe = new FFProbe();
                var info = ffprobe.GetMediaInfo(sourceFile);
                sourceWidth = info.Streams[0].Width;
                sourceHeight = info.Streams[0].Height;

                if (sourceWidth > 0 && sourceWidth % 2 != 0) sourceWidth -= 1;
                if (sourceHeight > 0 && sourceHeight % 2 != 0) sourceHeight -= 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot Get Source Video Resolution", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GetResultFileName()
        {
            string dir = Path.GetDirectoryName(sourceFile);
            string ori = Path.GetFileNameWithoutExtension(sourceFile);
            ResultFile = Path.Combine(dir, ori + ".mp4");
        }

        private void FfMpeg_ConvertProgress(object sender, ConvertProgressEventArgs e)
        {
            var p = e.Processed.TotalMilliseconds;
            var t = e.TotalDuration.TotalMilliseconds;
            var r = p / t * 100;
            Progress = r <= 100 ? r : 100;

            if (p == t)
            {
                Progress = 0;
            }
        }

        private void Converter_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ConvertButtonVisible = Visibility.Visible;
            Dispatcher.Invoke(() => { NotifyUser(); });
        }

        private void NotifyUser()
        {
            pageAnalytics.SendEvent("Process", "Finished Converting", "", "");

            string msg = "Converted file : " + ResultFile + "\nWould you like to play it?";
            if (MessageBox.Show(this, msg, "Notification", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                Process.Start(ResultFile);
            }
        }

        private void Converter_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                ffMpeg = new FFMpegConverter();
                ffMpeg.ConvertProgress += FfMpeg_ConvertProgress;
                var setting = new ConvertSettings();
                setting.SetVideoFrameSize(sourceWidth, sourceHeight);
                ffMpeg.ConvertMedia(sourceFile, Format.avi, ResultFile, Format.mp4, setting);
                //ffMpeg.GetVideoThumbnail(sourceFile, "output.jpg");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot Convert Video", MessageBoxButton.OK, MessageBoxImage.Error);
            }            
        }

        private void btBrowse_Click(object sender, RoutedEventArgs e)
        {
            pageAnalytics.SendEvent("Button", "Browse Video Source", "", "");
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Video files|*.avi;*.flv;*.mov";
            if (ofd.ShowDialog() == true)
            {
                SourceFile = ofd.FileName;
            }
        }

        private void btConvert_Click(object sender, RoutedEventArgs e)
        {
            pageAnalytics.SendEvent("Button", "Convert Video", "", "");
            
            if (!string.IsNullOrEmpty(sourceFile))
            {
                GetSourceResolution();
                GetResultFileName();
                InitProgressValue();
                ConvertButtonVisible = Visibility.Hidden;
                converter.RunWorkerAsync();
            }
            else
            {
                MessageBox.Show(this, "Please specify source video!", "Empty Source Video", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }
    }
}
