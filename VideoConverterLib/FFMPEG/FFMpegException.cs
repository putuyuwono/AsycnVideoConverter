using System;

namespace VideoConverterLib
{
    public class FFMpegException : Exception
    {
        public int ErrorCode
        {
            get;
            private set;
        }

        public FFMpegException(int errCode, string message) : base(string.Format("{0} (exit code: {1})", message, errCode))
        {
            this.ErrorCode = errCode;
        }
    }
}
