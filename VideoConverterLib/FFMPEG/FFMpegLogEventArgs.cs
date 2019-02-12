using System;

namespace VideoConverterLib
{
    public class FFMpegLogEventArgs : EventArgs
    {
        public string Data
        {
            get;
            private set;
        }

        public FFMpegLogEventArgs(string logData)
        {
            this.Data = logData;
        }
    }
}
