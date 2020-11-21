using System;
using System.Collections.Generic;
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

        public List<RadioStationModel> RadioStations { get; set; }
        
        public bool SearchActive { get; set; }

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

            MsgLogger.WriteLine($"start validation ({RadioStations.Count}) ...");

            const int minWaitTime = 10;

            do
            {
                int counter = 0;

                foreach (var radioStation in RadioStations)
                {
                    var sinceLastValidation = DateTime.Now - radioStation.LastValidation;

                    double progressPercent = Math.Round((counter / (double)RadioStations.Count)*100, 1);

                    _stationUpdateProgressParameter.SetValue(progressPercent);

                    if (!SearchActive && sinceLastValidation > ValidationInterval)
                    {
                        var toRemove = new List<string>();

                        if (radioStation.LastValidation == DateTime.MinValue)
                        {
                            MsgLogger.WriteLine($"first time validation, {counter}/{RadioStations.Count} {progressPercent} %");
                        }
                        else
                        {
                            MsgLogger.WriteLine($"last validation started for: {sinceLastValidation.TotalMinutes} min., {counter}/{RadioStations.Count} {progressPercent} %");
                        }
                        
                        foreach (var url in radioStation.Urls)
                        {
                            if (IsReadableUri(url.Uri))
                            {
                                url.IsValid = true;

                                radioStation.IsValid = true;

                                MsgLogger.WriteLine($"SUCCESS: url: '{url.Uri}', {counter}/{RadioStations.Count} {progressPercent} %");
                            }
                            else
                            {
                                url.IsValid = false;

                                toRemove.Add(url.Uri);

                                MsgLogger.WriteLine($"#ERROR#: url: '{url.Uri}', {counter}/{RadioStations.Count} {progressPercent} %");
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

                    counter++;
                }

                Thread.Sleep(minWaitTime);
            }
            while (!cancellationToken.IsCancellationRequested);

            MsgLogger.WriteLine("stop validation");
        }
    }
}
