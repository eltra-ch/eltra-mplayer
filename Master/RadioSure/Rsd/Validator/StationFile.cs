using EltraCommon.Logger;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace RadioSureMaster.Rsd.Validator
{
    class StationFile
    {
        private RsdxDownloader _rsdxDownloader;

        public StationFile()
        {
            
        }

        public string RsdxUrl { get; set; }

        protected RsdxDownloader RsdxDownloader
        {
            get => _rsdxDownloader ?? (_rsdxDownloader = new RsdxDownloader() { RsdxUrl = RsdxUrl });
        }

        public async Task<string> UpdateStationFile()
        {
            string tmpFullPath = string.Empty;

            try
            {
                string zipFileUrl = await RsdxDownloader.GetCurrentDownloadNameAsync();

                if (GetTempFileName(zipFileUrl, out var tmpPath))
                {
                    if (!File.Exists(tmpPath))
                    {
                        if (await DownloadFile(zipFileUrl, tmpPath))
                        {
                            tmpFullPath = tmpPath;
                        }
                        else
                        {
                            MsgLogger.WriteError($"{GetType().Name} - UpdateStationFile", $"download file {zipFileUrl} failed!");
                        }
                    }
                    else
                    {
                        tmpFullPath = tmpPath;
                    }
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - GetZipFileName", e);
            }

            return tmpFullPath;
        }

        private bool GetTempFileName(string url, out string tmpFullPath)
        {
            bool result = false;

            tmpFullPath = string.Empty;

            try
            {
                if (!string.IsNullOrEmpty(url))
                {
                    var uri = new Uri(url);
                    var filename = Path.GetFileName(uri.LocalPath);

                    tmpFullPath = Path.Combine(Path.GetTempPath(), filename);

                    result = true;
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - GetTempFileName", e);
            }

            return result;
        }

        private async Task<bool> DownloadFile(string url, string fileName)
        {
            bool result = false;

            try
            {
                var httpClient = new HttpClient();

                var stream = await httpClient.GetStreamAsync(url);

                using (var memoryStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memoryStream);

                    var byteArray = memoryStream.ToArray();

                    if (byteArray.Length > 0)
                    {
                        File.WriteAllBytes(fileName, byteArray);

                        result = true;
                    }
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - DownloadFile", e);
            }

            return result;
        }
    }
}
