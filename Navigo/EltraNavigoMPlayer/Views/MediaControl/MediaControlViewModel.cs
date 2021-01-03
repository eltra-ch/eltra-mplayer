using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraXamCommon.Controls;
using System.Threading.Tasks;
using Xamarin.Forms;
using System.ComponentModel;
using System.Windows.Input;
using EltraConnector.UserAgent.Definitions;
using static EltraNavigoMPlayer.Views.MPlayerControl.Converters.StatusWordToImageConverter;
using System.Reflection;
using Xamarin.Forms.Internals;
using MPlayerCommon.Contracts.Media;
using EltraNavigoMPlayer.Views.VolumeControl;
using System;
using System.Collections.Generic;
using EltraUiCommon.Helpers;

namespace EltraNavigoMPlayer.Views.MediaControl
{
    [Preserve(AllMembers = true)]
    public class MediaControlViewModel : XamToolViewModel
    {
        #region Private fields

        private ushort _statusWordValue;
        private string _turnOffButonText;
        private int _activeStationValue;
        private bool _internalChange;
        private XddParameter _statusWordParameter;
        private XddParameter _relayStateParameter;
        private XddParameter _activeStationParameter;
        
        private XddParameter _mediaDataParameter;
        private XddParameter _compressedMediaParameter;
        private XddParameter _activeArtistPositionParameter;
        private XddParameter _activeAlbumPositionParameter;
        private XddParameter _activeCompositionPositionParameter;

        private MediaStore _mediaStore;
        private List<Artist> _artists;
        private List<Album> _albums;
        private List<Composition> _compositions;
        private Artist _activeArtist;
        private Album _activeAlbum;
        private Composition _activeComposition;
        private double _stationUpdateProgressValue;
        private ushort _relayStateValue;
        private string _actualPlayingLabel;

        private VolumeControlViewModel _volumeControlViewModel;

        #endregion

        #region Constructors

        public MediaControlViewModel()
        {
            Title = $"Media";
            Uuid = "CC3FDCDB-DC76-41E4-B19B-15882D3893B8";
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

        public ICommand PlayButtonCommand => new Command(OnPlayButtonPressed);

        public List<Artist> Artists 
        { 
            get => _artists; 
            set => SetProperty(ref _artists, value); 
        }

        public List<Album> Albums
        {
            get => _albums;
            set => SetProperty(ref _albums, value);
        }

        public List<Composition> Compositions
        {
            get => _compositions;
            set => SetProperty(ref _compositions, value);
        }

        public Artist ActiveArtist
        {
            get => _activeArtist;
            set => SetProperty(ref _activeArtist, value);
        }

        public Album ActiveAlbum
        {
            get => _activeAlbum;
            set => SetProperty(ref _activeAlbum, value);
        }

        public Composition ActiveComposition
        {
            get => _activeComposition;
            set => SetProperty(ref _activeComposition, value);
        }

        public int ActiveAlbumPositionValue
        {
            get => ReadPosition(_activeAlbumPositionParameter);
            set => WritePosition(_activeAlbumPositionParameter, value);
        }

        public int ActiveArtistPositionValue
        {
            get => ReadPosition(_activeArtistPositionParameter);
            set => WritePosition(_activeArtistPositionParameter, value);
        }

        public int ActiveCompositionPositionValue
        {
            get => ReadPosition(_activeCompositionPositionParameter);
            set => WritePosition(_activeCompositionPositionParameter, value);
        }

        #endregion

        #region Events handling

        private void OnMediaDataParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.NewValue.Value != e.OldValue.Value)
            {
                ThreadHelper.RunOnMainThread(() => { UpdateMediaStore(); });
            }
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

            var command = await Device.GetCommand("StopMedia");

            if (command != null)
            {
                await command.Execute();
            }

            IsBusy = false;
        }

        private async void OnPlayButtonPressed(object obj)
        {
            IsBusy = true;

            var command = await Device.GetCommand("PlayMedia");

            if (command != null)
            {
                await command.Execute();
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

        protected override void OnInitialized()
        {
            IsBusy = true;

            UpdateAgentStatus();

            InitializeStateMachineParameter();

            InitializeRelayStateParameter();

            InitializeActiveStationParameter();

            InitializeMedia();

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

        private int ReadPosition(XddParameter parameter)
        {
            int result = -1;

            if (parameter != null)
            {
                var task = Task.Run(async () =>
                {
                    await parameter.UpdateValue();
                }).ContinueWith((t) =>
                {
                    if (parameter.GetValue(out int albumPosition))
                    {
                        result = albumPosition;
                    }
                });

                task.Wait();
            }

            return result;
        }

        private bool WritePosition(XddParameter parameter, int value)
        {
            bool result = false;

            if (parameter != null)
            {
                if (parameter.SetValue(value))
                {
                    Task.Run(async () => { await parameter.Write(); });

                    result = true;
                }
            }

            return result;
        }

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

        private void InitializeMedia()
        {
            _mediaDataParameter = Device?.SearchParameter("PARAM_Media_Data") as XddParameter;
            _compressedMediaParameter = Device?.SearchParameter("PARAM_Media_Data_Compressed") as XddParameter;
            _activeArtistPositionParameter = Device?.SearchParameter("PARAM_ActiveArtistPosition") as XddParameter;
            _activeAlbumPositionParameter = Device?.SearchParameter("PARAM_ActiveAlbumPosition") as XddParameter;
            _activeCompositionPositionParameter = Device?.SearchParameter("PARAM_ActiveCompositionPosition") as XddParameter;
        }

        private void UpdateMediaStore()
        {
            if (_compressedMediaParameter != null &&
                _mediaDataParameter != null)
            {
                if (_compressedMediaParameter.GetValue(out bool compressed))
                {
                    if (_mediaDataParameter.GetValue(out byte[] data))
                    {
                        if (compressed)
                        {
                            _mediaStore = MediaStore.Deserialize(data, compressed);

                            if (_mediaStore != null)
                            {
                                UpdateMediaViewModels();
                            }
                        }
                    }
                }
            }
        }

        private void UpdateMediaViewModels()
        {
            if (_mediaStore != null)
            {
                Artists = _mediaStore.Artists;

                if (Artists.Count > 0)
                {
                    var artistPosition = ActiveArtistPositionValue;

                    _internalChange = true;

                    if (artistPosition >= 0 && artistPosition < Artists.Count)
                    {
                        ActiveArtist = Artists[artistPosition];
                    }
                    else if (Artists.Count > 0)
                    {
                        ActiveArtist = Artists[0];
                    }
                    else
                    {
                        ActiveArtist = null;
                    }
                    
                    if (ActiveArtist != null)
                    {
                        Albums = ActiveArtist.Albums;

                        if (Albums != null)
                        {
                            var albumPosition = ActiveAlbumPositionValue;

                            if (albumPosition >= 0 && albumPosition < Albums.Count)
                            {
                                _internalChange = true;

                                ActiveAlbum = Albums[albumPosition];

                                _internalChange = false;
                            }
                            else if(Albums.Count > 0)
                            {
                                _internalChange = true;

                                ActiveAlbum = Albums[0];

                                _internalChange = false;
                            }
                            else
                            {
                                _internalChange = true;

                                ActiveAlbum = null;

                                _internalChange = false;
                            }

                            if(ActiveAlbum!=null)
                            {
                                AddActiveAlbumCompositions();

                                var compositionPosition = ActiveCompositionPositionValue;

                                if (compositionPosition >= 0 && compositionPosition < Compositions.Count)
                                {
                                    _internalChange = true;

                                    ActiveComposition = Compositions[compositionPosition];

                                    _internalChange = false;
                                }
                                else if (Compositions.Count > 0)
                                {
                                    _internalChange = true;

                                    ActiveComposition = Compositions[0];

                                    _internalChange = false;
                                }
                                else
                                {
                                    _internalChange = true;

                                    ActiveComposition = null;

                                    _internalChange = false;
                                }
                            }
                        }
                    }

                    _internalChange = false;

                }
            }
        }

        private void UpdateAgentStatus()
        {
            if (Agent != null)
            {
                IsEnabled = (Agent.Status == AgentStatus.Bound);
            }
        }

        private async Task UpdateMediaAsync()
        {
            IsBusy = true;

            if (_compressedMediaParameter != null)
            {
                await _compressedMediaParameter.UpdateValue();
            }

            if (_mediaDataParameter != null)
            {
                await _mediaDataParameter.UpdateValue();
            }

            ThreadHelper.RunOnMainThread(()=> { UpdateMediaStore(); });
            
            IsBusy = false;
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

            RegisterMediaParameterEvent();

            RegisterStationParameterEvent();
        }

        private void RegisterMediaParameterEvent()
        {
            if (_compressedMediaParameter != null)
            {
                _compressedMediaParameter.AutoUpdate();

                _compressedMediaParameter.ParameterChanged += OnMediaDataParameterChanged;
            }

            if (_mediaDataParameter != null)
            {
                _mediaDataParameter.AutoUpdate();

                _mediaDataParameter.ParameterChanged += OnMediaDataParameterChanged;
            }
        }

        protected override async Task UpdateAllControls()
        {
            await UpdateMediaAsync();

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

            UnregisterMediaParameterEvent();

            UnregisterStationParameterEvent();

            return base.UnregisterAutoUpdate();
        }

        private void UnregisterMediaParameterEvent()
        {
            if (_mediaDataParameter != null)
            {
                _mediaDataParameter.ParameterChanged -= OnMediaDataParameterChanged;

                _mediaDataParameter.StopUpdate();
            }

            if (_compressedMediaParameter != null)
            {
                _compressedMediaParameter.ParameterChanged -= OnMediaDataParameterChanged;

                _compressedMediaParameter.StopUpdate();
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

        internal void OnAlbumIndexChanged(int selectedIndex)
        {
            if (Albums != null && selectedIndex >= 0 && !_internalChange)
            {
                if (selectedIndex < Albums.Count)
                {
                    ActiveAlbumPositionValue = selectedIndex;

                    _internalChange = true;
                    
                    AddActiveAlbumCompositions();

                    if (Compositions != null && Compositions.Count > 0)
                    {
                        ActiveComposition = Compositions[0];
                        ActiveCompositionPositionValue = 0;
                    }
                    else
                    {
                        ActiveCompositionPositionValue = -1;
                    }

                    _internalChange = false;
                }
                else if (Albums.Count > 0)
                {
                    var activeAlbum = Albums[0];

                    if (activeAlbum != ActiveAlbum)
                    {
                        _internalChange = true;

                        ActiveAlbum = activeAlbum;

                        AddActiveAlbumCompositions();

                        if (Compositions != null)
                        {
                            ActiveComposition = Compositions[0];
                            ActiveCompositionPositionValue = 0;
                        }
                        else
                        {
                            ActiveCompositionPositionValue = -1;
                        }

                        _internalChange = false;
                    }
                }
                else
                {
                    _internalChange = true;

                    ActiveAlbum = null;
                    ActiveComposition = null;

                    ActiveAlbumPositionValue = -1;
                    ActiveCompositionPositionValue = -1;

                    _internalChange = false;
                }
            }
        }

        private void AddActiveAlbumCompositions()
        {
            var compositions = ActiveAlbum?.Compositions;
            if (compositions != null)
            {
                var compositionList = new List<Composition>();

                compositionList.Add(new Composition() { Position = -1, Title = "--- All ---" });
                compositionList.AddRange(compositions);

                Compositions = compositionList;
            }
        }

        internal void OnArtistIndexChanged(int selectedIndex)
        {
            if (Artists != null && selectedIndex >= 0 && !_internalChange)
            {
                ActiveArtistPositionValue = selectedIndex;

                if (selectedIndex < Artists.Count)
                {
                    var activeArtist = Artists[selectedIndex];

                    _internalChange = true;

                    Albums = activeArtist?.Albums;
                    if (Albums != null && Albums.Count > 0)
                    {
                        ActiveAlbum = Albums[0];
                        ActiveAlbumPositionValue = 0;
                    }
                    else
                    {
                        ActiveAlbumPositionValue = -1;
                    }

                    _internalChange = false;
                }
                else if (Artists.Count > 0)
                {
                    var activeArtist = Artists[0];

                    if (activeArtist != ActiveArtist)
                    {
                        _internalChange = true;

                        ActiveArtist = activeArtist;

                        Albums = ActiveArtist?.Albums;
                        if (Albums != null)
                        {
                            ActiveAlbum = Albums[0];
                            ActiveAlbumPositionValue = 0;
                        }
                        else
                        {
                            ActiveAlbumPositionValue = -1;
                        }

                        _internalChange = false;
                    }
                }
                else
                {
                    _internalChange = true;

                    ActiveArtist = null;
                    ActiveComposition = null;
                    ActiveArtistPositionValue = -1;
                    ActiveAlbumPositionValue = -1;
                    ActiveCompositionPositionValue = -1;

                    _internalChange = false;
                }         
            }
        }

        internal void OnCompositionIndexChanged(int selectedIndex)
        {
            if (Compositions != null && selectedIndex >= 0 && !_internalChange)
            {
                if (selectedIndex < Compositions.Count - 1 && selectedIndex >= 0)
                {
                    ActiveCompositionPositionValue = selectedIndex - 1;

                    _internalChange = true;

                    _internalChange = false;
                }
                else if (Compositions.Count > 0)
                {
                    var activeComposition = Compositions[0];

                    if (activeComposition != ActiveComposition)
                    {
                        _internalChange = true;

                        ActiveComposition = activeComposition;
                        ActiveCompositionPositionValue = -1;

                        _internalChange = false;
                    }
                }
                else
                {
                    _internalChange = true;

                    ActiveComposition = null;
                    ActiveCompositionPositionValue = -1;

                    _internalChange = false;
                }
            }
        }

        #endregion
    }
}
