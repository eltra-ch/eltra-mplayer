using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MPlayerMaster.Rsd.Models;

namespace MPlayerMaster.Rsd.Validator
{
    class RadioStationValidator
    {
        #region Private fields

        private RdsWebClient _rdsWebClient = new RdsWebClient();
        private Task _validationTask;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        #endregion

        public RadioStationValidator()
        {
            ValidationInterval = TimeSpan.FromHours(1);
        }

        ~RadioStationValidator()
        {
            Stop();
        }

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
            Console.WriteLine("start validation ...");

            do
            {
                foreach (var radioStation in RadioStations)
                {
                    var sinceLastValidation = DateTime.Now - radioStation.LastValidation;

                    if (!SearchActive && sinceLastValidation > ValidationInterval)
                    {
                        var toRemove = new List<string>();

                        if (radioStation.LastValidation == DateTime.MinValue)
                        {
                            Console.WriteLine("first time validation");
                        }
                        else
                        {
                            Console.WriteLine($"last validation started for: {sinceLastValidation.TotalMinutes} min.");
                        }
                        
                        foreach (var url in radioStation.Urls)
                        {
                            if (IsReadableUri(url.Uri))
                            {
                                url.IsValid = true;

                                radioStation.IsValid = true;

                                Console.WriteLine($"SUCCESS: url: '{url.Uri}' OK");
                            }
                            else
                            {
                                url.IsValid = false;

                                toRemove.Add(url.Uri);

                                Console.WriteLine($"#ERROR#: url: '{url.Uri}'");
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
                }
            }
            while (!cancellationToken.IsCancellationRequested);

            Console.WriteLine("stop validation");
        }
    }
}
