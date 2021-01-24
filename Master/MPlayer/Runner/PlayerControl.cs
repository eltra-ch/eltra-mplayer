using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraConnector.Master.Device;
using MPlayerMaster.Radio;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static MPlayerMaster.MPlayerDefinitions;

namespace MPlayerMaster.Runner
{
    class PlayerControl
    {
        #region Private fields

        private Parameter _volumeParameter;
        private XddParameter _muteParameter;
        private Parameter _statusWordParameter;

        private Parameter _controlWordParameter;

        private MPlayerRunner _runner;

        private RadioPlayer _radioPlayer;

        #endregion

        #region Properties

        public MPlayerSettings Settings { get; set; }

        public MasterVcs Vcs { get; internal set; }

        internal MPlayerRunner Runner => _runner ?? (_runner = CreateRunner());

        public RadioPlayer RadioPlayer
        {
            get => _radioPlayer;
            set
            {
                _radioPlayer = value;
                OnRadioPlayerChaned();
            }
        }

        #endregion

        #region Events

        public event EventHandler MPlayerProcessExited;

        #endregion

        #region Events handler

        private void OnRadioPlayerChaned()
        {
            RadioPlayer.Runner = Runner;
        }

        private void OnControlWordChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            ushort controlWordValue = 0;

            if (parameterValue != null && parameterValue.GetValue(ref controlWordValue))
            {

            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            MPlayerProcessExited?.Invoke(this, e);
        }

        private void OnVolumeChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            int currentValue = 0;

            if (parameterValue.GetValue(ref currentValue))
            {
                MsgLogger.WriteLine($"Volume Changed = {currentValue}");

                SetVolumeAsync(currentValue);
            }
        }

        private void OnMuteChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            bool currentValue = false;

            if (parameterValue.GetValue(ref currentValue))
            {
                MsgLogger.WriteLine($"Mute Changed = {currentValue}");

                SetMuteAsync(currentValue);
            }
        }

        #endregion

        #region Methods

        private MPlayerRunner CreateRunner()
        {
            var result = new MPlayerRunner
            {
                Settings = Settings
            };

            result.MPlayerProcessExited += OnProcessExited;

            return result;
        }

        internal async Task InitParameters()
        {
            InitStateMachine();

            await InitVolumeControl();
        }

        private void InitStateMachine()
        {
            _controlWordParameter = Vcs.SearchParameter("PARAM_ControlWord") as XddParameter;
            _statusWordParameter = Vcs.SearchParameter("PARAM_StatusWord") as XddParameter;

            if (_controlWordParameter != null)
            {
                _controlWordParameter.ParameterChanged += OnControlWordChanged;
            }

            if (!SetStatusWord(StatusWordEnums.Waiting))
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

        public bool GetObject(ushort objectIndex, byte objectSubindex, ref byte[] data)
        {
            bool result = false;

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
            
            return result;
        }

        public bool SetObject(ushort objectIndex, byte objectSubindex, byte[] data)
        {
            bool result = false;

            //PARAM_ControlWord
            if (objectIndex == 0x6040 && objectSubindex == 0x0)
            {
                var controlWordValue = BitConverter.ToUInt16(data, 0);

                MsgLogger.WriteLine($"new controlword value = {controlWordValue}");

                result = _controlWordParameter.SetValue(controlWordValue);
            }
            else if (objectIndex == 0x4200)
            {
                var volumeValue = BitConverter.ToInt32(data, 0);

                MsgLogger.WriteLine($"new volume value = {volumeValue}");

                result = _volumeParameter.SetValue(volumeValue);
            }
            else if (objectIndex == 0x4201)
            {
                var muteValue = BitConverter.ToBoolean(data, 0);

                MsgLogger.WriteLine($"new mute value = {muteValue}");

                result = _muteParameter.SetValue(muteValue);
            }
            
            return result;
        }

        public bool SetStatusWord(StatusWordEnums status)
        {
            bool result = false;

            if (_statusWordParameter != null)
            {
                result = _statusWordParameter.SetValue((ushort)status);
            }

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

                        SetStatusWord(StatusWordEnums.PendingExecution);

                        var startResult = Process.Start(startInfo);

                        SetStatusWord(startResult != null ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

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

                        SetStatusWord(StatusWordEnums.PendingExecution);

                        var startResult = Process.Start(startInfo);

                        MsgLogger.WriteFlow($"{GetType().Name} - SetMuteAsync", $"Mute request: {muteString}, result = {startResult != null}");

                        SetStatusWord(startResult != null ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);
                    }
                    catch (Exception e)
                    {
                        MsgLogger.Exception($"{GetType().Name} - SetMuteAsync", e);
                    }
                }
            });

            return result;
        }
        public int Start(string url)
        {
            return Runner.Start(url);
        }

        public bool Stop()
        {
            return Runner.Stop();
        }

        #endregion
    }
}
