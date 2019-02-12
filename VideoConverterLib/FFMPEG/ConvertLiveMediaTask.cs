using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace VideoConverterLib
{
    public class ConvertLiveMediaTask
    {
        internal class StreamOperationContext
        {
            private bool isInput;

            private bool isRead;

            public Stream TargetStream
            {
                get;
                private set;
            }

            public bool Read
            {
                get
                {
                    return this.isRead;
                }
            }

            public bool Write
            {
                get
                {
                    return !this.isRead;
                }
            }

            public bool IsInput
            {
                get
                {
                    return this.isInput;
                }
            }

            public bool IsOutput
            {
                get
                {
                    return !this.isInput;
                }
            }

            internal StreamOperationContext(Stream stream, bool isInput, bool isRead)
            {
                this.TargetStream = stream;
                this.isInput = isInput;
                this.isRead = isRead;
            }
        }

        private Stream Input;

        private Stream Output;

        private FFMpegConverter FFMpegConv;

        private string FFMpegToolArgs;

        private Process FFMpegProcess;

        private Thread CopyToStdInThread;

        private Thread CopyFromStdOutThread;

        public EventHandler OutputDataReceived;

        private string lastErrorLine;

        private FFMpegProgress ffmpegProgress;

        private long WriteBytesCount;

        private Exception lastStreamException;

        internal ConvertLiveMediaTask(FFMpegConverter ffmpegConv, string ffMpegArgs, Stream inputStream, Stream outputStream, FFMpegProgress progress)
        {
            this.Input = inputStream;
            this.Output = outputStream;
            this.FFMpegConv = ffmpegConv;
            this.FFMpegToolArgs = ffMpegArgs;
            this.ffmpegProgress = progress;
        }

        public void Start()
        {
            this.lastStreamException = null;
            string fFMpegExePath = this.FFMpegConv.GetFFMpegExePath();
            if (!File.Exists(fFMpegExePath))
            {
                throw new FileNotFoundException("Cannot find ffmpeg tool: " + fFMpegExePath);
            }
            ProcessStartInfo processStartInfo = new ProcessStartInfo(fFMpegExePath, "-stdin " + this.FFMpegToolArgs);
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.WorkingDirectory = Path.GetDirectoryName(fFMpegExePath);
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.StandardOutputEncoding = Encoding.Default;
            this.FFMpegConv.InitStartInfo(processStartInfo);
            this.FFMpegProcess = Process.Start(processStartInfo);
            if (this.FFMpegConv.FFMpegProcessPriority != ProcessPriorityClass.Normal)
            {
                this.FFMpegProcess.PriorityClass = this.FFMpegConv.FFMpegProcessPriority;
            }
            this.lastErrorLine = null;
            this.ffmpegProgress.Reset();
            this.FFMpegProcess.ErrorDataReceived += delegate (object o, DataReceivedEventArgs args)
            {
                if (args.Data == null)
                {
                    return;
                }
                this.lastErrorLine = args.Data;
                this.ffmpegProgress.ParseLine(args.Data);
                this.FFMpegConv.FFMpegLogHandler(args.Data);
            };
            this.FFMpegProcess.BeginErrorReadLine();
            if (this.Input != null)
            {
                this.CopyToStdInThread = new Thread(new ThreadStart(this.CopyToStdIn));
                this.CopyToStdInThread.Start();
            }
            else
            {
                this.CopyToStdInThread = null;
            }
            if (this.Output != null)
            {
                this.CopyFromStdOutThread = new Thread(new ThreadStart(this.CopyFromStdOut));
                this.CopyFromStdOutThread.Start();
                return;
            }
            this.CopyFromStdOutThread = null;
        }

        public void Write(byte[] buf, int offset, int count)
        {
            if (!this.FFMpegProcess.HasExited)
            {
                this.FFMpegProcess.StandardInput.BaseStream.Write(buf, offset, count);
                this.FFMpegProcess.StandardInput.BaseStream.Flush();
                this.WriteBytesCount += (long)count;
                return;
            }
            if (this.FFMpegProcess.ExitCode != 0)
            {
                throw new FFMpegException(this.FFMpegProcess.ExitCode, string.IsNullOrEmpty(this.lastErrorLine) ? "FFMpeg process has exited" : this.lastErrorLine);
            }
            throw new FFMpegException(-1, "FFMpeg process has exited");
        }

        public void Stop()
        {
            this.Stop(false);
        }

        public void Stop(bool forceFFMpegQuit)
        {
            if (this.CopyToStdInThread != null)
            {
                this.CopyToStdInThread = null;
            }
            if (forceFFMpegQuit)
            {
                if (this.Input == null && this.WriteBytesCount == 0L)
                {
                    this.FFMpegProcess.StandardInput.WriteLine("q\n");
                    this.FFMpegProcess.StandardInput.Close();
                }
                else
                {
                    this.Abort();
                }
            }
            else
            {
                this.FFMpegProcess.StandardInput.BaseStream.Close();
            }
            this.Wait();
        }

        private void OnStreamError(Exception ex, bool isStdinStdout)
        {
            if (ex is IOException && isStdinStdout)
            {
                return;
            }
            this.lastStreamException = ex;
            this.Abort();
        }

        protected void CopyToStdIn()
        {
            byte[] array = new byte[65536];
            Thread copyToStdInThread = this.CopyToStdInThread;
            Process fFMpegProcess = this.FFMpegProcess;
            Stream baseStream = this.FFMpegProcess.StandardInput.BaseStream;
            while (true)
            {
                int num;
                try
                {
                    num = this.Input.Read(array, 0, array.Length);
                }
                catch (Exception ex)
                {
                    this.OnStreamError(ex, false);
                    break;
                }
                if (num <= 0)
                {
                    goto IL_94;
                }
                if (this.FFMpegProcess == null || !object.ReferenceEquals(copyToStdInThread, this.CopyToStdInThread) || !object.ReferenceEquals(fFMpegProcess, this.FFMpegProcess))
                {
                    break;
                }
                try
                {
                    baseStream.Write(array, 0, num);
                    baseStream.Flush();
                    continue;
                }
                catch (Exception ex2)
                {
                    this.OnStreamError(ex2, true);
                    break;
                }                
            }
            return;
            IL_94:
            this.FFMpegProcess.StandardInput.Close();
        }

        protected void CopyFromStdOut()
        {
            byte[] array = new byte[65536];
            Thread copyFromStdOutThread = this.CopyFromStdOutThread;
            Stream baseStream = this.FFMpegProcess.StandardOutput.BaseStream;
            while (object.ReferenceEquals(copyFromStdOutThread, this.CopyFromStdOutThread))
            {
                int num;
                try
                {
                    num = baseStream.Read(array, 0, array.Length);
                }
                catch (Exception ex)
                {
                    this.OnStreamError(ex, true);
                    return;
                }
                if (num <= 0)
                {
                    Thread.Sleep(30);
                    continue;
                }
                if (object.ReferenceEquals(copyFromStdOutThread, this.CopyFromStdOutThread))
                {
                    try
                    {
                        this.Output.Write(array, 0, num);
                        this.Output.Flush();
                    }
                    catch (Exception ex2)
                    {
                        this.OnStreamError(ex2, false);
                        return;
                    }
                    if (this.OutputDataReceived != null)
                    {
                        this.OutputDataReceived(this, EventArgs.Empty);
                        continue;
                    }
                    continue;
                }
                return;
            }
        }

        public void Wait()
        {
            this.FFMpegProcess.WaitForExit(2147483647);
            if (this.CopyToStdInThread != null)
            {
                this.CopyToStdInThread = null;
            }
            if (this.CopyFromStdOutThread != null)
            {
                this.CopyFromStdOutThread = null;
            }
            if (this.FFMpegProcess.ExitCode != 0)
            {
                throw new FFMpegException(this.FFMpegProcess.ExitCode, this.lastErrorLine ?? "Unknown error");
            }
            if (this.lastStreamException != null)
            {
                throw new IOException(this.lastStreamException.Message, this.lastStreamException);
            }
            this.FFMpegProcess.Close();
            this.ffmpegProgress.Complete();
        }

        public void Abort()
        {
            if (this.CopyToStdInThread != null)
            {
                this.CopyToStdInThread = null;
            }
            if (this.CopyFromStdOutThread != null)
            {
                this.CopyFromStdOutThread = null;
            }
            try
            {
                this.FFMpegProcess.Kill();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
