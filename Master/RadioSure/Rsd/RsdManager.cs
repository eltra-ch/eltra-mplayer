using EltraCommon.Logger;
using MPlayerCommon.Contracts;
using RadioSureMaster.Rsd.Parser;
using RadioSureMaster.Rsd.Validator;
using System;
using System.Collections.Generic;
using System.IO;
using RadioSureMaster.Rsd.Models;
using Newtonsoft.Json;
using EltraConnector.Master.Device;
using System.Threading.Tasks;
using System.Net.Http;

namespace RadioSureMaster.Rsd
{
    class RsdManager : IDisposable
    {
        #region Private fields

        private RadioStationValidator _validator;
        private RsdxDownloader _rsdxDownloader;

        #endregion

        #region Constructors

        public RsdManager()
        {
            MinQueryLength = 3;
            MaxRadioStationEntries = 25;
        }

        #endregion

        #region Properties

        public RadioStationEntriesModel RadioStationEntriesModel { get; set; }
        
        public string RsdxUrl { get; set; }

        public int MinQueryLength { get; set; }

        public int MaxRadioStationEntries { get; set; }

        protected RadioStationValidator Validator
        {
            get => _validator ?? (_validator = new RadioStationValidator());
        }

        #endregion

        #region Methods

        private void GetZipFileName()
        {
            Task.Run(async () => {

                string tmpFullPath = string.Empty;

                try
                {
                    string zipFileUrl = await _rsdxDownloader.GetCurrentDownloadNameAsync();

                    if (!string.IsNullOrEmpty(zipFileUrl))
                    {
                        var uri = new Uri(zipFileUrl);
                        string filename = string.Empty;

                        if (uri.IsFile)
                        {
                            filename = Path.GetFileName(uri.LocalPath);
                        }

                        tmpFullPath = Path.Combine(Path.GetTempPath(), filename);

                        var httpClient = new HttpClient();

                        var stream = await httpClient.GetStreamAsync(zipFileUrl);

                        using (var memoryStream = new MemoryStream())
                        {
                            await stream.CopyToAsync(memoryStream);

                            var byteArray = memoryStream.ToArray();

                            File.WriteAllBytes(tmpFullPath, byteArray);
                        }
                    }
                }
                catch(Exception e)
                {
                    MsgLogger.Exception($"{GetType().Name} - GetZipFileName", e);
                }

                return tmpFullPath;
            }).ContinueWith((t)=> 
            {
                var zipFileName = t.Result;

                if (!string.IsNullOrEmpty(zipFileName))
                {
                    var parser = new RsdFileParser() { SerializeToJsonFile = false };

                    if (parser.ConvertRsdZipFileToJson(zipFileName))
                    {
                        Validator.Stop();

                        if (parser.Output != null)
                        {
                            RadioStationEntriesModel = parser.Output;

                            Validator.RadioStationEntriesModel = RadioStationEntriesModel;

                            Validator.Start();
                        }
                        else
                        {
                            MsgLogger.WriteError($"{GetType().Name} - Init", "init model failed!");
                        }
                    }
                    else
                    {
                        MsgLogger.WriteError($"{GetType().Name} - Init", "parsing zip file failed!");
                    }
                }
            });            
        }

        public void Init(MasterVcs vcs)
        {
            _rsdxDownloader = new RsdxDownloader() { RsdxUrl = RsdxUrl };

            Validator.Vcs = vcs;

            GetZipFileName();            
        }

        public string QueryStation(string query)
        {
            string result = string.Empty;
            
            try
            {
                if (RadioStationEntriesModel != null && RadioStationEntriesModel.Count > 0 && query.Length > MinQueryLength)
                {
                    var radioStations = new List<RadioStationEntry>();

                    var queryWords = query.Split(new char[] { ' ', ';', ';' });

                    if (queryWords.Length > 0)
                    {
                        foreach (var radioStation in RadioStationEntriesModel.Entries)
                        {
                            if (radioStation.IsValid)
                            {
                                var contains = ContainsWord(queryWords, radioStation);

                                if (contains)
                                {
                                    radioStations.Add(radioStation.Entry);

                                    if (radioStations.Count > MaxRadioStationEntries)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    result = JsonConvert.SerializeObject(radioStations);
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - QueryStation", e);
            }

            return result;
        }

        private static bool ContainsWord(string[] queryWords, RadioStationModel radioStation)
        {
            bool contains = true;
            foreach (var queryWord in queryWords)
            {
                var search = new RadioStationEntrySearch(radioStation);

                if (!search.Contains(queryWord))
                {
                    contains = false;
                }
            }

            return contains;
        }

        #endregion

        #region Disposable

        public void Dispose()
        {
            Validator.Stop();
        }

        #endregion
    }
}
