using System;
using System.Text.RegularExpressions;

namespace VideoConverterLib
{
    internal class FFMpegProgress
    {
        private static Regex DurationRegex = new Regex("Duration:\\s(?<duration>[0-9:.]+)([,]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        private static Regex ProgressRegex = new Regex("time=(?<progress>[0-9:.]+)\\s", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        internal float? Seek = null;

        internal float? MaxDuration = null;

        private Action<ConvertProgressEventArgs> ProgressCallback;

        private ConvertProgressEventArgs lastProgressArgs;

        private bool Enabled = true;

        private int progressEventCount;

        internal FFMpegProgress(Action<ConvertProgressEventArgs> progressCallback, bool enabled)
        {
            this.ProgressCallback = progressCallback;
            this.Enabled = enabled;
        }

        internal void Reset()
        {
            this.progressEventCount = 0;
            this.lastProgressArgs = null;
        }

        internal void ParseLine(string line)
        {
            if (this.Enabled)
            {
                TimeSpan timeSpan = (this.lastProgressArgs != null) ? this.lastProgressArgs.TotalDuration : TimeSpan.Zero;
                Match match = FFMpegProgress.DurationRegex.Match(line);
                if (match.Success)
                {
                    TimeSpan zero = TimeSpan.Zero;
                    if (TimeSpan.TryParse(match.Groups["duration"].Value, out zero))
                    {
                        TimeSpan totalDuration = timeSpan.Add(zero);
                        this.lastProgressArgs = new ConvertProgressEventArgs(TimeSpan.Zero, totalDuration);
                    }
                }
                Match match2 = FFMpegProgress.ProgressRegex.Match(line);
                if (match2.Success)
                {
                    TimeSpan zero2 = TimeSpan.Zero;
                    if (TimeSpan.TryParse(match2.Groups["progress"].Value, out zero2))
                    {
                        if (this.progressEventCount == 0)
                        {
                            timeSpan = this.CorrectDuration(timeSpan);
                        }
                        this.lastProgressArgs = new ConvertProgressEventArgs(zero2, (timeSpan != TimeSpan.Zero) ? timeSpan : zero2);
                        this.ProgressCallback(this.lastProgressArgs);
                        this.progressEventCount++;
                    }
                }
            }
        }

        private TimeSpan CorrectDuration(TimeSpan totalDuration)
        {
            if (totalDuration != TimeSpan.Zero)
            {
                if (this.Seek.HasValue)
                {
                    TimeSpan timeSpan = TimeSpan.FromSeconds((double)this.Seek.Value);
                    totalDuration = ((totalDuration > timeSpan) ? totalDuration.Subtract(timeSpan) : TimeSpan.Zero);
                }
                if (this.MaxDuration.HasValue)
                {
                    TimeSpan timeSpan2 = TimeSpan.FromSeconds((double)this.MaxDuration.Value);
                    if (totalDuration > timeSpan2)
                    {
                        totalDuration = timeSpan2;
                    }
                }
            }
            return totalDuration;
        }

        internal void Complete()
        {
            if (this.Enabled && this.lastProgressArgs != null && this.lastProgressArgs.Processed < this.lastProgressArgs.TotalDuration)
            {
                this.ProgressCallback(new ConvertProgressEventArgs(this.lastProgressArgs.TotalDuration, this.lastProgressArgs.TotalDuration));
            }
        }
    }
}
