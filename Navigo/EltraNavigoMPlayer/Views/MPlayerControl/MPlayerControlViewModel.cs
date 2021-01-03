using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraXamCommon.Controls;
using System.Threading.Tasks;
using Xamarin.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using EltraNavigoMPlayer.Views.MPlayerControl.Station;
using System.Windows.Input;
using EltraConnector.UserAgent.Definitions;
using System.Reflection;
using Xamarin.Forms.Internals;
using EltraNavigoMPlayer.Views.VolumeControl;

using static EltraNavigoMPlayer.Views.MPlayerControl.Converters.StatusWordToImageConverter;

namespace EltraNavigoMPlayer.Views.MPlayerControl
{
    [Preserve(AllMembers = true)]
    public class MPlayerControlViewModel : XamToolViewModel
    {
        #region Private fields
                
        private ushort _statusWordValue;
        private string _turnOffButonText;
        private int _activeStationValue;

        private XddParameter _statusWordParameter;
        private XddParameter _relayStateParameter;
        private XddParameter _activeStationParameter;
        private XddParameter _stationsCountParameter;
        
        private List<MPlayerStationViewModel> _stationList;
        
        private double _stationUpdateProgressValue;
        private ushort _relayStateValue;
        private string _actualStreamLabel;
        private string _actualStationLabel;
        private string _actualPlayingLabel;

        private VolumeControlViewModel _volumeControlViewModel;

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

        public VolumeControlViewModel VolumeControlViewModel
        {
            get => _volumeControlViewModel ?? (_volumeControlViewModel = CreateVolumeControlViewModel());
            set => SetProperty(ref _volumeControlViewModel, value);

        }

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

        public string ActualPlayingLabel
        {
            get => _actualPlayingLabel;
            set => SetProperty(ref _actualPlayingLabel, value);
        }

        #endregion

        #region Commands 

        public ICommand TurnOffButtonCommand => new Command(OnTurnOffButtonPressed);

        public ICommand StopButtonCommand => new Command(OnStopButtonPressed);

        #endregion

        #region Events handling

        private void OnStreamLabelChanged(object sender, string e)
        {
            _actualStreamLabel = e;

            UpdateActualPlayingLabel();
        }

        private void OnStationLabelChaned(object sender, string e)
        {
            _actualStationLabel = e;

            UpdateActualPlayingLabel();
        }

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
            IsBusy = true;

            if (_relayStateParameter != null)
            {
                await _relayStateParameter.UpdateValue();

                if (_relayStateParameter.GetValue(out ushort state))
                {
                    RelayStateValue = state;

                    UpdateTurnOffText();
                }
            }

            IsBusy = false;
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

        #endregion

        #region Methods

        private VolumeControlViewModel CreateVolumeControlViewModel()
        {
            var result = new VolumeControlViewModel(this);

            return result;
        }

        public override void SetUp()
        {
            if (CanSetUp)
            {
                Assembly assembly = GetType().GetTypeInfo().Assembly;
                var assemblyName = assembly.GetName();
                
                Image = ImageSource.FromResource($"{assemblyName.Name}.Resources.music_32px.png");

                UpdateViewModels = false;
            }

            base.SetUp();
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

                    stationViewModel.StationLabelChanged += OnStationLabelChaned;
                    stationViewModel.StreamLabelChanged += OnStreamLabelChanged;

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

        private void InitializeStateMachineParameter()
        {
            _statusWordParameter = Device?.SearchParameter("PARAM_StatusWord") as XddParameter;
        }

        private async Task UpdateStatusWordAsync()
        {
            IsBusy = true;

            if (_statusWordParameter != null)
            {
                await _statusWordParameter.UpdateValue();

                if (_statusWordParameter.GetValue(out ushort val))
                {
                    StatusWordValue = val;
                }
            }

            IsBusy = false;
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
            IsBusy = true;

            if (_activeStationParameter != null)
            {
                await _activeStationParameter.UpdateValue();

                if(GetActiveStationValue(out int activeStationValue))
                {
                    ActiveStationValue = activeStationValue;
                }
            }

            IsBusy = false;
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
            await UpdateActiveStationAsync();

            await UpdateStatusWordAsync();

            await UpdateRelayStateAsync();

            UpdateAgentStatus();
        }

        protected override Task UnregisterAutoUpdate()
        {
            if (_statusWordParameter != null)
            {
                _statusWordParameter.ParameterChanged -= OnStatusWordParameterChanged;

                _statusWordParameter.StopUpdate();
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

        private void UpdateActualPlayingLabel()
        {
            if(string.IsNullOrEmpty(_actualStreamLabel))
            {
                ActualPlayingLabel = $"{_actualStationLabel}";
            }
            else
            {
                ActualPlayingLabel = $"{_actualStationLabel} / {_actualStreamLabel}";
            }
        }

        protected override void GoingOnline()
        {
            IsEnabled = true;
        }

        protected override void GoingOffline()
        {
            IsEnabled = false;
        }

        #endregion
    }
}
