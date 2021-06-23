using EltraCommon.Contracts.CommandSets;
using EltraCommon.Contracts.Users;
using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraConnector.Agent;
using MPlayerMaster.Device.Runner;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static MPlayerMaster.MPlayerDefinitions;

namespace MPlayerMaster.Device.Players.Radio
{
    class RadioPlayer : Player
    {
        #region Private fields

        private AgentConnector _agentConnector;
        private DeviceCommand _queryStationCommand;

        private readonly List<Parameter> _urlParameters;
        private readonly List<Parameter> _stationTitleParameters;
        private readonly List<Parameter> _streamTitleParameters;

        private readonly List<Parameter> _volumeScalingParameters;
        private readonly List<Parameter> _processIdParameters;
        private readonly List<Parameter> _customTitleParameters;

        private Parameter _activeStationParameter;
        private Parameter _stationsCountParameter;
        private ushort _maxStationsCount;
        private Task<bool> _setActiveStationAsyncTask;

        #endregion

        #region Constructors

        public RadioPlayer()
        {
            _urlParameters = new List<Parameter>();
            _stationTitleParameters = new List<Parameter>();
            _streamTitleParameters = new List<Parameter>();
            _volumeScalingParameters = new List<Parameter>();
            _processIdParameters = new List<Parameter>();
            _customTitleParameters = new List<Parameter>();
        }

        #endregion

        #region Properties

        public int ActiveStationValue
        {
            get
            {
                int result = -1;

                if (_activeStationParameter != null && _activeStationParameter.GetValue(out int activeStationValue))
                {
                    result = activeStationValue;
                }

                return result;
            }
        }

        public Parameter ActiveStationParameter => _activeStationParameter;

        public Parameter StationsCountParameter => _stationsCountParameter;

        public List<Parameter> ProcessIdParameters => _processIdParameters;

        public List<Parameter> StreamTitleParameters => _streamTitleParameters;

        public List<Parameter> CustomStationTitleParameters => _customTitleParameters;

        public List<Parameter> StationTitleParameters => _stationTitleParameters;

        #endregion

        #region Events handling

        private void OnActiveStationParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            int activeStationValue = 0;

            if (parameterValue.GetValue(ref activeStationValue))
            {
                MsgLogger.WriteLine($"Active Station Changed = {activeStationValue}");

                SetActiveStationAsync(activeStationValue);
            }
        }

        private void OnCustomStationTitleChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameter = e.Parameter;

            if (parameter != null && parameter.GetValue(out string customTitle))
            {
                if (!string.IsNullOrEmpty(customTitle))
                {
                    var stationTitleParameter = Vcs.SearchParameter(parameter.Index, 0x02) as XddParameter;

                    if (stationTitleParameter != null)
                    {
                        stationTitleParameter.SetValue(customTitle);
                    }
                }
            }
        }

        #endregion

        #region Methods

        internal async Task InitParameters()
        {
            ResetStreamLabels();

            InitStationList();

            await InitQueryStation();

            await InitActiveStation();
        }

        private async Task InitActiveStation()
        {
            _activeStationParameter = Vcs.SearchParameter("PARAM_ActiveStation") as Parameter;

            if (_activeStationParameter != null)
            {
                _activeStationParameter.ParameterChanged += OnActiveStationParameterChanged;

                await _activeStationParameter.UpdateValue();
            }
        }

        private void InitStationList()
        {
            _stationsCountParameter = Vcs.SearchParameter("PARAM_StationsCount") as XddParameter;

            if (_stationsCountParameter != null && _stationsCountParameter.GetValue(out ushort maxCount))
            {
                _maxStationsCount = maxCount;

                for (ushort i = 0; i < maxCount; i++)
                {
                    ushort index = (ushort)(0x4000 + i);

                    var urlParameter = Vcs.SearchParameter(index, 0x01) as XddParameter;
                    var stationTitleParameter = Vcs.SearchParameter(index, 0x02) as XddParameter;
                    var streamTitleParameter = Vcs.SearchParameter(index, 0x03) as XddParameter;
                    var valumeScalingParameter = Vcs.SearchParameter(index, 0x04) as XddParameter;
                    var processIdParameter = Vcs.SearchParameter(index, 0x05) as XddParameter;
                    var customTitleParameter = Vcs.SearchParameter(index, 0x06) as XddParameter;

                    if (urlParameter != null &&
                        stationTitleParameter != null &&
                        streamTitleParameter != null &&
                        valumeScalingParameter != null &&
                        processIdParameter != null &&
                        customTitleParameter != null)
                    {
                        urlParameter.UpdateValue();
                        stationTitleParameter.UpdateValue();
                        streamTitleParameter.UpdateValue();
                        valumeScalingParameter.UpdateValue();
                        processIdParameter.UpdateValue();
                        customTitleParameter.UpdateValue();

                        _urlParameters.Add(urlParameter);
                        _stationTitleParameters.Add(stationTitleParameter);
                        _streamTitleParameters.Add(streamTitleParameter);
                        _volumeScalingParameters.Add(valumeScalingParameter);
                        _processIdParameters.Add(processIdParameter);
                        _customTitleParameters.Add(customTitleParameter);

                        customTitleParameter.ParameterChanged += OnCustomStationTitleChanged;

                        if (urlParameter.GetValue(out string url))
                        {
                            PlayerControl.CreateFifo((ushort)(i), url);
                        }
                        else
                        {
                            MsgLogger.WriteError($"{GetType().Name} - InitStationList", "get url parameter value failed!");
                        }
                    }
                }
            }
        }

        private void SetEmptyStreamLabel(ushort stationIndex)
        {
            if (_streamTitleParameters.Count > stationIndex)
            {
                var streamParam = _streamTitleParameters[stationIndex];

                if (streamParam != null)
                {
                    streamParam.SetValue("-");
                }
            }
        }

        private bool StopPlaying()
        {
            bool result;

            PlayerControl.SetStatusWord(StatusWordEnums.PendingExecution);

            result = PlayerControl.Stop();

            PlayerControl.SetStatusWord(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

            return result;
        }

        private void ResetStreamLabels()
        {
            for (ushort i = 0; i < _maxStationsCount; i++)
            {
                SetEmptyStreamLabel(i);
            }
        }

        private void SetActiveStationAsync(int activeStationValue)
        {
            if (_setActiveStationAsyncTask == null || _setActiveStationAsyncTask.IsCompleted)
            {
                MsgLogger.WriteFlow($"{GetType().Name} - SetActiveStationAsync", $"set active station id = {activeStationValue}");

                _setActiveStationAsyncTask = Task.Run(() =>
                {
                    bool result = false;

                    if (activeStationValue == 0)
                    {
                        result = StopPlaying();
                    }
                    else if (_urlParameters.Count >= activeStationValue && activeStationValue > 0)
                    {
                        var urlParam = _urlParameters[activeStationValue - 1];
                        var processParam = _processIdParameters[activeStationValue - 1];

                        if (urlParam.GetValue(out string url))
                        {
                            PlayerControl.SetStatusWord(StatusWordEnums.PendingExecution);

                            //SetEmptyStreamLabel((ushort)(activeStationValue - 1));

                            result = PlayerControl.PlayUrl((ushort)(activeStationValue - 1), url);

                            PlayerControl.SetStatusWord(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);
                        }
                    }

                    return result;
                });
            }
            else
            {
                MsgLogger.WriteFlow($"{GetType().Name} - SetActiveStationAsync", $"another set active station task is running, id = {activeStationValue}");
            }
        }

        public bool GetObject(ushort objectIndex, byte objectSubindex, ref byte[] data)
        {
            bool result = false;

            if (objectIndex == 0x4100)
            {
                if (_activeStationParameter != null && _activeStationParameter.GetValue(out byte[] v))
                {
                    data = v;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= 0x4000 + _maxStationsCount && objectSubindex == 0x01
                      && _urlParameters.Count > 0)
            {
                if (_urlParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= 0x4000 + _maxStationsCount && objectSubindex == 0x02
                      && _stationTitleParameters.Count > 0)
            {
                if (_stationTitleParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= 0x4000 + _maxStationsCount && objectSubindex == 0x03
                      && _streamTitleParameters.Count > 0)
            {
                if (_streamTitleParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= 0x4000 + _maxStationsCount && objectSubindex == 0x04
                      && _volumeScalingParameters.Count > 0)
            {
                if (_volumeScalingParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= 0x4000 + _maxStationsCount && objectSubindex == 0x05
                      && _processIdParameters.Count > 0)
            {
                if (_processIdParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= 0x4000 + _maxStationsCount && objectSubindex == 0x06
                      && _customTitleParameters.Count > 0)
            {
                if (_customTitleParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }

            return result;
        }

        public bool SetObject(ushort objectIndex, byte objectSubindex, byte[] data)
        {
            bool result = false;

            if (objectIndex >= 0x4000 && objectIndex <= 0x4000 + _maxStationsCount
                    && objectSubindex == 0x01)
            {
                if (_urlParameters.Count > objectIndex - 0x4000)
                {
                    result = _urlParameters[objectIndex - 0x4000].SetValue(data);
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= 0x4000 + _maxStationsCount
                    && objectSubindex == 0x04)
            {
                if (_volumeScalingParameters.Count > objectIndex - 0x4000)
                {
                    result = _volumeScalingParameters[objectIndex - 0x4000].SetValue(data);
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= 0x4000 + _maxStationsCount
                    && objectSubindex == 0x06)
            {
                if (_customTitleParameters.Count > objectIndex - 0x4000)
                {
                    result = _customTitleParameters[objectIndex - 0x4000].SetValue(data);
                }
            }
            else if (objectIndex == 0x4100 && objectSubindex == 0x0)
            {
                var activeStationValue = BitConverter.ToInt32(data, 0);

                Console.WriteLine($"new active station value = {activeStationValue}");

                result = _activeStationParameter.SetValue(activeStationValue);
            }

            return result;
        }

        private async Task<string> ExecQueryStation(string query)
        {
            string result = "failure";

            if (_queryStationCommand != null)
            {
                _queryStationCommand.SetParameterValue("Query", query);

                var command = await _queryStationCommand.Execute();

                if (command != null && command.GetParameterValue("Result", ref query))
                {
                    result = query;
                }
            }

            return result;
        }

        public async Task<string> QueryStation(string query)
        {
            var result = string.Empty;

            var queryResult = await ExecQueryStation(query);

            if (queryResult != "failure")
            {
                result = queryResult;
            }
            else
            {
                if (await InitQueryStation())
                {
                    queryResult = await ExecQueryStation(query);

                    if (queryResult != "failure")
                    {
                        result = queryResult;
                    }
                }
            }

            return result;
        }

        private async Task<bool> InitQueryStation()
        {
            bool result = false;

            if (_agentConnector != null)
            {
                _agentConnector.Disconnect();
            }

            _agentConnector = new AgentConnector() { Host = Settings.Host };

            if (string.IsNullOrEmpty(MPlayerMaster.Settings.Default.RdsLoginName))
            {
                MPlayerMaster.Settings.Default.RdsLoginName = $"rds-{Guid.NewGuid()}@eltra.ch";
                MPlayerMaster.Settings.Default.RdsLoginPasswd = $"{Guid.NewGuid()}";
                MPlayerMaster.Settings.Default.Save();
            }

            if (await _agentConnector.SignIn(new UserIdentity() { Login = MPlayerMaster.Settings.Default.RdsLoginName, Password = MPlayerMaster.Settings.Default.RdsLoginPasswd, Role = "developer" }, true))
            {
                if (await _agentConnector.Connect(new UserIdentity() { Login = Settings.RadioSureLogin, Password = Settings.RadioSurePasswd, Role = "developer" }))
                {
                    var channels = await _agentConnector.GetChannels();

                    foreach (var channel in channels)
                    {
                        foreach (var device in channel.Devices)
                        {
                            var queryStationCommand = await device.GetCommand("QueryStation");

                            if (queryStationCommand != null)
                            {
                                _queryStationCommand = queryStationCommand;
                                result = true;
                                break;
                            }
                        }
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
