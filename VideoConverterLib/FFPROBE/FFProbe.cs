using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Web;
using System.Xml.XPath;

namespace VideoConverterLib
{
    public class FFProbe
    {
        private static object globalObj = new object();

        public string ToolPath
        {
            get;
            set;
        }

        public string FFProbeExeName
        {
            get;
            set;
        }

        public string CustomArgs
        {
            get;
            set;
        }

        public ProcessPriorityClass ProcessPriority
        {
            get;
            set;
        }

        public TimeSpan? ExecutionTimeout
        {
            get;
            set;
        }

        public bool IncludeFormat
        {
            get;
            set;
        }

        public bool IncludeStreams
        {
            get;
            set;
        }

        public FFProbe()
        {
            string toolPath = AppDomain.CurrentDomain.BaseDirectory;
            if (HttpContext.Current != null)
            {
                toolPath = HttpRuntime.AppDomainAppPath + "bin";
            }
            this.ToolPath = toolPath;
            this.FFProbeExeName = "ffprobe.exe";
            this.ProcessPriority = ProcessPriorityClass.Normal;
            this.IncludeFormat = true;
            this.IncludeStreams = true;
        }

        private void EnsureFFProbe()
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            string[] manifestResourceNames = executingAssembly.GetManifestResourceNames();
            string text = "VideoConverterLib.FFProbe.";
            string[] array = manifestResourceNames;
            for (int i = 0; i < array.Length; i++)
            {
                string text2 = array[i];
                if (text2.StartsWith(text))
                {
                    string path = text2.Substring(text.Length);
                    string path2 = Path.Combine(this.ToolPath, Path.GetFileNameWithoutExtension(path));
                    lock (FFProbe.globalObj)
                    {
                        if (!File.Exists(path2) || !(File.GetLastWriteTime(path2) > File.GetLastWriteTime(executingAssembly.Location)))
                        {
                            Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(text2);
                            using (GZipStream gZipStream = new GZipStream(manifestResourceStream, CompressionMode.Decompress, false))
                            {
                                using (FileStream fileStream = new FileStream(path2, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    byte[] array2 = new byte[131072];
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

        public MediaInfo GetMediaInfo(string inputFile)
        {
            return new MediaInfo(this.GetInfoInternal(inputFile));
        }

        private XPathDocument GetInfoInternal(string input)
        {
            this.EnsureFFProbe();
            XPathDocument result;
            try
            {
                string text = Path.Combine(this.ToolPath, this.FFProbeExeName);
                if (!File.Exists(text))
                {
                    throw new FileNotFoundException("Cannot locate FFProbe: " + text);
                }
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(" -print_format xml -sexagesimal ");
                if (this.IncludeFormat)
                {
                    stringBuilder.Append(" -show_format ");
                }
                if (this.IncludeStreams)
                {
                    stringBuilder.Append(" -show_streams ");
                }
                if (!string.IsNullOrEmpty(this.CustomArgs))
                {
                    stringBuilder.Append(this.CustomArgs);
                }
                stringBuilder.AppendFormat(" \"{0}\" ", input);
                Process process = Process.Start(new ProcessStartInfo(text, stringBuilder.ToString())
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(this.ToolPath),
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                if (this.ProcessPriority != ProcessPriorityClass.Normal)
                {
                    process.PriorityClass = this.ProcessPriority;
                }
                string lastErrorLine = string.Empty;
                process.ErrorDataReceived += delegate (object o, DataReceivedEventArgs args)
                {
                    if (args.Data == null)
                    {
                        return;
                    }
                    lastErrorLine = lastErrorLine + args.Data + "\n";
                };
                process.BeginErrorReadLine();
                string s = process.StandardOutput.ReadToEnd();
                this.WaitProcessForExit(process);
                this.CheckExitCode(process.ExitCode, lastErrorLine);
                process.Close();
                result = new XPathDocument(new StringReader(s));
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
            return result;
        }

        private void WaitProcessForExit(Process proc)
        {
            if (this.ExecutionTimeout.HasValue)
            {
                if (!proc.WaitForExit((int)this.ExecutionTimeout.Value.TotalMilliseconds))
                {
                    this.EnsureProcessStopped(proc);
                    throw new FFProbeException(-2, string.Format("FFProbe process exceeded execution timeout ({0}) and was aborted", this.ExecutionTimeout));
                }
            }
            else
            {
                proc.WaitForExit();
            }
        }

        private void EnsureProcessStopped(Process proc)
        {
            if (!proc.HasExited)
            {
                try
                {
                    proc.Kill();
                    proc.Close();
                    proc = null;
                    return;
                }
                catch (Exception)
                {
                    return;
                }
            }
            proc.Close();
            proc = null;
        }

        private void CheckExitCode(int exitCode, string lastErrLine)
        {
            if (exitCode != 0)
            {
                throw new FFProbeException(exitCode, lastErrLine);
            }
        }
    }
}
