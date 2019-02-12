namespace VideoConverterLib
{
    public class OutputSettings
    {
        public int? AudioSampleRate = null;

        public string AudioCodec;

        public int? VideoFrameRate = null;

        public int? VideoFrameCount = null;

        public string VideoFrameSize;

        public string VideoCodec;

        public float? MaxDuration = null;

        public string CustomOutputArgs;

        public void SetVideoFrameSize(int width, int height)
        {
            this.VideoFrameSize = string.Format("{0}x{1}", width, height);
        }

        internal void CopyTo(OutputSettings outputSettings)
        {
            outputSettings.AudioSampleRate = this.AudioSampleRate;
            outputSettings.AudioCodec = this.AudioCodec;
            outputSettings.VideoFrameRate = this.VideoFrameRate;
            outputSettings.VideoFrameCount = this.VideoFrameCount;
            outputSettings.VideoFrameSize = this.VideoFrameSize;
            outputSettings.VideoCodec = this.VideoCodec;
            outputSettings.MaxDuration = this.MaxDuration;
            outputSettings.CustomOutputArgs = this.CustomOutputArgs;
        }
    }
}
