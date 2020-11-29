using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraMaster.DeviceManager.Events;
using System;
using EltraConnector.Master.Device;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using System.Runtime.InteropServices;
using EltraCommon.Contracts.Users;

using static MPlayerMaster.MPlayerDefinitions;
using MPlayerMaster.Runner;
using EltraConnector.Agent;
using EltraCommon.Contracts.CommandSets;
using EltraCommon.Os.Linux;
using ThermoMaster.DeviceManager.Wrapper;

namespace MPlayerMaster.Device
{
    public class MPlayerDeviceCommunication : MasterDeviceCommunication
    {
        #region Private fields

        private readonly List<Parameter> _urlParameters;
        private readonly List<Parameter> _stationTitleParameters;
        private readonly List<Parameter> _streamTitleParameters;
        private readonly List<Parameter> _volumeScalingParameters;
        private readonly List<Parameter> _processIdParameters;
        private readonly List<Parameter> _customTitleParameters;

        private Parameter _activeStationParameter;
        private Parameter _volumeParameter;
        private XddParameter _muteParameter;
        private Parameter _statusWordParameter;
        
        private Parameter _controlWordParameter;
        private Parameter _stationsCountParameter;
        private AgentConnector _agentConnector;

        private MPlayerSettings _settings;
        private ushort _maxStationsCount;
        private DeviceCommand _queryStationCommand;
        private MPlayerRunner _runner;
        
        #endregion

        #region Constructors

        public MPlayerDeviceCommunication(MasterDevice device, MPlayerSettings settings)
            : base(device)
        {
            _settings = settings;

            _urlParameters = new List<Parameter>();
            _stationTitleParameters = new List<Parameter>();
            _streamTitleParameters = new List<Parameter>();
            _volumeScalingParameters = new List<Parameter>();
            _processIdParameters = new List<Parameter>();
            _customTitleParameters = new List<Parameter>();
        }

        #endregion

        #region Properties

        internal MPlayerRunner Runner => _runner ?? (_runner = CreateRunner());

        public int ActiveStationValue
        {
            get
            {
                int result = -1;

                if(_activeStationParameter != null && _activeStationParameter.GetValue(out int activeStationValue))
                {
                    result = activeStationValue;
                }

                return result;
            }
        }

        #endregion

        #region Init

        private MPlayerRunner CreateRunner()
        {
            var result = new MPlayerRunner
            {
                StationTitleParameters = _stationTitleParameters,
                ProcessIdParameters = _processIdParameters,
                StreamTitleParameters = _streamTitleParameters,
                StreamCustomTitleParameters = _customTitleParameters,
                StationsCountParameter = _stationsCountParameter,
                StatusWordParameter = _statusWordParameter,
                ActiveStationParameter = _activeStationParameter,

                Settings = _settings
            };

            return result;
        }

        protected override async void OnInitialized()
        {
            Console.WriteLine($"device (node id={Device.NodeId}) initialized, processing ...");

            InitStateMachine();

            InitializeStationList();

            await SetActiveStation();

            await InitVolumeControl();

            await InitQueryStation();

            base.OnInitialized();
        }

        private async Task<bool> InitQueryStation()
        {
            bool result = false;

            if(_agentConnector!=null)
            {
                _agentConnector.Disconnect();
            }

            _agentConnector = new AgentConnector() { Host = _settings.Host };

            if (await _agentConnector.SignIn(new UserIdentity() { Login = _settings.Alias, Password = _settings.AliasPasswd, Role = "developer" }))
            {
                if (await _agentConnector.Connect(new UserIdentity() { Login = _settings.RadioSureLogin, Password = _settings.RadioSurePasswd, Role = "developer" }))
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

        private void InitStateMachine()
        {
            _controlWordParameter = Vcs.SearchParameter("PARAM_ControlWord") as XddParameter;
            _statusWordParameter = Vcs.SearchParameter("PARAM_StatusWord") as XddParameter;

            if(!SetExecutionStatus(StatusWordEnums.Waiting))
            {
                MsgLogger.WriteError($"{GetType().Name} - InitStateMachine", "Set execution state (waiting) failed!");
            }
        }

        private async Task InitVolumeControl()
        {
            _muteParameter = Vcs.SearchParameter("PARAM_Mute") as XddParameter;

            if (_muteParameter != null)
            {
                _muteParameter.ParameterChanged += OnMuteChanged;

                await _muteParameter.UpdateValue();

                await SetMuteAsync(_muteParameter);
            }

            _volumeParameter = Vcs.SearchParameter("PARAM_Volume") as Parameter;

            if (_volumeParameter != null)
            {
                _volumeParameter.ParameterChanged += OnVolumeChanged;

                await _volumeParameter.UpdateValue();

                await SetVolumeAsync(_volumeParameter);
            }
        }

        private async Task SetActiveStation()
        {
            _activeStationParameter = Vcs.SearchParameter("PARAM_ActiveStation") as Parameter;
            
            if (_activeStationParameter != null)
            {
                _activeStationParameter.ParameterChanged += OnActiveStationParameterChanged;

                await _activeStationParameter.UpdateValue();

                await SetActiveStationAsync(_activeStationParameter);
            }
        }

        private void InitializeStationList()
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
                        _urlParameters.Add(urlParameter);
                        _stationTitleParameters.Add(stationTitleParameter);
                        _streamTitleParameters.Add(streamTitleParameter);
                        _volumeScalingParameters.Add(valumeScalingParameter);
                        _processIdParameters.Add(processIdParameter);
                        _customTitleParameters.Add(customTitleParameter);

                        customTitleParameter.ParameterChanged += OnCustomStreamTitleChanged;
                    }
                }
            }
        }

        #endregion

        #region Events

        private void OnCustomStreamTitleChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameter = e.Parameter;

            if(parameter != null && parameter.GetValue(out string customTitle))
            {
                if(!string.IsNullOrEmpty(customTitle))
                {
                    var streamTitleParameter = Vcs.SearchParameter(parameter.Index, 0x03) as XddParameter;

                    if(streamTitleParameter!=null)
                    {
                        streamTitleParameter.SetValue(customTitle);
                    }
                }
            }
        }

        private void OnActiveStationParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            int activeStationValue = 0;

            if(parameterValue.GetValue(ref activeStationValue))
            {
                Console.WriteLine($"Active Station Changed = {activeStationValue}");

                Task.Run(async ()=>
                {
                    await SetActiveStationAsync(activeStationValue);
                });
                
            }
        }
        private void OnVolumeChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            int currentValue = 0;

            if (parameterValue.GetValue(ref currentValue))
            {
                Console.WriteLine($"Volume Changed = {currentValue}");

                SetVolumeAsync(currentValue);
            }
        }

        private void OnMuteChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            bool currentValue = false;

            if (parameterValue.GetValue(ref currentValue))
            {
                Console.WriteLine($"Mute Changed = {currentValue}");

                SetMuteAsync(currentValue);
            }
        }

        #endregion

        #region SDO

        public override bool GetObject(ushort objectIndex, byte objectSubindex, ref byte[] data)
        {
            bool result = false;

            //PARAM_ControlWord
            if (objectIndex == 0x6040 && objectSubindex == 0x0)
            {
                if(_controlWordParameter.GetValue(out byte[] v))
                {
                    data = v;
                    result = true;
                }
            }
            else if (objectIndex == 0x6041)
            {
                if (_statusWordParameter.GetValue(out byte[] v))
                {
                    data = v;
                    result = true;
                }
            } 
            else if (objectIndex == 0x4100)
            {
                if (_activeStationParameter.GetValue(out byte[] v))
                {
                    data = v;
                    result = true;
                }
            }
            else if (objectIndex == 0x4200)
            {
                if (_volumeParameter.GetValue(out byte[] v))
                {
                    data = v;
                    result = true;
                }
            }
            else if (objectIndex == 0x4201)
            {
                if (_muteParameter.GetValue(out byte[] v))
                {
                    data = v;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= (0x4000 + _maxStationsCount) && objectSubindex == 0x01
                      && _urlParameters.Count > 0)
            {
                if (_urlParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= (0x4000 + _maxStationsCount) && objectSubindex == 0x02
                      && _stationTitleParameters.Count > 0)
            {
                if (_stationTitleParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= (0x4000 + _maxStationsCount) && objectSubindex == 0x03
                      && _streamTitleParameters.Count > 0)
            {
                if (_streamTitleParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= (0x4000 + _maxStationsCount) && objectSubindex == 0x04
                      && _volumeScalingParameters.Count > 0)
            {
                if (_volumeScalingParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= (0x4000 + _maxStationsCount) && objectSubindex == 0x05
                      && _processIdParameters.Count > 0)
            {
                if (_processIdParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= (0x4000 + _maxStationsCount) && objectSubindex == 0x06
                      && _customTitleParameters.Count > 0)
            {
                if (_customTitleParameters[objectIndex - 0x4000].GetValue(out byte[] d1))
                {
                    data = d1;
                    result = true;
                }
            }
            else if(objectIndex == 0x3141 && objectSubindex == 0x01)
            {
                if(GetChannelState(out var state))
                {
                    data = BitConverter.GetBytes(state);

                    result = true;
                }
            }

            return result;
        }

        public override bool SetObject(ushort objectIndex, byte objectSubindex, byte[] data)
        {
            bool result = false;

            //PARAM_ControlWord
            if (objectIndex == 0x6040 && objectSubindex == 0x0)
            {
                var controlWordValue = BitConverter.ToUInt16(data, 0);

                Console.WriteLine($"new controlword value = {controlWordValue}");

                result = _controlWordParameter.SetValue(controlWordValue);
            }
            else if (objectIndex >= 0x4000 && objectIndex <= (0x4000 + _maxStationsCount)
                    && objectSubindex == 0x01)
            {
                if (_urlParameters.Count > (objectIndex - 0x4000))
                {
                    result = _urlParameters[objectIndex - 0x4000].SetValue(data);
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= (0x4000 + _maxStationsCount)
                    && objectSubindex == 0x04)
            {
                if (_volumeScalingParameters.Count > (objectIndex - 0x4000))
                {
                    result = _volumeScalingParameters[objectIndex - 0x4000].SetValue(data);
                }
            }
            else if (objectIndex >= 0x4000 && objectIndex <= (0x4000 + _maxStationsCount)
                    && objectSubindex == 0x06)
            {
                if (_customTitleParameters.Count > (objectIndex - 0x4000))
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
            else if (objectIndex == 0x4200)
            {
                var volumeValue = BitConverter.ToInt32(data, 0);

                Console.WriteLine($"new volume value = {volumeValue}");

                result = _volumeParameter.SetValue(volumeValue);
            }
            else if (objectIndex == 0x4201)
            {
                var muteValue = BitConverter.ToBoolean(data, 0);

                Console.WriteLine($"new mute value = {muteValue}");

                result = _muteParameter.SetValue(muteValue);
            }
            else if (objectIndex == 0x3141 && objectSubindex == 0x01)
            {
                ushort channelState = BitConverter.ToUInt16(data, 0);

                result = SetChannelState(channelState);
            }

            return result;
        }

        #endregion

        #region Events

        protected override void OnStatusChanged(DeviceCommunicationEventArgs e)
        {
            Console.WriteLine($"status changed, status = {e.Device.Status}, error code = {e.LastErrorCode}");

            base.OnStatusChanged(e);
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

        private Task SetActiveStationAsync(int activeStationValue)
        {
            var result = Task.Run(() =>
            {
                if(activeStationValue == 0)
                {
                    for (ushort i = 0; i < _maxStationsCount; i++)
                    {
                        SetEmptyStreamLabel(i);
                    }

                    SetExecutionStatus(StatusWordEnums.PendingExecution);

                    bool result = Runner.Stop();

                    SetExecutionStatus(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);
                }
                else if (_urlParameters.Count >= activeStationValue && activeStationValue > 0)
                {
                    var urlParam = _urlParameters[activeStationValue - 1];
                    var processParam = _processIdParameters[activeStationValue - 1];
                    
                    if (urlParam.GetValue(out string url))
                    {
                        SetExecutionStatus(StatusWordEnums.PendingExecution);

                        SetEmptyStreamLabel((ushort)(activeStationValue - 1));

                        Runner.Stop();

                        var result = Runner.Start(processParam, url);

                        SetExecutionStatus(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);
                    }
                }
            });

            return result;
        }

        private Task SetActiveStationAsync(Parameter activeStation)
        {
            Task result = Task.CompletedTask;

            if (activeStation != null)
            {
                if (activeStation.GetValue(out int activeStationValue))
                {
                    result = SetActiveStationAsync(activeStationValue);
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - SetActiveStationAsync", "get activeStation parameter value failed!");
                }
            }
            else
            {
                MsgLogger.WriteError($"{GetType().Name} - SetActiveStationAsync", "activeStation parameter not defined!");
            }

            return result;
        }

        private Task SetVolumeAsync(Parameter parameter)
        {
            Task result = null;

            if (parameter != null)
            {
                if (parameter.GetValue(out int parameterValue))
                {
                    result = SetVolumeAsync(parameterValue);
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - SetVolumeAsync", "get volume parameter value failed!");
                }
            }
            else
            {
                MsgLogger.WriteError($"{GetType().Name} - SetVolumeAsync", "volume parameter not defined!");
            }

            return result;
        }

        private Task SetVolumeAsync(int volumeValue)
        {
            var result = Task.Run(() =>
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        string args = $"-D pulse sset Master {volumeValue}%";

                        var startInfo = new ProcessStartInfo("amixer");

                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        startInfo.Arguments = args;

                        SetExecutionStatus(StatusWordEnums.PendingExecution);

                        var startResult = Process.Start(startInfo);

                        SetExecutionStatus(startResult != null ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

                        MsgLogger.WriteFlow($"{GetType().Name} - SetVolumeAsync", $"Set Volume request: {volumeValue}, result = {startResult != null}");
                    }
                    catch (Exception e)
                    {
                        MsgLogger.Exception($"{GetType().Name} - SetVolumeAsync", e);
                    }
                }
            });

            return result;
        }

        private Task SetMuteAsync(Parameter parameter)
        {
            Task result = null;
            if (parameter != null)
            {
                if (parameter.GetValue(out bool parameterValue))
                {
                    result = SetMuteAsync(parameterValue);
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - SetVolumeAsync", "get volume parameter value failed!");
                }
            }
            else
            {
                MsgLogger.WriteError($"{GetType().Name} - SetVolumeAsync", "volume parameter not defined!");
            }

            return result;
        }

        private Task SetMuteAsync(bool muteValue)
        {
            var result = Task.Run(() =>
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        string muteString = muteValue ? "mute" : "unmute";

                        string args = $"-D pulse sset Master {muteString}";

                        var startInfo = new ProcessStartInfo("amixer")
                        {
                            WindowStyle = ProcessWindowStyle.Hidden,
                            Arguments = args
                        };

                        SetExecutionStatus(StatusWordEnums.PendingExecution);

                        var startResult = Process.Start(startInfo);

                        MsgLogger.WriteFlow($"{GetType().Name} - SetMuteAsync", $"Mute request: {muteString}, result = {startResult!=null}");

                        SetExecutionStatus(startResult != null ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);
                    }
                    catch (Exception e)
                    {
                        MsgLogger.Exception($"{GetType().Name} - SetMuteAsync", e);
                    }
                }
            });

            return result;
        }

        private bool SetExecutionStatus(StatusWordEnums status)
        {
            bool result = false;

            if(_statusWordParameter!=null)
            {
                result = _statusWordParameter.SetValue((ushort)status);
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

        public string QueryStation(string query)
        {
            var result = string.Empty;

            var t = Task.Run(async ()=> {

                var queryResult = await ExecQueryStation(query);

                if(queryResult != "failure")
                {
                    result = queryResult;
                }
                else
                {
                    if(await InitQueryStation())
                    {
                        queryResult = await ExecQueryStation(query);

                        if (queryResult != "failure")
                        {
                            result = queryResult;
                        }
                    }
                }                
            });

            t.Wait();

            return result;
        }

        public bool GetChannelState(out ushort state)
        {
            bool result = false;
            int pinValue = 0;

            state = 0;

            MsgLogger.WriteDebug($"{GetType().Name} - GetChannelState", $"get channel - state ...");

            try
            {
                if (SystemHelper.IsLinux)
                {
                    EltraRelayWrapper.RelayRead((ushort)_settings.RelayGpioPin, ref pinValue);

                    state = (byte)pinValue;

                    MsgLogger.WriteDebug($"{GetType().Name} - GetChannelState", $"get channel, pin={_settings.RelayGpioPin} state success, value = {state}");

                    result = true;
                }
                else
                {
                    MsgLogger.WriteLine(LogMsgType.Warning, "GPIO library is not supported on windows, simulate success");
                    result = true;
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - GetChannelState", e);
            }

            return result;
        }

        public bool SetChannelState(ushort state)
        {
            bool result = false;
            int pinValue = 0;

            try
            {
                if (SystemHelper.IsLinux)
                {
                    MsgLogger.WriteFlow($"digital write - {_settings.RelayGpioPin} state = {state} ...");

                    EltraRelayWrapper.RelayWrite((ushort)_settings.RelayGpioPin, state);

                    EltraRelayWrapper.RelayRead((ushort)_settings.RelayGpioPin, ref pinValue);

                    if (pinValue == state)
                    {
                        MsgLogger.WriteFlow($"set channel, pin={_settings.RelayGpioPin} state -> {state} success");

                        result = true;
                    }
                    else
                    {
                        MsgLogger.WriteError($"{GetType().Name} - SetChannelState", $"set channel - state -> {state} failed!");
                    }
                }
                else
                {
                    MsgLogger.WriteLine(LogMsgType.Warning, "GPIO library is not supported on windows, simulate success");
                    result = true;
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - SetChannelState", e);
            }
            
            return result;
        }

        #endregion
    }
}
