using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraCommon.Os.Linux;
using MPlayerCommon.Contracts.Media;
using MPlayerCommon.Definitions;
using MPlayerMaster.Device.Runner;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MPlayerMaster.Device.Runner.Wrapper;
using static MPlayerMaster.MPlayerDefinitions;
using MPlayerMaster.Device.Contracts;
using MPlayerMaster.Extensions;

namespace MPlayerMaster.Device.Players.Media
{
    class MediaPlayer : Player
    {
        #region Private fields

        private bool disposedValue;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _workingTask = Task.CompletedTask;
        private MediaStore _mediaStore;
        private MediaPlanner _mediaPlanner;

        //media
        private XddParameter _mediaDataParameter;
        private XddParameter _mediaDataCompressedParameter;

        private XddParameter _mediaControlState;

        private XddParameter _mediaCompositionPlaying;

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

        #endregion

        #region Events handling

        private void OnRelayStateParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (_relayStateParameter != null && _relayStateParameter.GetValue(out ushort state))
            {
                Task.Run(() =>
                {
                    SetRelayState(state);
                });
            }
        }

        protected override void OnMPlayerProcessExited(object sender, EventArgs e)
        {
            MsgLogger.WriteFlow($"{GetType().Name} - OnMPlayerProcessExited", $"process exited, shuffle = {MediaPlanner.Shuffle}");

            if (GetMediaStatusWordValue(out var state) && state != MediaStatusWordValue.Stopping)
            {
                var composition = MediaPlanner.GetNextUrl();

                if (composition != null)
                {
                    MsgLogger.WriteFlow($"{GetType().Name} - OnMPlayerProcessExited", $"play composition {composition.GetTitle()}");

                    PlayMedia(composition);
                }
                else
                {
                    MsgLogger.WriteFlow($"{GetType().Name} - OnMPlayerProcessExited", $"no composition found");

                    SetMediaStatusWordValue(MediaStatusWordValue.Stopped);
                }
            }
            else
            {
                MsgLogger.WriteError($"{GetType().Name} - OnMPlayerProcessExited", $"getting status failed!");
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

        private void OnMediaDataStateParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            MediaPlanner.BuildPlaylist();
        }

        private void OnMediaControlStateChanged(MediaControlWordValue state)
        {
            MsgLogger.WriteFlow($"{GetType().Name} - OnMediaControlStateChanged", $"media control state changed, new state = {state}");


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

            MediaPlanner.SetPlayListToReady();

            _mediaCompositionPlaying?.SetValue(string.Empty);

            SetMediaStatusWordValue(MediaStatusWordValue.Stopping);

            bool result = PlayerControl.Stop(false);

            if (result)
            {
                SetMediaStatusWordValue(MediaStatusWordValue.Stopped);
            }

            SetStatusWord(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

            return result;
        }

        private bool CurrentMedia()
        {
            bool result = false;
            var composition = MediaPlanner.CurrentComposition;

            if (composition == null)
            {
                composition = MediaPlanner.GetNextUrl();
            }

            if (composition != null)
            {
                result = PlayMedia(composition);
            }

            return result;
        }

        private bool NextMedia()
        {
            bool result = false;
            var composition = MediaPlanner.GetNextUrl();

            if (composition != null)
            {
                result = PlayMedia(composition);
            }

            return result;
        }

        private bool PreviousMedia()
        {
            bool result = false;
            var composition = MediaPlanner.GetPreviousUrl();

            if (composition != null)
            {
                result = PlayMedia(composition);
            }

            return result;
        }

        private bool PauseMedia()
        {
            var composition = MediaPlanner.CurrentComposition;

            SetMediaStatusWordValue(MediaStatusWordValue.Stopping);

            bool result = PlayerControl.Stop(false);

            if (result)
            {
                if (composition != null)
                {
                    composition.State = PlayingState.Ready;
                }

                SetMediaStatusWordValue(MediaStatusWordValue.Stopped);
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

            if (Update())
            {
                _workingTask = Task.Run(async () => { await DoUpdate(); }, _cancellationTokenSource.Token);

                result = true;
            }

            return result;
        }

        private async Task DoUpdate()
        {
            const int minDelay = 250;

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                Update();

                var stopWatch = new Stopwatch();

                stopWatch.Start();

                while (stopWatch.Elapsed < UpdateInterval && !_cancellationTokenSource.IsCancellationRequested)
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

            _mediaCompositionPlaying = Vcs.SearchParameter("PARAM_CompositionPlaying") as XddParameter;

            if (_mediaCompositionPlaying != null)
            {
                await _mediaCompositionPlaying.UpdateValue();
            }

            if (_mediaDataParameter != null)
            {
                await _mediaDataParameter.UpdateValue();

                _mediaDataParameter.ParameterChanged += OnMediaDataStateParameterChanged;
            }

            if (_mediaControlState != null)
            {
                await _mediaControlState.UpdateValue();
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

        internal bool ControlMedia(MediaControlWordValue state)
        {
            bool result = false;

            switch (state)
            {
                case MediaControlWordValue.Play:
                    if (CurrentMedia())
                    {
                        result = SetMediaStatusWordValue(MediaStatusWordValue.Playing);
                    }
                    else
                    {
                        MsgLogger.WriteError($"{GetType().Name} - ControlMedia", $"play current media failed!");
                    }
                    break;
                case MediaControlWordValue.Next:
                    if (NextMedia())
                    {
                        result = SetMediaStatusWordValue(MediaStatusWordValue.Playing);
                    }
                    else
                    {
                        MsgLogger.WriteError($"{GetType().Name} - ControlMedia", $"play next media failed!");
                    }
                    break;
                case MediaControlWordValue.Previous:
                    if (PreviousMedia())
                    {
                        result = SetMediaStatusWordValue(MediaStatusWordValue.Playing);
                    }
                    else
                    {
                        MsgLogger.WriteError($"{GetType().Name} - ControlMedia", $"play previous media failed!");
                    }
                    break;
                case MediaControlWordValue.Pause:
                    if (PauseMedia())
                    {
                        result = SetMediaStatusWordValue(MediaStatusWordValue.Stopped);
                    }
                    else
                    {
                        MsgLogger.WriteError($"{GetType().Name} - ControlMedia", $"pause media failed!");
                    }
                    break;
                case MediaControlWordValue.Stop:
                    if (StopMedia())
                    {
                        result = SetMediaStatusWordValue(MediaStatusWordValue.Stopped);
                    }
                    else
                    {
                        MsgLogger.WriteError($"{GetType().Name} - ControlMedia", $"stop media failed!");
                    }
                    break;
            }

            if (!result)
            {
                MsgLogger.WriteError($"{GetType().Name} - ControlMedia", $"setting state {state} failed!");
            }

            return result;
        }

        private bool PlayMedia(PlannerComposition composition)
        {
            bool result = false;

            SetStatusWord(StatusWordEnums.PendingExecution);

            if (composition != null)
            {
                _mediaCompositionPlaying?.SetValue(composition.GetTitle());

                result = PlayerControl.Start(composition.GetUrl()) >= 0;

                SetStatusWord(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);
            }

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

        private bool SetMediaStatusWordValue(MediaStatusWordValue state)
        {
            bool result = false;

            if (_mediaControlState != null)
            {
                result = _mediaControlState.SetValue((ushort)state);
            }

            return result;
        }

        private bool GetMediaStatusWordValue(out MediaStatusWordValue state)
        {
            bool result = false;

            state = MediaStatusWordValue.Unknown;

            if (_mediaControlState != null)
            {
                if (_mediaControlState.GetValue(out ushort s))
                {
                    state = (MediaStatusWordValue)s;
                    result = true;
                }
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
