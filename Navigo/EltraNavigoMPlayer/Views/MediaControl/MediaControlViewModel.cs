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
using System.Collections.Generic;
using EltraUiCommon.Helpers;
using EltraXamCommon.Controls.Toast;
using System.Linq;
using MPlayerCommon.Definitions;

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

        private XddParameter _mediaControlParameter;
        private XddParameter _statusWordParameter;
        private XddParameter _relayStateParameter;
        private XddParameter _activeStationParameter;
        
        private XddParameter _mediaDataParameter;
        private XddParameter _compressedMediaParameter;
        private XddParameter _activeArtistPositionParameter;
        private XddParameter _activeAlbumPositionParameter;
        private XddParameter _activeCompositionPositionParameter;

        private XddParameter _randomParameter;
        private XddParameter _shuffleParameter;

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

        private int _activeAlbumPositionValue;
        private int _activeArtistPositionValue;
        private int _activeCompositionPositionValue;

        private bool _isRandom;
        private bool _isShuffle;

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

        public bool IsRandom
        {
            get => _isRandom;
            set => SetProperty(ref _isRandom, value);
        }
        public bool IsShuffle
        {
            get => _isShuffle;
            set => SetProperty(ref _isShuffle, value);
        }

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

        public int ActiveAlbumPositionValue
        {
            get => _activeAlbumPositionValue;
            set => SetProperty(ref _activeAlbumPositionValue, value);
        }

        public int ActiveArtistPositionValue
        {
            get => _activeArtistPositionValue;
            set => SetProperty(ref _activeArtistPositionValue, value);
        }

        public int ActiveCompositionPositionValue
        {
            get => _activeCompositionPositionValue;
            set => SetProperty(ref _activeCompositionPositionValue, value);
        }

        #endregion

        #region Commands 

        public ICommand TurnOffButtonCommand => new Command(OnTurnOffButtonPressed);

        public ICommand StopButtonCommand => new Command(OnStopButtonPressed);

        public ICommand PlayButtonCommand => new Command(OnPlayButtonPressed);

        public ICommand NextButtonCommand => new Command(OnNextButtonPressed);

        public ICommand PrevButtonCommand => new Command(OnPrevButtonPressed);
        
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
            else if(e.PropertyName == "IsRandom")
            {
                WriteIsRandom();
            }
            else if (e.PropertyName == "IsShuffle")
            {
                WriteIsShuffle();
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

            bool result = await WriteActivePositions();

            if (result)
            {
                var command = await Device.GetCommand("PlayMedia");

                if (command != null)
                {
                    await command.Execute();
                }
            }
            else
            {
                ToastMessage.ShortAlert("Setting position failed! Check your internet connectivity and try again!");
            }

            IsBusy = false;
        }

        private async void OnNextButtonPressed(object obj)
        {
            IsBusy = true;

            if(_mediaControlParameter!=null && _mediaControlParameter.SetValue((ushort)MediaControlWordValue.Next))
            {
                if (!await _mediaControlParameter.Write())
                {
                    ToastMessage.ShortAlert("Next failed!");
                }
            }

            IsBusy = false;
        }

        private async void OnPrevButtonPressed(object obj)
        {
            IsBusy = true;

            if (_mediaControlParameter != null && _mediaControlParameter.SetValue((ushort)MediaControlWordValue.Previous))
            {
                if (!await _mediaControlParameter.Write())
                {
                    ToastMessage.ShortAlert("Previous failed!");
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

        protected override void OnInitialized()
        {
            IsBusy = true;

            UpdateAgentStatus();

            InitializeStateMachineParameter();

            InitializeRelayStateParameter();

            InitializeActiveStationParameter();

            InitializeRandomShuffleParameters();

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

        internal void OnArtistIndexChanged(int selectedIndex)
        {
            if (Artists != null && selectedIndex >= 0 && !_internalChange)
            {
                _internalChange = true;

                ActiveArtistPositionValue = selectedIndex;

                if (selectedIndex < Artists.Count)
                {
                    var activeArtist = Artists[selectedIndex];

                    AddAlbums(activeArtist);

                    if (Albums != null && Albums.Count > 0)
                    {
                        ActiveAlbumPositionValue = -1;
                        ActiveAlbum = Albums[0];

                        AddAlbumCompositions(ActiveAlbum);

                        ActiveCompositionPositionValue = 0;

                        if (Compositions != null && Compositions.Count > 0)
                        {
                            ActiveComposition = Compositions[0];
                        }
                    }
                    else
                    {
                        ActiveAlbumPositionValue = -1;
                    }
                }
                else if (Artists.Count > 0)
                {
                    var activeArtist = Artists[0];

                    if (activeArtist != null)
                    {
                        AddAlbums(activeArtist);

                        if (Albums != null)
                        {
                            ActiveAlbumPositionValue = 0;
                            ActiveCompositionPositionValue = -1;
                        }
                        else
                        {
                            ActiveAlbumPositionValue = 0;
                            ActiveCompositionPositionValue = 0;
                        }
                    }
                }
                else
                {
                    ActiveArtistPositionValue = -1;
                    ActiveAlbumPositionValue = -1;
                    ActiveCompositionPositionValue = -1;
                }

                _internalChange = false;
            }
        }

        internal void OnAlbumIndexChanged(int selectedIndex)
        {
            if (Albums != null && selectedIndex >= 0 && !_internalChange)
            {
                _internalChange = true;

                if (selectedIndex < Albums.Count)
                {
                    ActiveAlbumPositionValue = selectedIndex;
                    var activeAlbum = Albums[selectedIndex];

                    AddAlbumCompositions(activeAlbum);

                    if (Compositions != null && Compositions.Count > 0)
                    {
                        ActiveCompositionPositionValue = -1;
                        ActiveComposition = Compositions[0];
                    }
                }
                else if (Albums.Count > 0)
                {
                    var activeAlbum = Albums[0];

                    if (activeAlbum != null)
                    {
                        AddAlbumCompositions(activeAlbum);

                        if (Compositions != null)
                        {
                            ActiveCompositionPositionValue = -1;
                            ActiveComposition = Compositions[0];
                        }
                    }
                }
                else
                {
                    ActiveAlbum = null;
                    ActiveComposition = null;

                    ActiveAlbumPositionValue = -1;
                    ActiveCompositionPositionValue = -1;
                }

                _internalChange = false;
            }
        }

        internal void OnCompositionIndexChanged(int selectedIndex)
        {
            if (Compositions != null && selectedIndex >= 0 && !_internalChange)
            {
                _internalChange = true;

                if (selectedIndex < Compositions.Count - 1 && selectedIndex >= 0)
                {
                    ActiveCompositionPositionValue = selectedIndex - 1;
                }
                else if (Compositions.Count > 0)
                {
                    var activeComposition = Compositions[0];

                    if (activeComposition != null)
                    {
                        ActiveComposition = activeComposition;
                        ActiveCompositionPositionValue = -1;
                    }
                }
                else
                {
                    ActiveComposition = null;
                    ActiveCompositionPositionValue = -1;
                }

                _internalChange = false;
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

        private async Task<int> ReadPosition(XddParameter parameter)
        {
            int result = -1;
            int position = -1;

            if (parameter != null)
            {
                var parameterValue = await parameter.ReadValue();

                if (parameterValue != null && parameterValue.GetValue(ref position))
                {
                    result = position;
                }
            }

            return result;
        }

        private async Task<bool> WritePosition(XddParameter parameter, int value)
        {
            bool result = false;

            if (parameter != null)
            {
                if (parameter.SetValue(value))
                {
                    result = await parameter.Write();
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
                
                Image = ImageSource.FromResource($"{assemblyName.Name}.Resources.disc_32px.png");

                UpdateViewModels = false;
            }

            base.SetUp();
        }

        private void InitializeMedia()
        {
            _mediaControlParameter = Device?.SearchParameter("PARAM_MediaControlState") as XddParameter;
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
                        }
                    }
                }
            }
        }

        private void UpdateArtist(int activeArtistPositionValue)
        {            
            _internalChange = true;

            if (_mediaStore != null)
            {
                AddArtists();

                if (Artists.Count > 0)
                {
                    if (activeArtistPositionValue >= -1)
                    {
                        ActiveArtist = Artists[activeArtistPositionValue + 1];
                    }

                    ActiveArtistPositionValue = activeArtistPositionValue;
                }
            }

            _internalChange = false;   
        }

        private void UpdateAlbum(int activeAlbumPositionValue)
        {            
            _internalChange = true;

            ActiveAlbumPositionValue = -1;
            ActiveAlbum = null;

            if (ActiveArtistPositionValue >= -1 &&
                Artists != null && Artists.Count > 0 &&
                ActiveArtistPositionValue < Artists.Count)
            {
                var artist = Artists[ActiveArtistPositionValue + 1];

                AddAlbums(artist);
            }

            if (Albums != null && Albums.Count > 0)
            {
                ActiveAlbumPositionValue = activeAlbumPositionValue;

                if (activeAlbumPositionValue >= -1)
                {
                    ActiveAlbum = Albums[activeAlbumPositionValue + 1];
                }
                else
                {
                    ActiveAlbum = Albums[0];
                }
            }
            else
            {
                ActiveAlbumPositionValue = 0;
            }

            _internalChange = false;   
        }

        private void UpdateComposition(int activeCompositionPositionValue)
        {            
            _internalChange = true;

            ActiveCompositionPositionValue = -1;
            ActiveComposition = null;

            if (ActiveAlbumPositionValue >= -1 &&
                Albums != null && Albums.Count > 0 &&
                ActiveAlbumPositionValue < Albums.Count)
            {
                var activeAlbum = Albums[ActiveAlbumPositionValue + 1];

                AddAlbumCompositions(activeAlbum);
            }

            if (Compositions != null && Compositions.Count > 0)
            {
                if (activeCompositionPositionValue >= -1)
                {
                    ActiveComposition = Compositions[activeCompositionPositionValue + 1];
                    ActiveCompositionPositionValue = activeCompositionPositionValue;
                }
                else
                {
                    ActiveComposition = Compositions[0];
                }
            }
            else
            {
                ActiveCompositionPositionValue = 0;
            }

            _internalChange = false;   
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

            UpdateMediaStore();

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

        private void InitializeRandomShuffleParameters()
        {
            _randomParameter = Device?.SearchParameter(0x3201, 0x04) as XddParameter;
            _shuffleParameter = Device?.SearchParameter(0x3201, 0x03) as XddParameter;
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

        private async Task UpdateRandomShuffleAsync()
        {
            IsBusy = true;

            if (_randomParameter != null && _shuffleParameter != null)
            {
                var randomParameterValue = await _randomParameter.ReadValue();
                var shuffleParameterValue = await _shuffleParameter.ReadValue();

                bool random = false;
                bool shuffle = false;

                if (randomParameterValue != null && randomParameterValue.GetValue(ref random))
                {
                    IsRandom = random;
                }

                if (shuffleParameterValue != null && shuffleParameterValue.GetValue(ref shuffle))
                {
                    IsShuffle = shuffle;
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

        public override Task Show()
        {
            Task.Run(async () =>
            {
                await UpdateMediaAsync();

                var activeArtistPositionValue = await ReadPosition(_activeArtistPositionParameter);
                var activeAlbumPositionValue = await ReadPosition(_activeAlbumPositionParameter);
                var activeCompositionPositionValue = await ReadPosition(_activeCompositionPositionParameter);

                ThreadHelper.RunOnMainThread(() =>
                {
                    UpdateArtist(activeArtistPositionValue);
                    UpdateAlbum(activeAlbumPositionValue);
                    UpdateComposition(activeCompositionPositionValue);
                });

                await UpdateActiveStationAsync();

                await UpdateRandomShuffleAsync();
            });
            
            return base.Show();
        }

        public async override Task Hide()
        {
            await WriteActivePositions();

            await base.Hide();
        }

        private async Task<bool> WriteActivePositions()
        {
            bool result = await WritePosition(_activeAlbumPositionParameter, ActiveAlbumPositionValue);
            
            if (result)
            {
                result = await WritePosition(_activeArtistPositionParameter, ActiveArtistPositionValue);
            
                if(result)
                {
                    result = await WritePosition(_activeCompositionPositionParameter, ActiveCompositionPositionValue);
                }    
            }

            return result;
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

        private async void WriteIsShuffle()
        {
            if (_shuffleParameter != null)
            {
                if(_shuffleParameter.SetValue(IsShuffle))
                {
                    await _shuffleParameter.Write();
                }
            }
        }

        private async void WriteIsRandom()
        {
            if (_randomParameter != null)
            {
                if (_randomParameter.SetValue(IsRandom))
                {
                    await _randomParameter.Write();
                }
            }
        }

        private void AddArtists()
        {
            ActiveArtistPositionValue = 0;
            ActiveArtist = null;
            var artists = _mediaStore?.Artists;

            if (artists != null)
            {
                var artistList = new List<Artist>();

                var allArtist = new Artist() { Asterisk = true, Name = "--- All ---" };

                foreach(var artist in artists)
                {
                    allArtist.Albums.AddRange(artist.Albums);
                }

                allArtist.Albums = allArtist.Albums.OrderBy(o => o.Name).ToList();

                artistList.Add(allArtist);
                artistList.AddRange(artists);

                Artists = artistList;
            }
        }

        private void AddAlbums(Artist artist)
        {
            if (artist != null)
            {
                var albumList = new List<Album>();

                var allAlbums = new Album() { Asterisk = true, Name = "--- All ---" };

                foreach(var album in artist.Albums)
                {
                    allAlbums.Compositions.AddRange(album.Compositions);
                }

                allAlbums.Compositions = allAlbums.Compositions.OrderBy(o=>o.FileName).ToList();

                albumList.Add(allAlbums);
                albumList.AddRange(artist.Albums);

                Albums = albumList;
            }
        }

        private void AddAlbumCompositions(Album album)
        {
            var compositions = album?.Compositions;
            if (compositions != null)
            {
                var compositionList = new List<Composition>();

                compositionList.Add(new Composition() { Position = -1, Title = "--- All ---" });
                compositionList.AddRange(compositions);

                Compositions = compositionList;
            }
        }

        #endregion
    }
}
