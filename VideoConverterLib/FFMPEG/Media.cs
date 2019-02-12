using System.IO;

namespace VideoConverterLib
{
    internal class Media
    {
        public string Filename
        {
            get;
            set;
        }

        public string Format
        {
            get;
            set;
        }

        public Stream DataStream
        {
            get;
            set;
        }
    }
}
