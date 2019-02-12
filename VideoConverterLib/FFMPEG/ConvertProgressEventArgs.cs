using System;

namespace VideoConverterLib
{
    public class ConvertProgressEventArgs : EventArgs
    {
        public TimeSpan TotalDuration
        {
            get;
            private set;
        }

        public TimeSpan Processed
        {
            get;
            private set;
        }

        public ConvertProgressEventArgs(TimeSpan processed, TimeSpan totalDuration)
        {
            this.TotalDuration = totalDuration;
            this.Processed = processed;
        }
    }
}
