﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraConnector.Master.Device;
using MPlayerMaster.Rsd.Models;

namespace MPlayerMaster.Rsd.Validator
{
    class RadioStationValidator
    {
        #region Private fields

        private RdsWebClient _rdsWebClient = new RdsWebClient();
        private Task _validationTask;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private XddParameter _stationUpdateProgressParameter;

        #endregion

        public RadioStationValidator()
        {
            ValidationInterval = TimeSpan.FromHours(12);
        }

        ~RadioStationValidator()
        {
            Stop();
        }

        public MasterVcs Vcs { get; set; }

        public TimeSpan ValidationInterval { get; set; }

        public RadioStationEntriesModel RadioStationEntriesModel { get; set; }
        
        public void Start()
        {
            Stop();

            _validationTask = Task.Run(() => { DoValidation(_cancellationTokenSource.Token); }, _cancellationTokenSource.Token);
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();

            _validationTask?.Wait();

            _cancellationTokenSource = new CancellationTokenSource();
        }

        private bool IsUrlValid(string uriName)
        {
            bool result = Uri.TryCreate(uriName, UriKind.Absolute, out var uriResult)
                          && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            return result;
        }

        private bool IsReadableUri(string url)
        {
            bool result = false;

            try
            {
                if (IsUrlValid(url))
                {
                    using (var data = _rdsWebClient.OpenRead(url))
                    {
                        if (data != null && data.CanRead)
                        {
                            result = true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                result = false;
            }
            
            return result;
        }

        private void DoValidation(CancellationToken cancellationToken)
        {
            _stationUpdateProgressParameter = Vcs.SearchParameter("PARAM_StationUpdateProgress") as XddParameter;

            if(_stationUpdateProgressParameter == null)
            {
                MsgLogger.WriteError($"{GetType().Name} - DoValidation", "station update progress parameter not found!");
                return;
            }

            if (RadioStationEntriesModel == null)
            {
                MsgLogger.WriteError($"{GetType().Name} - DoValidation", "model not found!");
                return;
            }

            int radioStationEntriesCount = RadioStationEntriesModel.Count;

            MsgLogger.WriteLine($"start validation ({radioStationEntriesCount}) ...");

            const int minWaitTimeMs = 10;

            do
            {
                int counter = 0;

                foreach (var radioStation in RadioStationEntriesModel.Entries)
                {
                    var sinceLastValidation = DateTime.Now - radioStation.LastValidation;

                    double progressPercent = Math.Round((counter / (double)radioStationEntriesCount) * 100, 1);

                    if (sinceLastValidation > ValidationInterval)
                    {
                        ValidateRadioStation(radioStation, radioStationEntriesCount, counter, sinceLastValidation, progressPercent);
                    }

                    UpdateProgress(progressPercent);

                    Thread.Sleep(minWaitTimeMs);

                    counter++;
                }

                var timeout = new Stopwatch();

                timeout.Start();

                while (!cancellationToken.IsCancellationRequested && timeout.Elapsed < ValidationInterval)
                {
                    Thread.Sleep(minWaitTimeMs);
                }
            }
            while (!cancellationToken.IsCancellationRequested);

            MsgLogger.WriteLine("stop validation");
        }

        private void ValidateRadioStation(RadioStationModel radioStation, int radioStationEntriesCount, int counter, TimeSpan sinceLastValidation, double progressPercent)
        {
            var toRemove = new List<string>();

            if (radioStation.LastValidation == DateTime.MinValue)
            {
                MsgLogger.WriteLine($"first time validation, {counter}/{radioStationEntriesCount} {progressPercent} %");
            }
            else
            {
                MsgLogger.WriteLine($"last validation started for: {sinceLastValidation.TotalMinutes} min., {counter}/{radioStationEntriesCount} {progressPercent} %");
            }

            foreach (var url in radioStation.Urls)
            {
                if (IsReadableUri(url.Uri))
                {
                    url.IsValid = true;

                    radioStation.IsValid = true;

                    MsgLogger.WriteLine($"SUCCESS: url: '{url.Uri}', {counter}/{radioStationEntriesCount} {progressPercent} %");
                }
                else
                {
                    url.IsValid = false;

                    toRemove.Add(url.Uri);

                    MsgLogger.WriteLine($"#ERROR#: url: '{url.Uri}', {counter}/{radioStationEntriesCount} {progressPercent} %");
                }
            }

            foreach (var uri in toRemove)
            {
                if (radioStation.Entry.Urls.Contains(uri))
                {
                    radioStation.Entry.Urls.Remove(uri);
                }
            }

            if (radioStation.IsValid && radioStation.Entry.Urls.Count == 0)
            {
                radioStation.IsValid = false;
            }

            radioStation.LastValidation = DateTime.Now;
        }

        private bool UpdateProgress(double progressPercent)
        {
            const double minStep = 1.0;
            bool result = false;

            if (_stationUpdateProgressParameter.GetValue(out double progress))
            {
                if (progressPercent - progress >= minStep)
                {
                    result = _stationUpdateProgressParameter.SetValue(progressPercent);
                }
            }

            return result;
        }
    }
}
