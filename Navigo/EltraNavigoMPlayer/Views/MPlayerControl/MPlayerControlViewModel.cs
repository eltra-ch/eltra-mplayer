﻿using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
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
       
        private XddParameter _volumeParameter;
        private XddParameter _muteParameter;
        private XddParameter _statusWordParameter;
        private XddParameter _relayStateParameter;
        private XddParameter _activeStationParameter;

        private Timer _valumeHistereseTimer;

        private List<MPlayerStationViewModel> _stationList;
        
        private double _stationUpdateProgressValue;
        private ushort _relayStateValue;

        #endregion

        #region Constructors

        public MPlayerControlViewModel()
        {
            string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            Title = $"MPlayer {assemblyVersion}";
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

        #endregion

        #region Commands 

        public ICommand TurnOffButtonCommand => new Command(OnTurnOffButtonPressed);

        public ICommand StopButtonCommand => new Command(OnStopButtonPressed);

        #endregion

        #region Events handling

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

        private void OnDeviceInitialized(object sender, EventArgs e)
        {
            IsBusy = true;

            InitAgentStatus();

            InitializeStateMachineParameter();

            InitializeRelayStateParameter();

            InitializeActiveStationParameter();

            InitializeVolumeParameter();

            InitializeMuteParameter();

            InitializeStationList();

            IsBusy = false;
        }

        private void UpdateTurnOffText()
        {
            if(RelayStateValue == 0)
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
            _relayStateParameter = Device.SearchParameter(0x3141, 0x01) as XddParameter;

            if (_relayStateParameter != null)
            {
                ushort state = 0;

                if (_relayStateParameter.GetValue(out state))
                {
                    RelayStateValue = state;
                }

                _relayStateParameter.ParameterChanged += OnRelayStateParameterChanged;

                _relayStateParameter.AutoUpdate();

                Task.Run(async () =>
                {
                    IsBusy = true;

                    await _relayStateParameter.UpdateValue();

                    IsBusy = false;
                }).ContinueWith((t) =>
                {
                    if (_relayStateParameter.GetValue(out state))
                    {
                        RelayStateValue = state;
                    }
                });
            }
        }

        private void InitializeStationList()
        {
            var stationList = new List<MPlayerStationViewModel>();
            var stationsCountParameter = Device.SearchParameter("PARAM_StationsCount") as XddParameter;

            if (stationsCountParameter != null && stationsCountParameter.GetValue(out ushort maxCount))
            {
                IsBusy = true;

                for (int i = 0; i < maxCount; i++)
                {
                    var stationViewModel = new MPlayerStationViewModel(this, i)
                    {
                        Agent = Agent,
                        Device = Device,
                        ActiveStationParameter = _activeStationParameter
                    };

                    stationList.Add(stationViewModel);
                }

                IsBusy = false;
            }

            StationList = stationList;
        }

        private void InitAgentStatus()
        {
            Agent.StatusChanged -= OnAgentStatusChanged;
            Agent.StatusChanged += OnAgentStatusChanged;

            IsEnabled = (Agent.Status == AgentStatus.Bound);
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

        private void InitializeMuteParameter()
        {
            _muteParameter = Device.SearchParameter(0x4201, 0x00) as XddParameter;

            if (_muteParameter != null)
            {
                bool muteVal = false;

                if (_muteParameter.GetValue(out muteVal))
                {
                    IsMuteActive = muteVal;
                }

                _muteParameter.ParameterChanged += OnVolumeParameterChanged;
                _muteParameter.AutoUpdate();

                Task.Run(async () =>
                {
                    IsBusy = true;

                    await _muteParameter.UpdateValue();

                    IsBusy = false;
                }).ContinueWith((t) =>
                {
                    if (_muteParameter.GetValue(out muteVal))
                    {
                        IsMuteActive = muteVal;
                    }
                });
            }
        }

        private void InitializeVolumeParameter()
        {
            _volumeParameter = Device.SearchParameter(0x4200, 0x00) as XddParameter;

            if (_volumeParameter != null)
            {
                int volumeValue = 0;

                if (_volumeParameter.GetValue(out volumeValue))
                {
                    VolumeValue = volumeValue;
                }

                _volumeParameter.ParameterChanged += OnVolumeParameterChanged;
                _volumeParameter.AutoUpdate();

                Task.Run(async () =>
                {
                    IsBusy = true;

                    await _volumeParameter.UpdateValue();

                    IsBusy = false;
                }).ContinueWith((t) =>
                {
                    if (_volumeParameter.GetValue(out volumeValue))
                    {
                        VolumeValue = volumeValue;
                    }
                });
            }
        }

        private void InitializeStateMachineParameter()
        {
            _statusWordParameter = Device.SearchParameter("PARAM_StatusWord") as XddParameter;

            if (_statusWordParameter != null)
            {
                ushort val = 0;

                if (_statusWordParameter.GetValue(out val))
                {
                    StatusWordValue = val;
                }

                _statusWordParameter.ParameterChanged += OnStatusWordParameterChanged;

                _statusWordParameter.AutoUpdate();

                Task.Run(async () =>
                {
                    IsBusy = true;

                    await _statusWordParameter.UpdateValue();

                    IsBusy = false;
                }).ContinueWith((t) =>
                {
                    if (_statusWordParameter.GetValue(out val))
                    {
                        StatusWordValue = val;
                    }
                });
            }
        }

        private void InitializeActiveStationParameter()
        {
            _activeStationParameter = Device.SearchParameter(0x4100, 0x00) as XddParameter;

            if (_activeStationParameter != null)
            {
                _activeStationParameter.AutoUpdate();

                Task.Run(async () =>
                {
                    IsBusy = true;

                    await _activeStationParameter.UpdateValue();

                    IsBusy = false;
                });
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

                DeviceInitialized += OnDeviceInitialized;

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

        private void OnVolumeChanged()
        {
            if(_volumeParameter != null)
            {
                int volumeValue = Convert.ToInt32(VolumeValue);

                if(_volumeParameter.SetValue(volumeValue))
                {
                    Task.Run(async ()=> {

                        Debug.Print($"before write, new value = {volumeValue}\r\n");

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
        }

        public void SliderVolumeValueChanged(double newValue)
        {
            if(_volumeParameter != null && _volumeParameter.GetValue(out int volumeValue))
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

        #endregion
    }
}
