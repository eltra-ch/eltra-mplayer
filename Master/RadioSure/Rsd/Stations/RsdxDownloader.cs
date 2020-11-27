using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using EltraCommon.Logger;

namespace RadioSureMaster.Rsd
{
    class RsdxDownloader
    {
        public RsdxDownloader()
        {
            RsdxUrl = "http://radiosure.com/rsdbms/stations.rsdx";
        }

        public string RsdxUrl { get; set; }
        
        public async Task<string> GetCurrentDownloadNameAsync()
        {
            string result = string.Empty;

            try
            {
                var rsdxXmlContent = await DownloadRsdxXml();

                if (!string.IsNullOrEmpty(rsdxXmlContent))
                {
                    result = GetRsdxXmlDownloadName(rsdxXmlContent);
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - GetCurrentDownloadNameAsync", "xml content is empty!");
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - GetCurrentDownloadNameAsync", e);
            }

            return result;
        }

        private string GetRsdxXmlDownloadName(string rsdxXmlContent)
        {
            string result = string.Empty;

            try
            {
                var rsdxXmlDoc = new XmlDocument();

                rsdxXmlDoc.LoadXml(rsdxXmlContent);

                var downloadNameNode = rsdxXmlDoc.SelectSingleNode("RadioSure/Stations/BaseFile/DownloadName");

                if (downloadNameNode != null && !string.IsNullOrEmpty(downloadNameNode.InnerXml))
                {
                    result = downloadNameNode.InnerXml;
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - GetRsdxXmlDownloadName", "download name node not found!");
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - GetRsdxXmlDownloadName", e);
            }
            
            return result;
        }

        private async Task<string> DownloadRsdxXml()
        {
            string result = string.Empty;

            try
            {
                var client = new HttpClient();

                var rsdxXmlContent = await client.GetStringAsync(RsdxUrl);

                if (!string.IsNullOrEmpty(rsdxXmlContent))
                {
                    result = rsdxXmlContent;
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - DownloadRsdxXml", "download content is empty!");
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - DownloadRsdxXml", e);
            }

            return result;
        }
    }
}
