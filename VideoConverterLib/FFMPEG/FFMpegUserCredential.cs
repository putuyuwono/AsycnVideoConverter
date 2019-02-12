using System.Security;

namespace VideoConverterLib
{
    public sealed class FFMpegUserCredential
    {
        public string UserName
        {
            get;
            private set;
        }

        public SecureString Password
        {
            get;
            private set;
        }

        public string Domain
        {
            get;
            private set;
        }

        public FFMpegUserCredential(string userName, SecureString password)
        {
            this.UserName = userName;
            this.Password = password;
        }

        public FFMpegUserCredential(string userName, SecureString password, string domain) : this(userName, password)
        {
            this.Domain = domain;
        }
    }
}
