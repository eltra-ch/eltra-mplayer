using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraCommon.Os.Linux;
using EltraConnector.Master.Device;
using MPlayerCommon.Contracts.Media;
using MPlayerCommon.Definitions;
using MPlayerMaster.Runner;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ThermoMaster.DeviceManager.Wrapper;
using static MPlayerMaster.MPlayerDefinitions;

namespace MPlayerMaster.Media
{
    class MediaPlayer : IDisposable
    {
        #region Private fields

        private bool disposedValue;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _workingTask = Task.CompletedTask;
        private MediaStore _mediaStore;
        private MediaPlanner _mediaPlanner;
        private PlayerControl _playerControl;

        //media
        private XddParameter _mediaDataParameter;
        private XddParameter _mediaDataCompressedParameter;
        
        private XddParameter _mediaControlState;
        private XddParameter _mediaControlStateDisplay;

        //relay
        private XddParameter _relayStateParameter;

        #endregion

        #region Constructors

        public MediaPlayer()
        {
            const double defaultUpdateIntervalInSec = 60;

            UpdateInterval = TimeSpan.FromSeconds(defaultUpdateIntervalInSec);
        }

        #endregion

        #region Properties

        private MediaPlanner MediaPlanner => _mediaPlanner ?? (_mediaPlanner = CreateMediaPlanner());

        public XddParameter StatusWordParameter { get; set; }

        public string MediaPath => Settings?.MediaPath;

        public TimeSpan UpdateInterval { get; set; }

        private MediaStore MediaStore => _mediaStore ?? (_mediaStore = new MediaStore());

        public PlayerControl PlayerControl
        { 
            get => _playerControl; 
            set
            {
                _playerControl = value;
                OnPlayerControlChanged();
            }
        }

        public MasterVcs Vcs { get; internal set; }

        public MPlayerSettings Settings { get; set; }

        #endregion

        #region Events handling

        private void OnRelayStateParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (_relayStateParameter != null && _relayStateParameter.GetValue(out ushort state))
            {
                Task.Run(() => {
                    SetRelayState(state);
                });
            }
        }

        private void OnPlayerControlChanged()
        {
            PlayerControl.MPlayerProcessExited += OnMPlayerProcessExited;
        }

        private void OnMPlayerProcessExited(object sender, EventArgs e)
        {
            var url = MediaPlanner.GetNextUrl();

            if (!string.IsNullOrEmpty(url))
            {
                PlayMedia(url);
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
        
            switch(state)
            {
                case MediaControlWordValue.Next:
                    NextMedia();
                    break;
                case MediaControlWordValue.Previous:
                    PreviousMedia();
                    break;
                case MediaControlWordValue.Stop:
                    StopMedia();
                    break;
            }
        }

        #endregion

        #region Methods

        private bool InitRelayState()
        {
            bool result = false;

            _relayStateParameter = Vcs.SearchParameter(0x3141, 0x01) as XddParameter;

            if (_relayStateParameter != null)
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

        private bool StopMedia()
        {
            SetStatusWord(StatusWordEnums.PendingExecution);

            MediaPlanner.ClearPlaylist();

            bool result = PlayerControl.Stop();

            SetStatusWord(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

            return result;
        }

        private bool NextMedia()
        {
            bool result = false;
            var url = MediaPlanner.GetNextUrl();

            if (!string.IsNullOrEmpty(url))
            {
                result = PlayMedia(url);
            }

            return result;
        }

        private bool PreviousMedia()
        {
            bool result = false;
            var url = MediaPlanner.GetPreviousUrl();

            if (!string.IsNullOrEmpty(url))
            {
                result = PlayMedia(url);
            }

            return result;
        }

        private MediaPlanner CreateMediaPlanner()
        {
            var mediaPlanner = new MediaPlanner() { MediaStore = MediaStore, Vcs = Vcs };

            return mediaPlanner;
        }

        internal bool Start()
        {
            bool result = false;
            
            if(Update())
            {
                _workingTask = Task.Run(async () => { await DoUpdate(); }, _cancellationTokenSource.Token);

                result = true;
            }

            return result;
        }

        private async Task DoUpdate()
        {
            const int minDelay = 250;

            while(!_cancellationTokenSource.IsCancellationRequested)
            {
                Update();

                var stopWatch = new Stopwatch();
                
                stopWatch.Start();

                while(stopWatch.Elapsed < UpdateInterval && !_cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(minDelay);
                }
            }            
        }

        internal bool Update()
        {
            bool result = false;
            
            if (_mediaDataParameter != null)
            {
                if (MediaStore.Build(MediaPath))
                {
                    var data = MediaStore.Serialize();

                    result = _mediaDataParameter.SetValue(data);
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - MediaPlayer", "Build failed!");
                }
            }
            else
            {
                MsgLogger.WriteError($"{GetType().Name} - MediaPlayer", "data parameter not specified");
            }

            return result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Cancel();

                    _workingTask.Wait();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {            
            Dispose(disposing: true);
        
            GC.SuppressFinalize(this);
        }

        

        internal async Task InitParameters()
        {
            InitRelayState();

            _mediaDataParameter = Vcs.SearchParameter("PARAM_Media_Data") as XddParameter;
            _mediaDataCompressedParameter = Vcs.SearchParameter("PARAM_Media_Data_Compressed") as XddParameter;
            
            _mediaControlState = Vcs.SearchParameter("PARAM_MediaControlState") as XddParameter;
            _mediaControlStateDisplay = Vcs.SearchParameter("PARAM_MediaControlStateDisplay") as XddParameter;
            
            if (_mediaControlState != null)
            {
                await _mediaControlState.UpdateValue();

                _mediaControlState.ParameterChanged += OnMediaControlStateParameterChanged;
            }

            if (_mediaControlStateDisplay != null)
            {
                await _mediaControlStateDisplay.UpdateValue();
            }

            await MediaPlanner.InitParameters();

            Start();
        }

        private bool SetStatusWord(StatusWordEnums status)
        {
            bool result = false;

            if (StatusWordParameter != null)
            {
                result = StatusWordParameter.SetValue((ushort)status);
            }

            return result;
        }

        public bool Play()
        {
            bool result = SetMediaControlWordValue(MediaControlWordValue.Play);          

            return result;
        }

        private bool PlayMedia(string url)
        {
            SetStatusWord(StatusWordEnums.PendingExecution);

            bool result = PlayerControl.Start(url) >= 0;

            SetStatusWord(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

            return result;
        }

        internal bool Stop()
        {
            var result = SetMediaControlWordValue(MediaControlWordValue.Stop);

            return result;
        }

        internal bool SetObject(ushort objectIndex, byte objectSubindex, byte[] data)
        {
            bool result = false;

            if (objectIndex == 0x3200)
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
                }
            }
            else if (objectIndex == 0x3201)
            {
                switch (objectSubindex)
                {
                    case 0x01:
                        {
                            ushort state = BitConverter.ToUInt16(data, 0);

                            if (_mediaControlState != null)
                            {
                                result = _mediaControlState.SetValue(state);
                            }
                        }
                        break;                    
                }
            }
            else if (objectIndex == 0x3141 && objectSubindex == 0x01)
            {
                ushort relayState = BitConverter.ToUInt16(data, 0);

                if (_relayStateParameter != null)
                {
                    result = _relayStateParameter.SetValue(relayState);
                }
            }

            if (!result)
            {
                result = MediaPlanner.SetObject(objectIndex, objectSubindex, data);
            }

            return result;
        }

        private bool SetMediaControlWordValue(MediaControlWordValue state)
        {
            bool result = false;

            if (_mediaControlState != null)
            {
                result = _mediaControlState.SetValue((ushort) state);
            }

            return result;
        }

        private bool GetRelayState(out ushort state)
        {
            bool result = false;
            int pinValue = 0;

            state = 0;

            MsgLogger.WriteDebug($"{GetType().Name} - GetChannelState", $"get channel - state ...");

            try
            {
                if (SystemHelper.IsLinux)
                {
                    EltraRelayWrapper.RelayRead((ushort)Settings.RelayGpioPin, ref pinValue);

                    state = (byte)pinValue;

                    MsgLogger.WriteDebug($"{GetType().Name} - GetChannelState", $"get channel, pin={Settings.RelayGpioPin} state success, value = {state}");

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

        private bool SetRelayState(ushort state)
        {
            bool result = false;
            int pinValue = 0;

            try
            {
                if (SystemHelper.IsLinux)
                {
                    MsgLogger.WriteFlow($"digital write - {Settings.RelayGpioPin} state = {state} ...");

                    EltraRelayWrapper.RelayWrite((ushort)Settings.RelayGpioPin, state);

                    EltraRelayWrapper.RelayRead((ushort)Settings.RelayGpioPin, ref pinValue);

                    if (pinValue == state)
                    {
                        MsgLogger.WriteFlow($"set channel, pin={Settings.RelayGpioPin} state -> {state} success");

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

        public bool GetObject(ushort objectIndex, byte objectSubindex, ref byte[] data)
        {
            bool result = false;

            if (objectIndex == 0x3141 && objectSubindex == 0x01)
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

            return result;
        }

        #endregion
    }
}
