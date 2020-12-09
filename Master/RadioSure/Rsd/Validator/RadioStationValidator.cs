using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraConnector.Master.Device;
using RadioSureMaster.Rsd.Models;
using RadioSureMaster.Rsd.Parser;

namespace RadioSureMaster.Rsd.Validator
{
    public class RadioStationValidator
    {
        #region Private fields

        private RdsWebClient _rdsWebClient = new RdsWebClient();
        private Task _validationTask;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private XddParameter _stationUpdateProgressParameter;
        private XddParameter _validationIntervalParameter;
        StationFile _stationFile;
        private string _rsdFileName;

        #endregion

        #region Constructors

        public RadioStationValidator()
        {
            ValidationInterval = TimeSpan.FromHours(24);
        }

        ~RadioStationValidator()
        {
            Stop();
        }

        #endregion

        #region Properties

        public string RsdxUrl { get; set; }

        public MasterVcs Vcs { get; set; }

        public TimeSpan ValidationInterval { get; set; }

        public RadioStationEntriesModel RadioStationEntriesModel { get; set; }

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

        public void Start()
        {
            const int minWaitTimeMs = 100;

            var cancellationToken = _cancellationTokenSource.Token;

            Stop();

            _stationUpdateProgressParameter = Vcs.SearchParameter("PARAM_StationUpdateProgress") as XddParameter;

            if (_stationUpdateProgressParameter == null)
            {
                MsgLogger.WriteError($"{GetType().Name} - Start", "station update progress parameter not found!");
                return;
            }

            _stationFile = new StationFile() { RsdxUrl = RsdxUrl };

            _validationTask = Task.Run(async () =>
            {

                do
                {
                    MsgLogger.WriteLine("update stations...");

                    await UpdateValidationInterval();

                    await UpdateStations();

                    var timeout = new Stopwatch();

                    timeout.Start();

                    while (!cancellationToken.IsCancellationRequested && timeout.Elapsed.TotalSeconds < ValidationInterval.TotalSeconds)
                    {
                        Thread.Sleep(minWaitTimeMs);
                    }
                }
                while (!cancellationToken.IsCancellationRequested);
            });
        }

        private async Task<bool> UpdateValidationInterval()
        {
            bool result = false;

            _validationIntervalParameter = Vcs.SearchParameter("PARAM_ValidationInterval") as XddParameter;

            if (_validationIntervalParameter != null)
            {
                await _validationIntervalParameter.UpdateValue();

                if (_validationIntervalParameter.GetValue(out byte validationIntervalValue))
                {
                    MsgLogger.WriteFlow($"{GetType().Name} - UpdateValidationInterval", $"validation interval = {validationIntervalValue} h");

                    ValidationInterval = TimeSpan.FromHours(validationIntervalValue);
                    
                    result = true;
                }
            }
            else
            {
                MsgLogger.WriteError($"{GetType().Name} - UpdateValidationInterval", "ValidationInterval parameter not found!");
            }

            return result;
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

        private Task UpdateStations()
        {
            var t = Task.Run(async () =>
            {
                MsgLogger.WriteLine("update radio station file");

                var fileName = await _stationFile.UpdateStationFile();

                MsgLogger.WriteLine($"update radio station file completed, result = {fileName}");

                return fileName;
            }).ContinueWith((t) =>
            {
                MsgLogger.WriteLine("update radio station model");

                if(UpdateRadioStationModel(t.Result))
                {
                    MsgLogger.WriteLine("do validation");

                    DoValidation(_cancellationTokenSource.Token);
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - UpdateStations", "update radio station model failed!");
                }
            });

            return t;
        }

        private bool UpdateRadioStationModel(string zipFileName)
        {
            bool result = false;

            if (!string.IsNullOrEmpty(zipFileName))
            {
                var parser = new RsdFileParser() { SerializeToJsonFile = false };

                parser.RsdFileNameChanged += (s, e) => 
                {
                    RsdFileName = e;
                };

                if (parser.ConvertRsdZipFileToJson(zipFileName))
                {
                    if (parser.Output != null)
                    {
                        RadioStationEntriesModel = parser.Output;

                        result = true;
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

            return result;
        }

        private void DoValidation(CancellationToken cancellationToken)
        {
            const int minWaitTimeMs = 10;

            if (RadioStationEntriesModel == null)
            {
                MsgLogger.WriteError($"{GetType().Name} - DoValidation", "model not found!");
                return;
            }

            int radioStationEntriesCount = RadioStationEntriesModel.Count;

            MsgLogger.WriteLine($"start validation ({radioStationEntriesCount}) ...");

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

                if(cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Thread.Sleep(minWaitTimeMs);

                counter++;
            }

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
                if (Math.Abs(progressPercent - progress) >= minStep)
                {
                    result = _stationUpdateProgressParameter.SetValue(progressPercent);
                }
            }

            return result;
        }

        #endregion
    }
}
