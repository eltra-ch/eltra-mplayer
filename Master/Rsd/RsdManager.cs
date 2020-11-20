using EltraCommon.Logger;
using MPlayerCommon.Contracts;
using MPlayerMaster.Rsd.Parser;
using MPlayerMaster.Rsd.Validator;
using System;
using System.Collections.Generic;
using System.IO;
using MPlayerMaster.Rsd.Models;
using Newtonsoft.Json;
using EltraConnector.Master.Device;

namespace MPlayerMaster.Rsd
{
    class RsdManager : IDisposable
    {
        #region Private fields

        private List<RadioStationModel> _radioStations;
        private RadioStationValidator _validator;

        #endregion

        #region Constructors

        public RsdManager()
        {
            MinQueryLength = 3;
            MaxRadioStationEntries = 25;
        }

        #endregion

        #region Properties

        public List<RadioStationModel> RadioStations
        {
            get => _radioStations ?? (_radioStations = CreateRadioStations());
        }

        public string RsdZipFile { get; set; }

        public int MinQueryLength { get; set; }

        public int MaxRadioStationEntries { get; set; }

        protected RadioStationValidator Validator
        {
            get => _validator ?? (_validator = new RadioStationValidator());
        }

        #endregion

        #region Methods

        protected virtual List<RadioStationModel> CreateRadioStations()
        {
            return new List<RadioStationModel>();
        }

        public void Init(MasterVcs vcs)
        {
            if (File.Exists(RsdZipFile))
            {
                var parser = new RsdFileParser() { SerializeToJsonFile = false };

                if (parser.ConvertRsdZipFileToJson(RsdZipFile))
                {
                    Validator.Stop();

                    InitRadioStations(parser.Output);

                    Validator.Vcs = vcs;
                    Validator.RadioStations = _radioStations;

                    Validator.Start();
                }
            }
        }

        private void InitRadioStations(List<RadioStationEntry> radioStationEntries)
        {
            RadioStations.Clear();

            foreach (var radioStationEntry in radioStationEntries)
            {
                var model = new RadioStationModel() { Entry = radioStationEntry };

                RadioStations.Add(model);
            }
        }

        public string QueryStation(string query)
        {
            string result = string.Empty;
            
            try
            {
                if (RadioStations != null && RadioStations.Count > 0 && query.Length > MinQueryLength)
                {
                    var radioStations = new List<RadioStationEntry>();

                    var queryWords = query.Split(new char[] { ' ', ';', ';' });

                    if (queryWords.Length > 0)
                    {
                        Validator.SearchActive = true;

                        foreach (var radioStation in RadioStations)
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

                        Validator.SearchActive = false;
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
