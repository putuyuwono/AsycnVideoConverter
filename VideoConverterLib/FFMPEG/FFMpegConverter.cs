using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Web;

namespace VideoConverterLib
{
    public class FFMpegConverter
    {
        private Process FFMpegProcess;

        private static object globalObj = new object();

        public event EventHandler<ConvertProgressEventArgs> ConvertProgress;

        public event EventHandler<FFMpegLogEventArgs> LogReceived;

        public string FFMpegToolPath
        {
            get;
            set;
        }

        public string FFMpegExeName
        {
            get;
            set;
        }

        public TimeSpan? ExecutionTimeout
        {
            get;
            set;
        }

        public ProcessPriorityClass FFMpegProcessPriority
        {
            get;
            set;
        }

        public FFMpegUserCredential FFMpegProcessUser
        {
            get;
            set;
        }

        public string LogLevel
        {
            get;
            set;
        }

        public FFMpegConverter()
        {
            this.FFMpegProcessPriority = ProcessPriorityClass.Normal;
            this.LogLevel = "info";
            this.FFMpegToolPath = AppDomain.CurrentDomain.BaseDirectory;
            if (HttpContext.Current != null)
            {
                this.FFMpegToolPath = HttpRuntime.AppDomainAppPath + "bin";
            }
            if (string.IsNullOrEmpty(this.FFMpegToolPath))
            {
                this.FFMpegToolPath = Path.GetDirectoryName(typeof(FFMpegConverter).Assembly.Location);
            }
            this.FFMpegExeName = "ffmpeg.exe";
        }

        private void CopyStream(Stream inputStream, Stream outputStream, int bufSize)
        {
            byte[] array = new byte[bufSize];
            int count;
            while ((count = inputStream.Read(array, 0, array.Length)) > 0)
            {
                outputStream.Write(array, 0, count);
            }
        }

        public void ConvertMedia(string inputFile, string outputFile, string outputFormat)
        {
            this.ConvertMedia(inputFile, null, outputFile, outputFormat, null);
        }

        public void ConvertMedia(string inputFile, string inputFormat, string outputFile, string outputFormat, ConvertSettings settings)
        {
            if (inputFile == null)
            {
                throw new ArgumentNullException("inputFile");
            }
            if (outputFile == null)
            {
                throw new ArgumentNullException("outputFile");
            }
            if (File.Exists(inputFile) && string.IsNullOrEmpty(Path.GetExtension(inputFile)) && inputFormat == null)
            {
                throw new Exception("Input format is required for file without extension");
            }
            if (string.IsNullOrEmpty(Path.GetExtension(outputFile)) && outputFormat == null)
            {
                throw new Exception("Output format is required for file without extension");
            }
            Media input = new Media
            {
                Filename = inputFile,
                Format = inputFormat
            };
            Media output = new Media
            {
                Filename = outputFile,
                Format = outputFormat
            };
            this.ConvertMedia(input, output, settings ?? new ConvertSettings());
        }

        public void ConvertMedia(string inputFile, Stream outputStream, string outputFormat)
        {
            this.ConvertMedia(inputFile, null, outputStream, outputFormat, null);
        }

        public void ConvertMedia(string inputFile, string inputFormat, Stream outputStream, string outputFormat, ConvertSettings settings)
        {
            if (inputFile == null)
            {
                throw new ArgumentNullException("inputFile");
            }
            if (File.Exists(inputFile) && string.IsNullOrEmpty(Path.GetExtension(inputFile)) && inputFormat == null)
            {
                throw new Exception("Input format is required for file without extension");
            }
            if (outputFormat == null)
            {
                throw new ArgumentNullException("outputFormat");
            }
            Media input = new Media
            {
                Filename = inputFile,
                Format = inputFormat
            };
            Media output = new Media
            {
                DataStream = outputStream,
                Format = outputFormat
            };
            this.ConvertMedia(input, output, settings ?? new ConvertSettings());
        }

        public void ConvertMedia(FFMpegInput[] inputs, string output, string outputFormat, OutputSettings settings)
        {
            if (inputs == null || inputs.Length == 0)
            {
                throw new ArgumentException("At least one ffmpeg input should be specified");
            }
            FFMpegInput fFMpegInput = inputs[inputs.Length - 1];
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < inputs.Length - 1; i++)
            {
                FFMpegInput fFMpegInput2 = inputs[i];
                if (fFMpegInput2.Format != null)
                {
                    stringBuilder.Append(" -f " + fFMpegInput2.Format);
                }
                if (fFMpegInput2.CustomInputArgs != null)
                {
                    stringBuilder.AppendFormat(" {0} ", fFMpegInput2.CustomInputArgs);
                }
                stringBuilder.AppendFormat(" -i {0} ", this.CommandArgParameter(fFMpegInput2.Input));
            }
            ConvertSettings convertSettings = new ConvertSettings();
            settings.CopyTo(convertSettings);
            convertSettings.CustomInputArgs = stringBuilder.ToString() + fFMpegInput.CustomInputArgs;
            this.ConvertMedia(fFMpegInput.Input, fFMpegInput.Format, output, outputFormat, convertSettings);
        }

        public ConvertLiveMediaTask ConvertLiveMedia(string inputFormat, Stream outputStream, string outputFormat, ConvertSettings settings)
        {
            return this.ConvertLiveMedia(inputStream: null, inputFormat: inputFormat, outputStream: outputStream, outputFormat: outputFormat, settings: settings);
        }

        public ConvertLiveMediaTask ConvertLiveMedia(string inputSource, string inputFormat, Stream outputStream, string outputFormat, ConvertSettings settings)
        {
            this.EnsureFFMpegLibs();
            string toolArgs = this.ComposeFFMpegCommandLineArgs(inputSource, inputFormat, "-", outputFormat, settings);
            return this.CreateLiveMediaTask(toolArgs, null, outputStream, settings);
        }

        public ConvertLiveMediaTask ConvertLiveMedia(Stream inputStream, string inputFormat, string outputFile, string outputFormat, ConvertSettings settings)
        {
            this.EnsureFFMpegLibs();
            string toolArgs = this.ComposeFFMpegCommandLineArgs("-", inputFormat, outputFile, outputFormat, settings);
            return this.CreateLiveMediaTask(toolArgs, inputStream, null, settings);
        }

        public ConvertLiveMediaTask ConvertLiveMedia(Stream inputStream, string inputFormat, Stream outputStream, string outputFormat, ConvertSettings settings)
        {
            this.EnsureFFMpegLibs();
            string toolArgs = this.ComposeFFMpegCommandLineArgs("-", inputFormat, "-", outputFormat, settings);
            return this.CreateLiveMediaTask(toolArgs, inputStream, outputStream, settings);
        }

        private ConvertLiveMediaTask CreateLiveMediaTask(string toolArgs, Stream inputStream, Stream outputStream, ConvertSettings settings)
        {
            FFMpegProgress fFMpegProgress = new FFMpegProgress(new Action<ConvertProgressEventArgs>(this.OnConvertProgress), this.ConvertProgress != null);
            if (settings != null)
            {
                fFMpegProgress.Seek = settings.Seek;
                fFMpegProgress.MaxDuration = settings.MaxDuration;
            }
            return new ConvertLiveMediaTask(this, toolArgs, inputStream, outputStream, fFMpegProgress);
        }

        public void GetVideoThumbnail(string inputFile, Stream outputJpegStream)
        {
            this.GetVideoThumbnail(inputFile, outputJpegStream, null);
        }

        public void GetVideoThumbnail(string inputFile, string outputFile)
        {
            this.GetVideoThumbnail(inputFile, outputFile, null);
        }

        public void GetVideoThumbnail(string inputFile, Stream outputJpegStream, float? frameTime)
        {
            Media input = new Media
            {
                Filename = inputFile
            };
            Media output = new Media
            {
                DataStream = outputJpegStream,
                Format = "mjpeg"
            };
            ConvertSettings settings = new ConvertSettings
            {
                VideoFrameCount = new int?(1),
                Seek = frameTime,
                MaxDuration = new float?(1f)
            };
            this.ConvertMedia(input, output, settings);
        }

        public void GetVideoThumbnail(string inputFile, string outputFile, float? frameTime)
        {
            Media input = new Media
            {
                Filename = inputFile
            };
            Media output = new Media
            {
                Filename = outputFile,
                Format = "mjpeg"
            };
            ConvertSettings settings = new ConvertSettings
            {
                VideoFrameCount = new int?(1),
                Seek = frameTime,
                MaxDuration = new float?(1f)
            };
            this.ConvertMedia(input, output, settings);
        }

        private string CommandArgParameter(string arg)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append('"');
            stringBuilder.Append(arg);
            stringBuilder.Append('"');
            return stringBuilder.ToString();
        }

        internal void InitStartInfo(ProcessStartInfo startInfo)
        {
            if (this.FFMpegProcessUser != null)
            {
                if (this.FFMpegProcessUser.Domain != null)
                {
                    startInfo.Domain = this.FFMpegProcessUser.Domain;
                }
                if (this.FFMpegProcessUser.UserName != null)
                {
                    startInfo.UserName = this.FFMpegProcessUser.UserName;
                }
                if (this.FFMpegProcessUser.Password != null)
                {
                    startInfo.Password = this.FFMpegProcessUser.Password;
                }
            }
        }

        internal string GetFFMpegExePath()
        {
            return Path.Combine(this.FFMpegToolPath, this.FFMpegExeName);
        }

        public void ConcatMedia(string[] inputFiles, string outputFile, string outputFormat, ConcatSettings settings)
        {
            this.EnsureFFMpegLibs();
            string fFMpegExePath = this.GetFFMpegExePath();
            if (!File.Exists(fFMpegExePath))
            {
                throw new FileNotFoundException("Cannot find ffmpeg tool: " + fFMpegExePath);
            }
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < inputFiles.Length; i++)
            {
                string text = inputFiles[i];
                if (!File.Exists(text))
                {
                    throw new FileNotFoundException("Cannot find input video file: " + text);
                }
                stringBuilder.AppendFormat(" -i {0} ", this.CommandArgParameter(text));
            }
            StringBuilder stringBuilder2 = new StringBuilder();
            this.ComposeFFMpegOutputArgs(stringBuilder2, outputFormat, settings);
            stringBuilder2.Append(" -filter_complex \"");
            stringBuilder2.AppendFormat("concat=n={0}", inputFiles.Length);
            if (settings.ConcatVideoStream)
            {
                stringBuilder2.Append(":v=1");
            }
            if (settings.ConcatAudioStream)
            {
                stringBuilder2.Append(":a=1");
            }
            if (settings.ConcatVideoStream)
            {
                stringBuilder2.Append(" [v]");
            }
            if (settings.ConcatAudioStream)
            {
                stringBuilder2.Append(" [a]");
            }
            stringBuilder2.Append("\" ");
            if (settings.ConcatVideoStream)
            {
                stringBuilder2.Append(" -map \"[v]\" ");
            }
            if (settings.ConcatAudioStream)
            {
                stringBuilder2.Append(" -map \"[a]\" ");
            }
            string arguments = string.Format("-y -loglevel {3} {0} {1} {2}", new object[]
            {
                stringBuilder.ToString(),
                stringBuilder2,
                this.CommandArgParameter(outputFile),
                this.LogLevel
            });
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo(fFMpegExePath, arguments);
                processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                processStartInfo.UseShellExecute = false;
                processStartInfo.CreateNoWindow = true;
                processStartInfo.WorkingDirectory = Path.GetDirectoryName(this.FFMpegToolPath);
                processStartInfo.RedirectStandardInput = true;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                this.InitStartInfo(processStartInfo);
                if (this.FFMpegProcess != null)
                {
                    throw new InvalidOperationException("FFMpeg process is already started");
                }
                this.FFMpegProcess = Process.Start(processStartInfo);
                if (this.FFMpegProcessPriority != ProcessPriorityClass.Normal)
                {
                    this.FFMpegProcess.PriorityClass = this.FFMpegProcessPriority;
                }
                string lastErrorLine = string.Empty;
                FFMpegProgress ffmpegProgress = new FFMpegProgress(new Action<ConvertProgressEventArgs>(this.OnConvertProgress), this.ConvertProgress != null);
                if (settings != null)
                {
                    ffmpegProgress.MaxDuration = settings.MaxDuration;
                }
                this.FFMpegProcess.ErrorDataReceived += delegate (object o, DataReceivedEventArgs args)
                {
                    if (args.Data == null)
                    {
                        return;
                    }
                    lastErrorLine = args.Data;
                    ffmpegProgress.ParseLine(args.Data);
                    this.FFMpegLogHandler(args.Data);
                };
                this.FFMpegProcess.OutputDataReceived += delegate (object o, DataReceivedEventArgs args)
                {
                };
                this.FFMpegProcess.BeginOutputReadLine();
                this.FFMpegProcess.BeginErrorReadLine();
                this.WaitFFMpegProcessForExit();
                if (this.FFMpegProcess.ExitCode != 0)
                {
                    throw new FFMpegException(this.FFMpegProcess.ExitCode, lastErrorLine);
                }
                this.FFMpegProcess.Close();
                this.FFMpegProcess = null;
                ffmpegProgress.Complete();
            }
            catch (Exception)
            {
                this.EnsureFFMpegProcessStopped();
                throw;
            }
        }

        protected void WaitFFMpegProcessForExit()
        {
            if (this.FFMpegProcess == null)
            {
                throw new FFMpegException(-1, "FFMpeg process was aborted");
            }
            if (this.FFMpegProcess.HasExited)
            {
                return;
            }
            int milliseconds = this.ExecutionTimeout.HasValue ? ((int)this.ExecutionTimeout.Value.TotalMilliseconds) : 2147483647;
            if (!this.FFMpegProcess.WaitForExit(milliseconds))
            {
                this.EnsureFFMpegProcessStopped();
                throw new FFMpegException(-2, string.Format("FFMpeg process exceeded execution timeout ({0}) and was aborted", this.ExecutionTimeout));
            }
        }

        protected void EnsureFFMpegProcessStopped()
        {
            if (this.FFMpegProcess != null && !this.FFMpegProcess.HasExited)
            {
                try
                {
                    this.FFMpegProcess.Kill();
                    this.FFMpegProcess = null;
                }
                catch (Exception)
                {
                }
            }
        }

        protected void ComposeFFMpegOutputArgs(StringBuilder outputArgs, string outputFormat, OutputSettings settings)
        {
            if (settings == null)
            {
                return;
            }
            if (settings.MaxDuration.HasValue)
            {
                outputArgs.AppendFormat(CultureInfo.InvariantCulture, " -t {0}", new object[]
                {
                    settings.MaxDuration
                });
            }
            if (outputFormat != null)
            {
                outputArgs.AppendFormat(" -f {0} ", outputFormat);
            }
            if (settings.AudioSampleRate.HasValue)
            {
                outputArgs.AppendFormat(" -ar {0}", settings.AudioSampleRate);
            }
            if (settings.AudioCodec != null)
            {
                outputArgs.AppendFormat(" -acodec {0}", settings.AudioCodec);
            }
            if (settings.VideoFrameCount.HasValue)
            {
                outputArgs.AppendFormat(" -vframes {0}", settings.VideoFrameCount);
            }
            if (settings.VideoFrameRate.HasValue)
            {
                outputArgs.AppendFormat(" -r {0}", settings.VideoFrameRate);
            }
            if (settings.VideoCodec != null)
            {
                outputArgs.AppendFormat(" -vcodec {0}", settings.VideoCodec);
            }
            if (settings.VideoFrameSize != null)
            {
                outputArgs.AppendFormat(" -s {0}", settings.VideoFrameSize);
            }
            if (settings.CustomOutputArgs != null)
            {
                outputArgs.AppendFormat(" {0} ", settings.CustomOutputArgs);
            }
        }

        protected string ComposeFFMpegCommandLineArgs(string inputFile, string inputFormat, string outputFile, string outputFormat, ConvertSettings settings)
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (settings.AppendSilentAudioStream)
            {
                stringBuilder.Append(" -f lavfi -i aevalsrc=0 ");
            }
            if (settings.Seek.HasValue)
            {
                stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " -ss {0}", new object[]
                {
                    settings.Seek
                });
            }
            if (inputFormat != null)
            {
                stringBuilder.Append(" -f " + inputFormat);
            }
            if (settings.CustomInputArgs != null)
            {
                stringBuilder.AppendFormat(" {0} ", settings.CustomInputArgs);
            }
            StringBuilder stringBuilder2 = new StringBuilder();
            this.ComposeFFMpegOutputArgs(stringBuilder2, outputFormat, settings);
            if (settings.AppendSilentAudioStream)
            {
                stringBuilder2.Append(" -shortest ");
            }
            return string.Format("-y -loglevel {4} {0} -i {1} {2} {3}", new object[]
            {
                stringBuilder.ToString(),
                this.CommandArgParameter(inputFile),
                stringBuilder2.ToString(),
                this.CommandArgParameter(outputFile),
                this.LogLevel
            });
        }

        public void Invoke(string ffmpegArgs)
        {
            this.EnsureFFMpegLibs();
            try
            {
                string fFMpegExePath = this.GetFFMpegExePath();
                if (!File.Exists(fFMpegExePath))
                {
                    throw new FileNotFoundException("Cannot find ffmpeg tool: " + fFMpegExePath);
                }
                ProcessStartInfo processStartInfo = new ProcessStartInfo(fFMpegExePath, ffmpegArgs);
                processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                processStartInfo.CreateNoWindow = true;
                processStartInfo.UseShellExecute = false;
                processStartInfo.WorkingDirectory = Path.GetDirectoryName(this.FFMpegToolPath);
                processStartInfo.RedirectStandardInput = true;
                processStartInfo.RedirectStandardOutput = false;
                processStartInfo.RedirectStandardError = true;
                this.InitStartInfo(processStartInfo);
                if (this.FFMpegProcess != null)
                {
                    throw new InvalidOperationException("FFMpeg process is already started");
                }
                this.FFMpegProcess = Process.Start(processStartInfo);
                if (this.FFMpegProcessPriority != ProcessPriorityClass.Normal)
                {
                    this.FFMpegProcess.PriorityClass = this.FFMpegProcessPriority;
                }
                string lastErrorLine = string.Empty;
                this.FFMpegProcess.ErrorDataReceived += delegate (object o, DataReceivedEventArgs args)
                {
                    if (args.Data == null)
                    {
                        return;
                    }
                    lastErrorLine = args.Data;
                    this.FFMpegLogHandler(args.Data);
                };
                this.FFMpegProcess.BeginErrorReadLine();
                this.WaitFFMpegProcessForExit();
                if (this.FFMpegProcess.ExitCode != 0)
                {
                    throw new FFMpegException(this.FFMpegProcess.ExitCode, lastErrorLine);
                }
                this.FFMpegProcess.Close();
                this.FFMpegProcess = null;
            }
            catch (Exception)
            {
                this.EnsureFFMpegProcessStopped();
                throw;
            }
        }

        internal void FFMpegLogHandler(string line)
        {
            if (this.LogReceived != null)
            {
                this.LogReceived(this, new FFMpegLogEventArgs(line));
            }
        }

        internal void OnConvertProgress(ConvertProgressEventArgs args)
        {
            if (this.ConvertProgress != null)
            {
                this.ConvertProgress(this, args);
            }
        }

        internal void ConvertMedia(Media input, Media output, ConvertSettings settings)
        {
            this.EnsureFFMpegLibs();
            string text = input.Filename;
            if (text == null)
            {
                text = Path.GetTempFileName();
                using (FileStream fileStream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    this.CopyStream(input.DataStream, fileStream, 262144);
                }
            }
            string text2 = output.Filename;
            if (text2 == null)
            {
                text2 = Path.GetTempFileName();
            }
            if ((output.Format == "flv" || Path.GetExtension(text2).ToLower() == ".flv") && !settings.AudioSampleRate.HasValue)
            {
                settings.AudioSampleRate = new int?(44100);
            }
            try
            {
                string fFMpegExePath = this.GetFFMpegExePath();
                if (!File.Exists(fFMpegExePath))
                {
                    throw new FileNotFoundException("Cannot find ffmpeg tool: " + fFMpegExePath);
                }
                string arguments = this.ComposeFFMpegCommandLineArgs(text, input.Format, text2, output.Format, settings);
                ProcessStartInfo processStartInfo = new ProcessStartInfo(fFMpegExePath, arguments);
                processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                processStartInfo.CreateNoWindow = true;
                processStartInfo.UseShellExecute = false;
                processStartInfo.WorkingDirectory = Path.GetDirectoryName(this.FFMpegToolPath);
                processStartInfo.RedirectStandardInput = true;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                this.InitStartInfo(processStartInfo);
                if (this.FFMpegProcess != null)
                {
                    throw new InvalidOperationException("FFMpeg process is already started");
                }
                this.FFMpegProcess = Process.Start(processStartInfo);
                if (this.FFMpegProcessPriority != ProcessPriorityClass.Normal)
                {
                    this.FFMpegProcess.PriorityClass = this.FFMpegProcessPriority;
                }
                string lastErrorLine = string.Empty;
                FFMpegProgress ffmpegProgress = new FFMpegProgress(new Action<ConvertProgressEventArgs>(this.OnConvertProgress), this.ConvertProgress != null);
                if (settings != null)
                {
                    ffmpegProgress.Seek = settings.Seek;
                    ffmpegProgress.MaxDuration = settings.MaxDuration;
                }
                this.FFMpegProcess.ErrorDataReceived += delegate (object o, DataReceivedEventArgs args)
                {
                    if (args.Data == null)
                    {
                        return;
                    }
                    lastErrorLine = args.Data;
                    ffmpegProgress.ParseLine(args.Data);
                    this.FFMpegLogHandler(args.Data);
                };
                this.FFMpegProcess.OutputDataReceived += delegate (object o, DataReceivedEventArgs args)
                {
                };
                this.FFMpegProcess.BeginOutputReadLine();
                this.FFMpegProcess.BeginErrorReadLine();
                this.WaitFFMpegProcessForExit();
                if (this.FFMpegProcess.ExitCode != 0)
                {
                    throw new FFMpegException(this.FFMpegProcess.ExitCode, lastErrorLine);
                }
                this.FFMpegProcess.Close();
                this.FFMpegProcess = null;
                ffmpegProgress.Complete();
                if (output.Filename == null)
                {
                    using (FileStream fileStream2 = new FileStream(text2, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        this.CopyStream(fileStream2, output.DataStream, 262144);
                    }
                }
            }
            catch (Exception)
            {
                this.EnsureFFMpegProcessStopped();
                throw;
            }
            finally
            {
                if (text != null && input.Filename == null && File.Exists(text))
                {
                    File.Delete(text);
                }
                if (text2 != null && output.Filename == null && File.Exists(text2))
                {
                    File.Delete(text2);
                }
            }
        }

        public void ExtractFFmpeg()
        {
            this.EnsureFFMpegLibs();
        }

        private void EnsureFFMpegLibs()
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            string[] manifestResourceNames = executingAssembly.GetManifestResourceNames();
            string text = "VideoConverterLib.FFMpeg.";
            string[] array = manifestResourceNames;
            for (int i = 0; i < array.Length; i++)
            {
                string text2 = array[i];
                if (text2.StartsWith(text))
                {
                    string path = text2.Substring(text.Length);
                    string path2 = Path.Combine(this.FFMpegToolPath, Path.GetFileNameWithoutExtension(path));
                    lock (FFMpegConverter.globalObj)
                    {
                        if (!File.Exists(path2) || !(File.GetLastWriteTime(path2) > File.GetLastWriteTime(executingAssembly.Location)))
                        {
                            Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(text2);
                            using (GZipStream gZipStream = new GZipStream(manifestResourceStream, CompressionMode.Decompress, false))
                            {
                                using (FileStream fileStream = new FileStream(path2, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    byte[] array2 = new byte[65536];
                                    int count;
                                    while ((count = gZipStream.Read(array2, 0, array2.Length)) > 0)
                                    {
                                        fileStream.Write(array2, 0, count);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void Abort()
        {
            this.EnsureFFMpegProcessStopped();
        }

        public bool Stop()
        {
            if (this.FFMpegProcess != null && !this.FFMpegProcess.HasExited && this.FFMpegProcess.StartInfo.RedirectStandardInput)
            {
                this.FFMpegProcess.StandardInput.WriteLine("q\n");
                this.FFMpegProcess.StandardInput.Close();
                this.WaitFFMpegProcessForExit();
                return true;
            }
            return false;
        }
    }
}
