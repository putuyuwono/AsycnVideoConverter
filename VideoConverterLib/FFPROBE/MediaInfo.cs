using System;
using System.Collections.Generic;
using System.Xml.XPath;

namespace VideoConverterLib
{
    public class MediaInfo
    {
        public class StreamInfo
        {
            private MediaInfo Info;

            private KeyValuePair<string, string>[] _Tags;

            public string Index
            {
                get;
                private set;
            }

            public string CodecName
            {
                get
                {
                    return this.Info.GetAttrValue(this.XPathPrefix + "/@codec_name");
                }
            }

            public string CodecLongName
            {
                get
                {
                    return this.Info.GetAttrValue(this.XPathPrefix + "/@codec_long_name");
                }
            }

            public string CodecType
            {
                get
                {
                    return this.Info.GetAttrValue(this.XPathPrefix + "/@codec_type");
                }
            }

            public string PixelFormat
            {
                get
                {
                    return this.Info.GetAttrValue(this.XPathPrefix + "/@pix_fmt");
                }
            }

            public int Width
            {
                get
                {
                    string attrValue = this.Info.GetAttrValue(this.XPathPrefix + "/@width");
                    return this.ParseInt(attrValue);
                }
            }

            public int Height
            {
                get
                {
                    string attrValue = this.Info.GetAttrValue(this.XPathPrefix + "/@height");
                    return this.ParseInt(attrValue);
                }
            }

            public float FrameRate
            {
                get
                {
                    string attrValue = this.Info.GetAttrValue(this.XPathPrefix + "/@r_frame_rate");
                    if (!string.IsNullOrEmpty(attrValue))
                    {
                        string[] array = attrValue.Split(new char[]
                        {
                            '/'
                        });
                        if (array.Length == 2)
                        {
                            int num = this.ParseInt(array[0]);
                            int num2 = this.ParseInt(array[1]);
                            if (num > 0 && num2 > 0)
                            {
                                return (float)num / (float)num2;
                            }
                        }
                    }
                    return -1f;
                }
            }

            public KeyValuePair<string, string>[] Tags
            {
                get
                {
                    KeyValuePair<string, string>[] arg_2E_0;
                    if ((arg_2E_0 = this._Tags) == null)
                    {
                        arg_2E_0 = (this._Tags = this.Info.GetTags(this.XPathPrefix + "/tag"));
                    }
                    return arg_2E_0;
                }
            }

            private string XPathPrefix
            {
                get
                {
                    return "/ffprobe/streams/stream[@index=\"" + this.Index + "\"]";
                }
            }

            private int ParseInt(string s)
            {
                int result;
                if (!string.IsNullOrEmpty(s) && int.TryParse(s, out result))
                {
                    return result;
                }
                return -1;
            }

            internal StreamInfo(MediaInfo info, string index)
            {
                this.Info = info;
                this.Index = index;
            }
        }

        private KeyValuePair<string, string>[] _FormatTags;

        private MediaInfo.StreamInfo[] _Streams;

        public string FormatName
        {
            get
            {
                return this.GetAttrValue("/ffprobe/format/@format_name");
            }
        }

        public string FormatLongName
        {
            get
            {
                return this.GetAttrValue("/ffprobe/format/@format_long_name");
            }
        }

        public KeyValuePair<string, string>[] FormatTags
        {
            get
            {
                KeyValuePair<string, string>[] arg_1E_0;
                if ((arg_1E_0 = this._FormatTags) == null)
                {
                    arg_1E_0 = (this._FormatTags = this.GetTags("/ffprobe/format/tag"));
                }
                return arg_1E_0;
            }
        }

        public MediaInfo.StreamInfo[] Streams
        {
            get
            {
                MediaInfo.StreamInfo[] streamArr;
                if ((streamArr = this._Streams) == null)
                {
                    streamArr = (this._Streams = this.GetStreams());
                }
                return streamArr;
            }
        }

        public TimeSpan Duration
        {
            get
            {
                string attrValue = this.GetAttrValue("/ffprobe/format/@duration");
                TimeSpan result;
                if (!string.IsNullOrEmpty(attrValue) && TimeSpan.TryParse(attrValue, out result))
                {
                    return result;
                }
                return TimeSpan.Zero;
            }
        }

        public XPathDocument Result
        {
            get;
            private set;
        }

        public MediaInfo(XPathDocument ffProbeResult)
        {
            this.Result = ffProbeResult;
        }

        public string GetAttrValue(string xpath)
        {
            XPathNavigator xPathNavigator = this.Result.CreateNavigator();
            XPathNavigator xPathNavigator2 = xPathNavigator.SelectSingleNode(xpath);
            if (xPathNavigator2 == null)
            {
                return null;
            }
            return xPathNavigator2.Value;
        }

        private KeyValuePair<string, string>[] GetTags(string xpath)
        {
            XPathNavigator xPathNavigator = this.Result.CreateNavigator();
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            XPathNodeIterator xPathNodeIterator = xPathNavigator.Select(xpath);
            while (xPathNodeIterator.MoveNext())
            {
                list.Add(new KeyValuePair<string, string>(xPathNodeIterator.Current.GetAttribute("key", string.Empty), xPathNodeIterator.Current.GetAttribute("value", string.Empty)));
            }
            return list.ToArray();
        }

        private MediaInfo.StreamInfo[] GetStreams()
        {
            List<MediaInfo.StreamInfo> list = new List<MediaInfo.StreamInfo>();
            XPathNavigator xPathNavigator = this.Result.CreateNavigator();
            XPathNodeIterator xPathNodeIterator = xPathNavigator.Select("/ffprobe/streams/stream/@index");
            while (xPathNodeIterator.MoveNext())
            {
                list.Add(new MediaInfo.StreamInfo(this, xPathNodeIterator.Current.Value));
            }
            return list.ToArray();
        }
    }
}
