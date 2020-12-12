using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraConnector.UserAgent.Definitions;
using EltraUiCommon.Controls;
using EltraUiCommon.Controls.Parameters;
using EltraXamCommon.Controls;
using EltraXamCommon.Controls.Parameters;
using EltraXamCommon.Controls.Toast;
using MPlayerCommon.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;
using Prism.Services.Dialogs;
using MPlayerMaster.Views.Dialogs;
using System.ComponentModel;

namespace EltraNavigoMPlayer.Views.MPlayerControl.Station
{
    public class MPlayerStationViewModel : XamToolViewModel
    {
        #region Private fields

        private int _stationIndex;
        private int _previousStationIndex;
        private int _activeStationValue;
        private string _controlButtonText;
        private string _stationStreamTitle;
        private bool _isStationEditVisible;
        private ParameterEditViewModel _stationIdParameter;
        private ParameterEditViewModel _stationCustomTitleParameter;
        private ParameterEditViewModel _stationVolumeScalingParameter;
        private bool _isActiveStation;
        private XddParameter _activeStationParameter;
        private XddParameter _urlStationParameter;
        private XddParameter _labelStationParameter;
        private XddParameter _streamTitleStationParameter;
        
        #endregion

        #region Constructors

        public MPlayerStationViewModel(ToolViewBaseModel parent, int stationIndex)
            : base(parent)
        {
            _previousStationIndex = 0;
            _stationIndex = stationIndex;

            _stationIdParameter = new XamParameterEditViewModel(this, $"PARAM_Station_{stationIndex+1}_Id");   
            _stationVolumeScalingParameter = new XamParameterEditViewModel(this, $"PARAM_Station_{stationIndex+1}_VolumeScaling");
            _stationCustomTitleParameter = new XamParameterEditViewModel(this, $"PARAM_Station_{stationIndex + 1}_CustomTitle");

            _stationIdParameter.ShowLabel = false;
            _stationVolumeScalingParameter.ShowLabel = false;
            _stationCustomTitleParameter.ShowLabel = false;

            PropertyChanged += OnViewModelPropertyChanged;
        }

        #endregion

        #region Commands 

        public ICommand ControlButtonCommand => new Command(OnControlButtonPressed);
        
        public ICommand EditButtonCommand => new Command(OnEditButtonPressed);

        #endregion

        #region Events

        public event EventHandler<string> StationLabelChanged;
        
        public event EventHandler<string> StreamLabelChanged;

        #endregion

        #region Properties

        public int ActiveStationValue
        {
            get => _activeStationValue;
            set => SetProperty(ref _activeStationValue, value);
        }

        public XddParameter ActiveStationParameter
        {
            get => _activeStationParameter;
            set
            {
                if (_activeStationParameter != value)
                {
                    _activeStationParameter = value;

                    OnActiveStationParameterChanged();
                }
            }
        }

        public bool IsActiveStation
        {
            get => _isActiveStation;
            set => SetProperty(ref _isActiveStation, value);
        }

        public string ControlButtonText
        {
            get => _controlButtonText;
            set => SetProperty(ref _controlButtonText, value);
        }

        public string StationStreamTitle
        {
            get => _stationStreamTitle;
            set => SetProperty(ref _stationStreamTitle, value);
        }

        public bool IsStationEditVisible
        {
            get => _isStationEditVisible;
            set => SetProperty(ref _isStationEditVisible, value);
        }

        public ParameterEditViewModel StationIdParameter
        {
            get => _stationIdParameter;
        }

        public ParameterEditViewModel StationCustomTitleParameter
        {
            get => _stationCustomTitleParameter;
        }

        public ParameterEditViewModel StationVolumeScalingParameter
        {
            get => _stationVolumeScalingParameter;
        }

        public ICommand PerformSearch => new Command<string>((string query) =>
        {
            Task.Run(async () =>
            {
                IsBusy = true;

                var searchResults = new List<RadioStationEntry>();

                var queryStationCommand = await Device.GetCommand("QueryStation");

                if(queryStationCommand!=null)
                {
                    string queryResult = string.Empty;

                    queryStationCommand.SetParameterValue("Query", query);

                    var executeResult = await queryStationCommand.Execute();

                    if (executeResult != null)
                    {
                        executeResult.GetParameterValue("Result", ref queryResult);
                    }

                    if (!string.IsNullOrEmpty(queryResult))
                    {
                        searchResults = JsonConvert.DeserializeObject<List<RadioStationEntry>>(queryResult);                        
                    }
                }

                SearchResults = searchResults;

                IsBusy = false;
            });

        });

        private List<RadioStationEntry> _searchResults;

        public List<RadioStationEntry> SearchResults
        {
            get
            {
                return _searchResults;
            }
            set
            {
                _searchResults = value;
                OnPropertyChanged("SearchResults");
            }
        }

        #endregion

        #region Events handling

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "ActiveStationValue" ||
                e.PropertyName == "ControlButtonText" ||
                e.PropertyName == "StationStreamTitle")
            {
                if (IsActiveStation)
                {
                    if (e.PropertyName == "StationStreamTitle")
                    {
                        OnStreamLabelChaned(StationStreamTitle);
                    }

                    if (e.PropertyName == "ControlButtonText")
                    {
                        OnStationLabelChaned(ControlButtonText);
                    }
                }
            }
        }

        private void OnStationLabelChaned(string label)
        {
            StationLabelChanged?.Invoke(this, label);
        }

        private void OnStreamLabelChaned(string label)
        {
            StreamLabelChanged?.Invoke(this, label);
        }

        private void OnActiveStationParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.Parameter is Parameter activeStationParameter)
            {
                if (activeStationParameter.GetValue(out int activeStationValue))
                {
                    ActiveStationValue = activeStationValue;

                    IsActiveStation = activeStationValue == (_stationIndex + 1);
                }
            }
        }

        private void OnActiveStationParameterChanged()
        {
            RegisterStationParameterEvent();

            if (GetActiveStationValue(out var activeStationValue))
            {
                ActiveStationValue = activeStationValue;

                IsActiveStation = ActiveStationValue == (_stationIndex + 1);
            }
        }

        private void RegisterStationParameterEvent()
        {
            UnregisterStationParameterEvent();

            if (ActiveStationParameter != null)
            {
                ActiveStationParameter.ParameterChanged += OnActiveStationParameterChanged;
            }
        }

        private void UnregisterStationParameterEvent()
        {
            if (ActiveStationParameter != null)
            {
                ActiveStationParameter.ParameterChanged -= OnActiveStationParameterChanged;
            }
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

        private void OnStationIdWritten(object sender, ParameterWrittenEventArgs e)
        {
            if (e.Result)
            {
                StationIdParameter.InitModelData();

                OnControlButtonPressed();
            }
        }

        protected override void OnInitialized()
        {
            UpdateAgentStatus();

            InitializeStationParameter();

            base.OnInitialized();
        }

        private void OnAgentStatusChanged(object sender, AgentStatusEventArgs e)
        {
            IsEnabled = (e.Status == AgentStatus.Bound);
        }

        private void OnEditButtonPressed(object obj)
        {
            IsStationEditVisible = !IsStationEditVisible;
        }

        private void OnStationLabelParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.Parameter.GetValue(out string label))
            {
                ControlButtonText = label;

                if (IsActiveStation)
                {
                    OnStationLabelChaned(label);
                }
            }
        }
        private void OnStreamTitleStationLabelParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.Parameter.GetValue(out string label))
            {
                StationStreamTitle = label;

                if (IsActiveStation)
                {
                    OnStreamLabelChaned(label);
                }
            }
        }

        private void OnControlButtonPressed(object obj = null)
        {
            if (IsInitialized)
            {
                Task.Run(async () =>
                {
                    IsBusy = true;

                    await ActiveSelection(_stationIndex + 1);

                    IsBusy = false;
                });
            }
        }

        protected override void OnDialogClosed(object sender, IDialogResult dialogResult)
        {
            System.Diagnostics.Debug.Print("");
        }

        internal void OnSearchResultTapped(RadioStationEntry entry)
        {
            IsBusy = true;

            var parameters = new DialogParameters
                    {
                        { "entry", entry },
                        { "stationIdParameter", StationIdParameter.Parameter }
                    };

            ShowDialog(new StationDialogViewModel(), parameters);

            IsBusy = false;
        }

        #endregion

        #region Methods

        private void UpdateAgentStatus()
        {
            if (Agent != null)
            {
                IsEnabled = (Agent.Status == AgentStatus.Bound);
            }
        }

        public async Task TurnOff()
        {
            if (IsInitialized)
            {
                IsBusy = true;

                await ActiveSelection(0);

                IsBusy = false;
            }
        }

        protected override async Task RegisterAutoUpdate()
        {
            if (Agent != null)
            {
                Agent.StatusChanged += OnAgentStatusChanged;
            }

            if (StationIdParameter != null)
            {
                StationIdParameter.Written += OnStationIdWritten;

                await StationIdParameter.StartUpdate();
            }

            if (_labelStationParameter != null && _streamTitleStationParameter != null && _urlStationParameter != null)
            {
                _labelStationParameter.ParameterChanged += OnStationLabelParameterChanged;
                _streamTitleStationParameter.ParameterChanged += OnStreamTitleStationLabelParameterChanged;

                _labelStationParameter.AutoUpdate();
                _streamTitleStationParameter.AutoUpdate();
                _urlStationParameter.AutoUpdate();
            }
        }

        protected override async Task UnregisterAutoUpdate()
        {
            if (StationIdParameter != null)
            {
                StationIdParameter.Written -= OnStationIdWritten;

                await StationIdParameter.StopUpdate();
            }

            if (_labelStationParameter != null && _streamTitleStationParameter != null && _urlStationParameter != null)
            {
                _labelStationParameter.ParameterChanged -= OnStationLabelParameterChanged;
                _streamTitleStationParameter.ParameterChanged -= OnStreamTitleStationLabelParameterChanged;
                
                _labelStationParameter.StopUpdate();
                _streamTitleStationParameter.StopUpdate();
                _urlStationParameter.StopUpdate();
            }

            if (Agent != null)
            {
                Agent.StatusChanged -= OnAgentStatusChanged;
            }
        }

        private void InitializeStationParameter()
        {
            ushort index = (ushort)(0x4000 + (ushort)_stationIndex);

            if (Device != null)
            {
                _urlStationParameter = Device.SearchParameter(index, 0x01) as XddParameter;
                _labelStationParameter = Device.SearchParameter(index, 0x02) as XddParameter;
                _streamTitleStationParameter = Device.SearchParameter(index, 0x03) as XddParameter;
            }
        }

        protected override async Task UpdateAllControls()
        {            
            if (_urlStationParameter != null && _labelStationParameter != null && _streamTitleStationParameter != null)
            {
                UpdateActualStationValues();

                await _urlStationParameter.UpdateValue();
                await _labelStationParameter.UpdateValue();
                await _streamTitleStationParameter.UpdateValue();

                UpdateActualStationValues();
            }

            await base.UpdateAllControls();
        }

        private void UpdateActualStationValues()
        {
            if (_urlStationParameter != null && _labelStationParameter != null && _streamTitleStationParameter != null)
            {
                if (_urlStationParameter.GetValue(out string url))
                {
                    ControlButtonText = url;
                }

                if (_labelStationParameter.GetValue(out string label))
                {
                    ControlButtonText = label;
                }

                if (_streamTitleStationParameter.GetValue(out string streamLabel))
                {
                    StationStreamTitle = streamLabel;
                }
            }
        }

        private async Task<bool> ActiveSelection(int index)
        {
            bool result = false;

            IsBusy = true;

            if (_previousStationIndex != index)
            {
                result = await SetActiveStation(index);

                _previousStationIndex = index;
            }
            else
            {
                if (_previousStationIndex != 0 && index > 0)
                {
                    if (await SetActiveStation(0))
                    {
                        result = await SetActiveStation(index);
                    }
                }
                else if(index == 0)
                {
                    result = await SetActiveStation(0);
                }
            }

            IsBusy = false;

            return result;
        }

        private async Task<bool> SetActiveStation(int index)
        {
            bool result = false;

            if (ActiveStationParameter != null && ActiveStationParameter.SetValue(index))
            {
                IsBusy = true;

                if (!await ActiveStationParameter.Write())
                {
                    ToastMessage.ShortAlert($"Activate Button {index + 1} failed!");
                }
                else
                {
                    result = true;
                }

                IsBusy = false;
            }

            return result;
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
