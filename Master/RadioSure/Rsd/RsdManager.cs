using EltraCommon.Logger;
using MPlayerCommon.Contracts;
using RadioSureMaster.Rsd.Validator;
using System;
using System.Collections.Generic;
using RadioSureMaster.Rsd.Models;
using Newtonsoft.Json;
using EltraConnector.Master.Device;

namespace RadioSureMaster.Rsd
{
    public class RsdManager : IDisposable
    {
        #region Private fields

        private RadioStationValidator _validator;
        private string _rsdFileName;

        #endregion

        #region Constructors

        public RsdManager()
        {
            MinQueryLength = 3;
            MaxRadioStationEntries = 25;
        }

        #endregion

        #region Properties

        public RadioStationEntriesModel RadioStationEntriesModel => Validator.RadioStationEntriesModel;

        public string RsdxUrl { get; set; }

        public int MinQueryLength { get; set; }

        public int MaxRadioStationEntries { get; set; }

        protected RadioStationValidator Validator
        {
            get => _validator ?? (_validator = CreateValidator());
        }

        public string RsdFileName
        {
            get => _rsdFileName;
            set
            {
                _rsdFileName = value;
                OnRsdFileNameChanged();
            }
        }

        #endregion

        #region Events

        public event EventHandler<string> RsdFileNameChanged;

        private void OnRsdFileNameChanged()
        {
            RsdFileNameChanged?.Invoke(this, RsdFileName);
        }

        #endregion

        #region Methods

        protected virtual RadioStationValidator CreateValidator()
        {
            var result = new RadioStationValidator();

            result.RsdFileNameChanged += (s, e) =>
            {
                RsdFileName = e;
            };

            return result;
        }

        public void Init(MasterVcs vcs)
        {
            Validator.RsdxUrl = RsdxUrl;
            Validator.Vcs = vcs;

            Validator.Start();            
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
