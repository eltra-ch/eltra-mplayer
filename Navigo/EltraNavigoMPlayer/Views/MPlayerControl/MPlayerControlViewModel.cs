using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraXamCommon.Controls;
using System.Timers;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using EltraNavigoMPlayer.Views.MPlayerControl.Station;
using System.Windows.Input;
using EltraConnector.UserAgent.Definitions;
using static EltraNavigoMPlayer.Views.MPlayerControl.Converters.StatusWordToImageConverter;
using System.Reflection;
using Xamarin.Forms.Internals;

namespace EltraNavigoMPlayer.Views.MPlayerControl
{
    [Preserve(AllMembers = true)]
    public class MPlayerControlViewModel : XamToolViewModel
    {
        #region Private fields

        private double _volumeValue;
        private bool _isMuteActive;
        private bool _internalChange;
        private ushort _statusWordValue;
        private string _turnOffButonText;
        private int _activeStationValue;

        private XddParameter _volumeParameter;
        private XddParameter _muteParameter;
        private XddParameter _statusWordParameter;
        private XddParameter _relayStateParameter;
        private XddParameter _activeStationParameter;
        private XddParameter _stationsCountParameter;
        private Timer _valumeHistereseTimer;

        private List<MPlayerStationViewModel> _stationList;
        
        private double _stationUpdateProgressValue;
        private ushort _relayStateValue;

        #endregion

        #region Constructors

        public MPlayerControlViewModel()
        {
            Title = $"MPlayer";
            Uuid = "C999F6E2-1FF8-44E1-977C-5B8826E3B9CA";
            TurnOffButonText = "Turn Off";

            PropertyChanged += OnViewPropertyChanged;
        }

        #endregion

        #region Properties

        public List<MPlayerStationViewModel> StationList
        {
            get => _stationList ?? (_stationList = new List<MPlayerStationViewModel>());
            set => SetProperty(ref _stationList, value);
        }

        public ushort StatusWordValue
        {
            get => _statusWordValue;
            set => SetProperty(ref _statusWordValue, value);
        }

        public bool IsMuteActive
        {
            get => _isMuteActive;
            set => SetProperty(ref _isMuteActive, value);
        }

        public double VolumeValue
        {
            get => _volumeValue;
            set => SetProperty(ref _volumeValue, value);
        }

        public double StationUpdateProgressValue
        {
            get => _stationUpdateProgressValue;
            set => SetProperty(ref _stationUpdateProgressValue, value);
        }

        public ushort RelayStateValue
        {
            get => _relayStateValue;
            set => SetProperty(ref _relayStateValue, value);
        }

        public string TurnOffButonText
        {
            get => _turnOffButonText;
            set => SetProperty(ref _turnOffButonText, value);
        }

        public int ActiveStationValue
        {
            get => _activeStationValue;
            set => SetProperty(ref _activeStationValue, value);
        }

        #endregion

        #region Commands 

        public ICommand TurnOffButtonCommand => new Command(OnTurnOffButtonPressed);

        public ICommand StopButtonCommand => new Command(OnStopButtonPressed);

        #endregion

        #region Events handling

        private void OnActiveStationParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.Parameter is Parameter activeStationParameter)
            {
                if (activeStationParameter.GetValue(out int activeStationValue))
                {
                    ActiveStationValue = activeStationValue;
                }
            }
        }

        private void OnViewPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "RelayStateValue")
            {
                UpdateTurnOffText();
            }
        }

        private async void OnStopButtonPressed(object obj)
        {
            IsBusy = true;

            foreach (var station in StationList)
            {
                if (station.IsActiveStation)
                {
                    await station.TurnOff();
                }
            }

            IsBusy = false;
        }

        private async void OnTurnOffButtonPressed(object obj)
        {
            ushort newState;

            IsBusy = true;

            if (RelayStateValue == 0)
            {
                //is OFF you are going to turn ON
                newState = 1;
            }
            else
            {
                //ON -> turn off
                newState = 0;
            }
            
            if (_relayStateParameter != null && _relayStateParameter.SetValue(newState))
            {                
                if(await _relayStateParameter.Write())
                {
                    RelayStateValue = newState;
                }
            }

            IsBusy = false;
        }

        private void OnVolumeChanged()
        {
            if (_volumeParameter != null)
            {
                int volumeValue = Convert.ToInt32(VolumeValue);

                if (_volumeParameter.SetValue(volumeValue))
                {
                    UpdateVolumeAsync(volumeValue);
                }
            }
        }

        public void SliderVolumeValueChanged(double newValue)
        {
            if (_volumeParameter != null && _volumeParameter.GetValue(out int volumeValue))
            {
                int newIntValue = Convert.ToInt32(newValue);

                if (volumeValue != newIntValue)
                {
                    _volumeValue = Math.Round(newValue);

                    CreateVolumeHistereseTimer();
                }
            }
        }

        private void OnStationUpdateProgressParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.Parameter is Parameter stationUpdateProgressParameter)
            {
                if (stationUpdateProgressParameter.GetValue(out double stationUpdateProgress))
                {
                    StationUpdateProgressValue = stationUpdateProgress / 100;
                }
            }
        }

        protected override void OnInitialized()
        {
            IsBusy = true;

            UpdateAgentStatus();

            InitializeStateMachineParameter();

            InitializeRelayStateParameter();

            InitializeActiveStationParameter();

            InitializeVolumeParameter();

            InitializeMuteParameter();

            InitializeStationList();

            IsBusy = false;

            base.OnInitialized();
        }

        private void OnAgentStatusChanged(object sender, AgentStatusEventArgs e)
        {
            IsEnabled = (e.Status == AgentStatus.Bound);

            if (!IsEnabled)
            {
                StatusWordValue = (ushort)StatusWordEnums.Undefined;
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "IsMuteActive")
            {
                if (!_internalChange)
                {
                    Task.Run(async () =>
                    {
                        if (_muteParameter != null && _muteParameter.SetValue(IsMuteActive))
                        {
                            IsBusy = true;

                            if (!await _muteParameter.Write())
                            {
                                _internalChange = true;

                                IsMuteActive = !IsMuteActive;

                                _internalChange = false;
                            }

                            IsBusy = false;
                        }
                    });
                }
            }
        }

        private void UpdateTurnOffText()
        {
            if (RelayStateValue == 0)
            {
                TurnOffButonText = "Turn On";
            }
            else
            {
                TurnOffButonText = "Turn Off";
            }
        }

        private void InitializeRelayStateParameter()
        {
            _relayStateParameter = Device?.SearchParameter(0x3141, 0x01) as XddParameter;
        }

        private async Task UpdateRelayStateAsync()
        {
            if (_relayStateParameter != null)
            {
                await _relayStateParameter.UpdateValue();

                if (_relayStateParameter.GetValue(out ushort state))
                {
                    RelayStateValue = state;

                    UpdateTurnOffText();
                }
            }
        }

        private void OnVolumeParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if(e.Parameter is Parameter volumeParameter)
            {
                if (volumeParameter.GetValue(out int volumeValue))
                {
                    VolumeValue = volumeValue;
                }
            }
        }

        private void OnStatusWordParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.Parameter is Parameter statusWordParameter)
            {
                if (statusWordParameter.GetValue(out ushort val))
                {
                    StatusWordValue = val;
                }
            }
        }

        private void OnRelayStateParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.Parameter is Parameter relayStateParameter)
            {
                if (relayStateParameter.GetValue(out ushort val))
                {
                    RelayStateValue = val;
                }
            }
        }

        private void OnVolumeHistereseElapsed(object sender, ElapsedEventArgs e)
        {
            _valumeHistereseTimer.Stop();

            OnVolumeChanged();
        }

        #endregion

        #region Methods

        public override void SetUp()
        {
            if (CanSetUp)
            {
                Assembly assembly = GetType().GetTypeInfo().Assembly;
                var assemblyName = assembly.GetName();
                
                Image = ImageSource.FromResource($"{assemblyName.Name}.Resources.music_32px.png");

                UpdateViewModels = false;

                PropertyChanged += OnViewModelPropertyChanged;
            }

            base.SetUp();
        }

        private void CreateVolumeHistereseTimer()
        {
            if(_valumeHistereseTimer!=null)
            {
                _valumeHistereseTimer.Stop();
                _valumeHistereseTimer.Dispose();
            }

            _valumeHistereseTimer = new Timer(500);
            _valumeHistereseTimer.Elapsed += OnVolumeHistereseElapsed;
            _valumeHistereseTimer.Enabled = true;
            _valumeHistereseTimer.AutoReset = true;
        }

        private void InitializeStationList()
        {
            var stationList = new List<MPlayerStationViewModel>();
            
            _stationsCountParameter = Device?.SearchParameter("PARAM_StationsCount") as XddParameter;

            if (_stationsCountParameter != null && _stationsCountParameter.GetValue(out ushort maxCount))
            {
                IsBusy = true;

                for (int i = 0; i < maxCount; i++)
                {
                    var stationViewModel = new MPlayerStationViewModel(this, i)
                    {
                        ActiveStationParameter = _activeStationParameter
                    };

                    stationList.Add(stationViewModel);
                }

                IsBusy = false;
            }

            StationList = stationList;
        }

        private void UpdateAgentStatus()
        {
            if (Agent != null)
            {
                IsEnabled = (Agent.Status == AgentStatus.Bound);
            }
        }

        private void InitializeMuteParameter()
        {
            _muteParameter = Device?.SearchParameter(0x4201, 0x00) as XddParameter;
        }

        private async Task UpdateMuteParameterAsync()
        {
            if (_muteParameter != null)
            {
                await _muteParameter.UpdateValue();

                if (_muteParameter.GetValue(out bool muteVal))
                {
                    IsMuteActive = muteVal;
                }
            }
        }

        private void InitializeVolumeParameter()
        {
            _volumeParameter = Device?.SearchParameter(0x4200, 0x00) as XddParameter;
        }

        private async Task UpdateVolumeAsync()
        {
            if (_volumeParameter != null)
            {
                await _volumeParameter.UpdateValue();

                if (_volumeParameter.GetValue(out int volumeValue))
                {
                    VolumeValue = volumeValue;
                }
            }
        }

        private void InitializeStateMachineParameter()
        {
            _statusWordParameter = Device?.SearchParameter("PARAM_StatusWord") as XddParameter;
        }

        private async Task UpdateStatusWordAsync()
        {
            if (_statusWordParameter != null)
            {
                await _statusWordParameter.UpdateValue();

                if (_statusWordParameter.GetValue(out ushort val))
                {
                    StatusWordValue = val;
                }
            }
        }

        private void InitializeActiveStationParameter()
        {
            _activeStationParameter = Device?.SearchParameter(0x4100, 0x00) as XddParameter;
        }

        private bool GetActiveStationValue(out int activeStationValue)
        {
            bool result = false;

            activeStationValue = 0;

            if (_activeStationParameter != null && _activeStationParameter.GetValue(out int stationValue))
            {
                activeStationValue = stationValue;
                result = true;
            }

            return result;
        }

        private async Task UpdateActiveStationAsync()
        {
            if (_activeStationParameter != null)
            {
                await _activeStationParameter.UpdateValue();

                if(GetActiveStationValue(out int activeStationValue))
                {
                    ActiveStationValue = activeStationValue;
                }
            }
        }

        private void RegisterStationParameterEvent()
        {
            UnregisterStationParameterEvent();

            if (_activeStationParameter != null)
            {
                _activeStationParameter.ParameterChanged += OnActiveStationParameterChanged;
            }
        }

        private void UnregisterStationParameterEvent()
        {
            if (_activeStationParameter != null)
            {
                _activeStationParameter.ParameterChanged -= OnActiveStationParameterChanged;
            }
        }

        protected override async Task RegisterAutoUpdate()
        {
            await UnregisterAutoUpdate();

            if (_statusWordParameter != null)
            {
                _statusWordParameter.ParameterChanged += OnStatusWordParameterChanged;

                _statusWordParameter.AutoUpdate();
            }

            if (_volumeParameter != null)
            {
                _volumeParameter.ParameterChanged += OnVolumeParameterChanged;

                _volumeParameter.AutoUpdate();
            }

            if (_muteParameter != null)
            {
                _muteParameter.ParameterChanged += OnVolumeParameterChanged;

                _muteParameter.AutoUpdate();
            }

            if (_relayStateParameter != null)
            {
                _relayStateParameter.ParameterChanged += OnRelayStateParameterChanged;

                _relayStateParameter.AutoUpdate();
            }

            if (_activeStationParameter != null)
            {
                _activeStationParameter.AutoUpdate();
            }

            if (Agent != null)
            {
                Agent.StatusChanged += OnAgentStatusChanged;
            }

            RegisterStationParameterEvent();
        }

        protected override async Task UpdateAllControls()
        {
            IsBusy = true;

            await UpdateActiveStationAsync();

            await UpdateStatusWordAsync();

            await UpdateMuteParameterAsync();

            await UpdateRelayStateAsync();

            UpdateAgentStatus();

            await UpdateVolumeAsync();

            IsBusy = false;
        }

        protected override Task UnregisterAutoUpdate()
        {
            if (_statusWordParameter != null)
            {
                _statusWordParameter.ParameterChanged -= OnStatusWordParameterChanged;

                _statusWordParameter.StopUpdate();
            }

            if (_volumeParameter != null)
            {
                _volumeParameter.ParameterChanged -= OnVolumeParameterChanged;

                _volumeParameter.StopUpdate();
            }

            if (_muteParameter != null)
            {
                _muteParameter.ParameterChanged -= OnVolumeParameterChanged;

                _muteParameter.StopUpdate();
            }

            if (_relayStateParameter != null)
            {
                _relayStateParameter.ParameterChanged -= OnRelayStateParameterChanged;

                _relayStateParameter.StopUpdate();
            }

            if (_activeStationParameter != null)
            {
                _activeStationParameter.StopUpdate();
            }

            if (Agent != null)
            {
                Agent.StatusChanged -= OnAgentStatusChanged;
            }

            UnregisterStationParameterEvent();

            return base.UnregisterAutoUpdate();
        }

        private void UpdateVolumeAsync(int volumeValue)
        {
            if (_volumeParameter != null)
            {
                Task.Run(async () =>
                {
                    IsBusy = true;

                    if (await _volumeParameter.Write())
                    {
                        Debug.Print($"after write, new value = {volumeValue}\r\n");
                    }
                    else
                    {
                        Debug.Print($"after write failed, value = {volumeValue}\r\n");
                    }

                    IsBusy = false;
                });
            }
        }

        #endregion
    }
}
