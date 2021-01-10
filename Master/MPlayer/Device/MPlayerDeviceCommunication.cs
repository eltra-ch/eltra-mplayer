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
using MPlayerMaster.Media;
using MPlayerCommon.Definitions;

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
        private XddParameter _relayStateParameter;
        private Parameter _controlWordParameter;
        private Parameter _stationsCountParameter;
        
        //media
        private XddParameter _mediaDataParameter;
        private XddParameter _mediaDataCompressedParameter;
        private XddParameter _mediaActiveArtistPositionParameter;
        private XddParameter _mediaActiveAlbumPositionParameter;
        private XddParameter _mediaActiveCompositionPositionParmeter;
        private XddParameter _mediaControlState;
        private XddParameter _mediaControlStateDisplay;
        private XddParameter _mediaControlShuffle;
        private XddParameter _mediaControlRandom;

        private MediaPlayer _mediaPlayer;

        private AgentConnector _agentConnector;

        private MPlayerSettings _settings;
        private ushort _maxStationsCount;
        private DeviceCommand _queryStationCommand;
        private MPlayerRunner _runner;
        private Task<bool> _setActiveStationAsyncTask;

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

        private MediaPlayer MediaPlayer => _mediaPlayer ?? (_mediaPlayer = CreateMediaPlayer());

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

        private MediaPlayer CreateMediaPlayer()
        {
            var result = new MediaPlayer()
            {
                MediaPath = _settings.MediaPath,
                DataParameter = _mediaDataParameter
            };

            return result;
        }

        protected override async void OnInitialized()
        {
            Console.WriteLine($"device (node id={Device.NodeId}) initialized, processing ...");

            InitStateMachine();

            InitRelayState();

            InitStationList();

            await InitMedia();

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

            if(string.IsNullOrEmpty(Settings.Default.RdsLoginName))
            {
                Settings.Default.RdsLoginName = $"rds-{Guid.NewGuid()}@eltra.ch";
                Settings.Default.RdsLoginPasswd = $"{Guid.NewGuid()}";
                Settings.Default.Save();
            }

            if (await _agentConnector.SignIn(new UserIdentity() { Login = Settings.Default.RdsLoginName, Password = Settings.Default.RdsLoginPasswd, Role = "developer" }, true))
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

            if (_controlWordParameter != null)
            {
                _controlWordParameter.ParameterChanged += OnControlWordChanged;
            }

            if (!SetExecutionStatus(StatusWordEnums.Waiting))
            {
                MsgLogger.WriteError($"{GetType().Name} - InitStateMachine", "Set execution state (waiting) failed!");
            }
        }

        private bool InitRelayState()
        {
            bool result = false;

            _relayStateParameter = Vcs.SearchParameter(0x3141, 0x01) as XddParameter;

            if(_relayStateParameter != null)
            {
                _relayStateParameter.ParameterChanged += OnRelayStateParameterChanged;

                if (GetRelayState(out ushort state))
                {
                    result = _relayStateParameter.SetValue(state);

                    if (!result)
                    {
                        MsgLogger.WriteError($"{GetType().Name} - InitRelayState", "set relay state failed!");
                    }
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - InitRelayState", "get relay state failed!");
                }
            }

            return result;
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

                SetActiveStationAsync(_activeStationParameter);
            }
        }

        private async Task InitMedia()
        {
            _mediaDataParameter = Vcs.SearchParameter("PARAM_Media_Data") as XddParameter;
            _mediaDataCompressedParameter = Vcs.SearchParameter("PARAM_Media_Data_Compressed") as XddParameter;
            _mediaActiveArtistPositionParameter = Vcs.SearchParameter("PARAM_ActiveArtistPosition") as XddParameter;
            _mediaActiveAlbumPositionParameter = Vcs.SearchParameter("PARAM_ActiveAlbumPosition") as XddParameter;
            _mediaActiveCompositionPositionParmeter = Vcs.SearchParameter("PARAM_ActiveCompositionPosition") as XddParameter;

            _mediaControlState = Vcs.SearchParameter("PARAM_MediaControlState") as XddParameter;
            _mediaControlStateDisplay = Vcs.SearchParameter("PARAM_MediaControlStateDisplay") as XddParameter;
            _mediaControlShuffle = Vcs.SearchParameter("PARAM_MediaControlShuffle") as XddParameter;
            _mediaControlRandom = Vcs.SearchParameter("PARAM_MediaControlRandom") as XddParameter;

            if (_mediaControlState != null)
            {
                await _mediaControlState.UpdateValue();

                _mediaControlState.ParameterChanged += OnMediaControlStateParameterChanged;
            }

            if (_mediaControlStateDisplay != null)
            {
                await _mediaControlStateDisplay.UpdateValue();
            }

            if (_mediaControlShuffle != null)
            {
                await _mediaControlShuffle.UpdateValue();

                _mediaControlShuffle.ParameterChanged += OnMediaControlShuffleParameterChanged;
            }

            if (_mediaControlRandom != null)
            {
                await _mediaControlRandom.UpdateValue();

                _mediaControlRandom.ParameterChanged += OnMediaControlRandomParameterChanged;
            }

            MediaPlayer.Start();
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
                        _urlParameters.Add(urlParameter);
                        _stationTitleParameters.Add(stationTitleParameter);
                        _streamTitleParameters.Add(streamTitleParameter);
                        _volumeScalingParameters.Add(valumeScalingParameter);
                        _processIdParameters.Add(processIdParameter);
                        _customTitleParameters.Add(customTitleParameter);

                        customTitleParameter.ParameterChanged += OnCustomStationTitleChanged;
                    }
                }
            }
        }

        #endregion

        #region Events

        private void OnControlWordChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            ushort controlWordValue = 0;

            if (parameterValue != null && parameterValue.GetValue(ref controlWordValue))
            {

            }
        }

        protected override void OnStatusChanged(DeviceCommunicationEventArgs e)
        {
            Console.WriteLine($"status changed, status = {e.Device.Status}, error code = {e.LastErrorCode}");

            base.OnStatusChanged(e);
        }

        private void OnRelayStateParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if(_relayStateParameter != null && _relayStateParameter.GetValue(out ushort state))
            {
                Task.Run(()=> {
                    SetRelayState(state);
                });                
            }
        }

        private void OnCustomStationTitleChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameter = e.Parameter;

            if(parameter != null && parameter.GetValue(out string customTitle))
            {
                if(!string.IsNullOrEmpty(customTitle))
                {
                    var stationTitleParameter = Vcs.SearchParameter(parameter.Index, 0x02) as XddParameter;

                    if(stationTitleParameter!=null)
                    {
                        stationTitleParameter.SetValue(customTitle);
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
                MsgLogger.WriteLine($"Active Station Changed = {activeStationValue}");

                SetActiveStationAsync(activeStationValue);
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

        private void OnMediaControlRandomParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            bool state = false;

            if (parameterValue != null && parameterValue.GetValue(ref state))
            {
                OnMediaControlRandomChanged(state);
            }
        }

        private void OnMediaControlShuffleParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            bool state = false;

            if (parameterValue != null && parameterValue.GetValue(ref state))
            {
                OnMediaControlShuffleChanged(state);
            }
        }

        private void OnMediaControlStateParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            ushort state = 0;

            if (parameterValue != null && parameterValue.GetValue(ref state))
            {
                OnMediaControlStateChanged((MediaControlWordValue)state);
            }
        }

        private void OnMediaControlStateChanged(MediaControlWordValue state)
        {
            MsgLogger.WriteFlow($"{GetType().Name} - OnMediaControlStateChanged", $"media control state changed, new state = {state}");
        }

        private void OnMediaControlShuffleChanged(bool state)
        {
            MsgLogger.WriteFlow($"{GetType().Name} - OnMediaControlShuffleChanged", $"shuffle state changed, new state = {state}");
        }

        private void OnMediaControlRandomChanged(bool state)
        {
            MsgLogger.WriteFlow($"{GetType().Name} - OnMediaControlRandomChanged", $"random state changed, new state = {state}");
        }

        #endregion

        #region Methods

        #region SDO

        public override bool GetObject(ushort objectIndex, byte objectSubindex, ref byte[] data)
        {
            bool result = base.GetObject(objectIndex, objectSubindex, ref data);

            //PARAM_ControlWord
            if (objectIndex == 0x6040 && objectSubindex == 0x0)
            {
                if (_controlWordParameter.GetValue(out byte[] v))
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
            else if (objectIndex == 0x3141 && objectSubindex == 0x01)
            {
                if (GetRelayState(out var state))
                {
                    data = BitConverter.GetBytes(state);

                    if (_relayStateParameter != null)
                    {
                        result = _relayStateParameter.SetValue(state);
                    }
                }
            }
            else if (objectIndex == 0x3200)
            {
                switch (objectSubindex)
                {
                    case 0x01:
                        if (_mediaDataParameter != null && _mediaDataParameter.GetValue(out byte[] d1))
                        {
                            data = d1;
                            result = true;
                        }
                        break;
                    case 0x02:
                        if (_mediaDataCompressedParameter != null && _mediaDataCompressedParameter.GetValue(out byte[] d2))
                        {
                            data = d2;
                            result = true;
                        }
                        break;
                    case 0x03:
                        if (_mediaActiveArtistPositionParameter != null && _mediaActiveArtistPositionParameter.GetValue(out byte[] d3))
                        {
                            data = d3;
                            result = true;
                        }
                        break;
                    case 0x04:
                        if (_mediaActiveAlbumPositionParameter != null && _mediaActiveAlbumPositionParameter.GetValue(out byte[] d4))
                        {
                            data = d4;
                            result = true;
                        }
                        break;
                    case 0x05:
                        if (_mediaActiveCompositionPositionParmeter != null && _mediaActiveCompositionPositionParmeter.GetValue(out byte[] d5))
                        {
                            data = d5;
                            result = true;
                        }
                        break;
                }
            }
            else if (objectIndex == 0x3201)
            {
                switch (objectSubindex)
                {
                    case 0x01:
                        if(_mediaControlState.GetValue(out byte[] d1))
                        {
                            data = d1;
                            result = true;
                        }                            
                        break;
                    case 0x02:
                        if (_mediaControlStateDisplay.GetValue(out byte[] d2))
                        {
                            data = d2;
                            result = true;
                        }
                        break;
                    case 0x03:
                        if (_mediaControlShuffle.GetValue(out byte[] d3))
                        {
                            data = d3;
                            result = true;
                        }
                        break;
                    case 0x04:
                        if (_mediaControlRandom.GetValue(out byte[] d4))
                        {
                            data = d4;
                            result = true;
                        }
                        break;
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
                ushort relayState = BitConverter.ToUInt16(data, 0);

                if (_relayStateParameter != null)
                {
                    result = _relayStateParameter.SetValue(relayState);
                }
            }
            else if (objectIndex == 0x3200)
            {
                switch (objectSubindex)
                {
                    case 0x01:
                        if (_mediaDataParameter != null && _mediaDataParameter.SetValue(data))
                        {
                            result = true;
                        }
                        break;
                    case 0x02:
                        bool val = BitConverter.ToBoolean(data, 0);
                        if (_mediaDataCompressedParameter != null && _mediaDataCompressedParameter.SetValue(val))
                        {
                            result = true;
                        }
                        break;
                    case 0x03:
                        int val1 = BitConverter.ToInt32(data, 0);
                        if (_mediaActiveArtistPositionParameter != null && _mediaActiveArtistPositionParameter.SetValue(val1))
                        {
                            result = true;
                        }
                        break;
                    case 0x04:
                        int val2 = BitConverter.ToInt32(data, 0);
                        if (_mediaActiveAlbumPositionParameter != null && _mediaActiveAlbumPositionParameter.SetValue(val2))
                        {
                            result = true;
                        }
                        break;
                    case 0x05:
                        int val3 = BitConverter.ToInt32(data, 0);
                        if (_mediaActiveCompositionPositionParmeter != null && _mediaActiveCompositionPositionParmeter.SetValue(val3))
                        {
                            result = true;
                        }
                        break;
                }
            }
            else if(objectIndex == 0x3201)
            {
                switch(objectSubindex)
                {
                    case 0x01:
                        {
                            ushort state = BitConverter.ToUInt16(data, 0);

                            if (_mediaControlState != null)
                            {
                                result = _mediaControlState.SetValue(state);
                            }
                        } break;
                    case 0x03:
                        {
                            bool state = BitConverter.ToBoolean(data, 0);

                            if (_mediaControlShuffle != null)
                            {
                                result = _mediaControlShuffle.SetValue(state);
                            }
                        }
                        break;
                    case 0x04:
                        {
                            bool state = BitConverter.ToBoolean(data, 0);

                            if (_mediaControlRandom != null)
                            {
                                result = _mediaControlRandom.SetValue(state);
                            }
                        }
                        break;
                }
            }

            return result;
        }

        #endregion

        internal bool StopMedia()
        {
            return StopPlaying();
        }

        internal bool PlayMedia()
        {
            bool result = false;

            SetExecutionStatus(StatusWordEnums.PendingExecution);

            if (_mediaActiveArtistPositionParameter != null &&
               _mediaActiveAlbumPositionParameter != null &&
               _mediaActiveCompositionPositionParmeter != null && 
               _mediaDataParameter != null)
            {
                _mediaActiveArtistPositionParameter.GetValue(out int activeArtistPosition);
                _mediaActiveAlbumPositionParameter.GetValue(out int activeAlbumPosition);
                _mediaActiveCompositionPositionParmeter.GetValue(out int activeCompositionPosition);

                string url = _mediaPlayer.GetUrl(activeArtistPosition, activeAlbumPosition, activeCompositionPosition);

                Runner.Stop();

                result = Runner.Start(url) >= 0;
            }

            SetExecutionStatus(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

            return result;
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
                            SetExecutionStatus(StatusWordEnums.PendingExecution);

                            SetEmptyStreamLabel((ushort)(activeStationValue - 1));

                            Runner.Stop();

                            result = Runner.Start(processParam, url);

                            SetExecutionStatus(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);
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

        private bool StopPlaying()
        {
            bool result;
            for (ushort i = 0; i < _maxStationsCount; i++)
            {
                SetEmptyStreamLabel(i);
            }

            SetExecutionStatus(StatusWordEnums.PendingExecution);

            result = Runner.Stop();

            SetExecutionStatus(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

            return result;
        }

        private bool SetActiveStationAsync(Parameter activeStation)
        {
            bool result = false;

            if (activeStation != null)
            {
                if (activeStation.GetValue(out int activeStationValue))
                {
                    SetActiveStationAsync(activeStationValue);

                    result = true;
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

        public bool GetRelayState(out ushort state)
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

        public bool SetRelayState(ushort state)
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
