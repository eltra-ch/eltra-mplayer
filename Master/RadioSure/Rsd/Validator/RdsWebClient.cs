using System;
using System.Net;

namespace RadioSureMaster.Rsd.Validator
{
    class RdsWebClient : WebClient
    {
        public RdsWebClient()
        {
            Timeout = 3000;
        }

        public int Timeout { get; set; }

        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest result = base.GetWebRequest(uri);

            if (result != null)
            {
                result.Timeout = Timeout;
            }

            return result;
        }
    }
}
