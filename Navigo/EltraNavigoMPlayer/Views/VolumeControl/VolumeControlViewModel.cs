using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraConnector.UserAgent.Definitions;
using EltraUiCommon.Controls;
using EltraXamCommon.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;

namespace EltraNavigoMPlayer.Views.VolumeControl
{
    public class VolumeControlViewModel : XamToolViewModel
    {
        #region Private fields

        private double _volumeValue;
        private bool _isMuteActive;
        private bool _internalChange;

        private XddParameter _muteParameter;
        private XddParameter _volumeParameter;
        private Timer _valumeHistereseTimer;

        #endregion

        #region Constructors

        public VolumeControlViewModel(ToolViewBaseModel parent)
            : base(parent)
        {
            PropertyChanged += OnViewModelPropertyChanged;
        }

        #endregion

        #region Commands 

        #endregion

        #region Events

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsMuteActive")
            {
                if (!_internalChange)
                {
                    SetMuteActivityAsync();
                }
            }
        }

        #endregion

        #region Properties

        public double VolumeValue
        {
            get => _volumeValue;
            set => SetProperty(ref _volumeValue, value);
        }

        public bool IsMuteActive
        {
            get => _isMuteActive;
            set => SetProperty(ref _isMuteActive, value);
        }

        #endregion

        #region Events handling

        protected override void OnInitialized()
        {
            UpdateAgentStatus();

            InitializeMuteParameter(); 
            InitializeVolumeParameter();

            base.OnInitialized();
        }

        private void OnAgentStatusChanged(object sender, AgentStatusEventArgs e)
        {
            IsEnabled = (e.Status == AgentStatus.Bound);
        }

        private void OnVolumeParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.Parameter is Parameter volumeParameter)
            {
                if (volumeParameter.GetValue(out int volumeValue))
                {
                    VolumeValue = Math.Round((double)volumeValue, 1);
                }
            }
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
                    _volumeValue = Math.Round(newValue, 1);

                    CreateVolumeHistereseTimer();
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

        private void InitializeMuteParameter()
        {
            _muteParameter = Device?.SearchParameter(0x4201, 0x00) as XddParameter;
        }

        private void CreateVolumeHistereseTimer()
        {
            if (_valumeHistereseTimer != null)
            {
                _valumeHistereseTimer.Stop();
                _valumeHistereseTimer.Dispose();
            }

            _valumeHistereseTimer = new Timer(500);
            _valumeHistereseTimer.Elapsed += OnVolumeHistereseElapsed;
            _valumeHistereseTimer.Enabled = true;
            _valumeHistereseTimer.AutoReset = true;
        }

        protected override async Task RegisterAutoUpdate()
        {
            await UnregisterAutoUpdate();

            if (_muteParameter != null)
            {
                _muteParameter.ParameterChanged += OnVolumeParameterChanged;

                _muteParameter.AutoUpdate();
            }

            if (_volumeParameter != null)
            {
                _volumeParameter.ParameterChanged += OnVolumeParameterChanged;

                _volumeParameter.AutoUpdate();
            }
        }

        protected override Task UnregisterAutoUpdate()
        {
            if (_muteParameter != null)
            {
                _muteParameter.ParameterChanged -= OnVolumeParameterChanged;

                _muteParameter.StopUpdate();
            }
            
            if (_volumeParameter != null)
            {
                _volumeParameter.ParameterChanged -= OnVolumeParameterChanged;

                _volumeParameter.StopUpdate();
            }

            if (Agent != null)
            {
                Agent.StatusChanged -= OnAgentStatusChanged;
            }

            return base.UnregisterAutoUpdate();
        }

        protected override async Task UpdateAllControls()
        {
            await UpdateMuteParameterAsync();

            UpdateAgentStatus();

            await UpdateVolumeAsync();
        }

        private void UpdateAgentStatus()
        {
            if (Agent != null)
            {
                IsEnabled = (Agent.Status == AgentStatus.Bound);
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

        private async Task UpdateMuteParameterAsync()
        {
            IsBusy = true;

            if (_muteParameter != null)
            {
                await _muteParameter.UpdateValue();

                if (_muteParameter.GetValue(out bool muteVal))
                {
                    IsMuteActive = muteVal;
                }
            }

            IsBusy = false;
        }

        private void SetMuteActivityAsync()
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

        private void InitializeVolumeParameter()
        {
            _volumeParameter = Device?.SearchParameter(0x4200, 0x00) as XddParameter;
        }

        private async Task UpdateVolumeAsync()
        {
            IsBusy = true;

            if (_volumeParameter != null)
            {
                await _volumeParameter.UpdateValue();

                if (_volumeParameter.GetValue(out int volumeValue))
                {
                    VolumeValue = Math.Round((double)volumeValue, 1);
                }
            }

            IsBusy = false;
        }

        #endregion
    }
}
