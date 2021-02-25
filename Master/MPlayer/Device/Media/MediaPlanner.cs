using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraConnector.Master.Device;
using MPlayerCommon.Contracts.Media;
using MPlayerMaster.Device.Contracts;
using MPlayerMaster.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MPlayerMaster.Device.Media
{
    class MediaPlanner
    {
        #region Private fields

        private XddParameter _mediaControlShuffle;
        private XddParameter _mediaControlRandom;
        private XddParameter _mediaActiveArtistPositionParameter;
        private XddParameter _mediaActiveAlbumPositionParameter;
        private XddParameter _mediaActiveCompositionPositionParmeter;
        private bool _random;
        private bool _shuffle;
        private PlannerComposition _currentComposition;
        private List<PlannerComposition> _currentPlaylist;

        #endregion

        #region Properties

        public List<PlannerComposition> CurrentPlaylist => _currentPlaylist ?? (_currentPlaylist = new List<PlannerComposition>());

        public MasterVcs Vcs { get; internal set; }

        public MediaStore MediaStore { get; set; }

        public bool Random
        {
            get => _random;
            set
            {
                if (_random != value)
                {
                    _random = value;
                }
            }
        }

        public bool Shuffle
        {
            get => _shuffle;
            set
            {
                if (_shuffle != value)
                {
                    _shuffle = value;
                }
            }
        }

        public bool IsPlaylistEmpty => CurrentPlaylist.Count == 0;

        public PlannerComposition CurrentComposition => _currentComposition;

        #endregion

        #region Events handling

        private void OnMediaControlRandomParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            bool state = false;

            if (parameterValue != null && parameterValue.GetValue(ref state))
            {
                OnMediaControlRandomChanged(state);
            }
        }

        private void OnMediaControlShuffleParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            var parameterValue = e.NewValue;
            bool state = false;

            if (parameterValue != null && parameterValue.GetValue(ref state))
            {
                OnMediaControlShuffleChanged(state);
            }
        }

        private void OnMediaControlShuffleChanged(bool shuffle)
        {
            MsgLogger.WriteFlow($"{GetType().Name} - OnMediaControlShuffleChanged", $"shuffle state changed, new state = {shuffle}");

            Shuffle = shuffle;
        }

        private void OnMediaControlRandomChanged(bool random)
        {
            MsgLogger.WriteFlow($"{GetType().Name} - OnMediaControlRandomChanged", $"random state changed, new state = {random}");

            Random = random;
        }

        private void OnMediaPositionParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            BuildPlaylist();
        }

        #endregion

        #region Methods

        internal void BuildPlaylist()
        {
            if (GetPositions(out int activeArtistPosition, out int activeAlbumPosition, out int activeCompositionPosition))
            {
                BuildPlaylist(activeArtistPosition, activeAlbumPosition, activeCompositionPosition);
            }
            else
            {
                MsgLogger.WriteError($"{GetType().Name} - PlayMedia", "planner get url positions failed!");
            }
        }

        private bool GetPositions(out int activeArtistPosition, out int activeAlbumPosition, out int activeCompositionPosition)
        {
            bool result = false;

            activeArtistPosition = -1;
            activeAlbumPosition = -1;
            activeCompositionPosition = -1;

            if (_mediaActiveArtistPositionParameter != null &&
               _mediaActiveAlbumPositionParameter != null &&
               _mediaActiveCompositionPositionParmeter != null)
            {
                result = _mediaActiveArtistPositionParameter.GetValue(out activeArtistPosition);
                if(result) result = _mediaActiveAlbumPositionParameter.GetValue(out activeAlbumPosition);
                if (result) result = _mediaActiveCompositionPositionParmeter.GetValue(out activeCompositionPosition);                
            }

            return result;
        }

        internal async Task InitParameters()
        {
            _mediaActiveArtistPositionParameter = Vcs.SearchParameter("PARAM_ActiveArtistPosition") as XddParameter;
            _mediaActiveAlbumPositionParameter = Vcs.SearchParameter("PARAM_ActiveAlbumPosition") as XddParameter;
            _mediaActiveCompositionPositionParmeter = Vcs.SearchParameter("PARAM_ActiveCompositionPosition") as XddParameter;

            if(_mediaActiveArtistPositionParameter!=null)
            {
                await _mediaActiveAlbumPositionParameter.UpdateValue();

                _mediaActiveAlbumPositionParameter.ParameterChanged += OnMediaPositionParameterChanged;
            }

            if (_mediaActiveAlbumPositionParameter != null)
            {
                await _mediaActiveAlbumPositionParameter.UpdateValue();

                _mediaActiveAlbumPositionParameter.ParameterChanged += OnMediaPositionParameterChanged;
            }

            if (_mediaActiveCompositionPositionParmeter != null)
            {
                await _mediaActiveCompositionPositionParmeter.UpdateValue();

                _mediaActiveCompositionPositionParmeter.ParameterChanged += OnMediaPositionParameterChanged;
            }

            _mediaControlShuffle = Vcs.SearchParameter("PARAM_MediaControlShuffle") as XddParameter;
            _mediaControlRandom = Vcs.SearchParameter("PARAM_MediaControlRandom") as XddParameter;

            if (_mediaControlShuffle != null)
            {
                var shuffleParameterValue = await _mediaControlShuffle.UpdateValue();

                if(shuffleParameterValue != null)
                {
                    shuffleParameterValue.GetValue(ref _shuffle);
                }

                _mediaControlShuffle.ParameterChanged += OnMediaControlShuffleParameterChanged;
            }

            if (_mediaControlRandom != null)
            {
                var randomParameterValue = await _mediaControlRandom.UpdateValue();

                if(randomParameterValue!=null)
                {
                    randomParameterValue.GetValue(ref _random);
                }

                _mediaControlRandom.ParameterChanged += OnMediaControlRandomParameterChanged;
            }

            BuildPlaylist();
        }

        internal PlannerComposition GetNextUrl(bool repeat = true)
        {
            PlannerComposition result = null;

            if(CurrentPlaylist.Count > 0)
            {
                var toBePlayed = GetToBePlayed();

                if (toBePlayed.Count > 0)
                {
                    var composition = toBePlayed[0];

                    if (composition != null)
                    {
                        composition.State = PlayingState.Played;

                        _currentComposition = composition;

                        result = composition;
                    }
                }
                else
                {
                    if (Shuffle)
                    {
                        RestorePlaylistState();

                        if (repeat)
                        {
                            result = GetNextUrl(false);
                        }
                    }
                }
            }

            return result;
        }

        private int FindCurrentCompositionIndex()
        {
            int result = -1;
            int index = 0;

            if (_currentComposition != null)
            {
                foreach (var composition in CurrentPlaylist)
                {
                    if (composition == _currentComposition)
                    {
                        result = index;
                        break;
                    }
                    index++;
                }
            }

            return result;
        }

        internal PlannerComposition GetPreviousUrl(bool repeat = true)
        {
            PlannerComposition result = null;

            if (CurrentPlaylist.Count > 0)
            {
                int index = FindCurrentCompositionIndex();

                if(index != -1)
                {
                    CurrentPlaylist[index].State = PlayingState.Ready;
                    if (index > 0)
                    {
                        CurrentPlaylist[index - 1].State = PlayingState.Ready;
                    }
                }
                else
                {
                    RestorePlaylistState();
                }

                var toBePlayed = GetToBePlayed();

                if (toBePlayed.Count > 0)
                {
                    var composition = toBePlayed[0];

                    if (composition != null)
                    {
                        composition.State = PlayingState.Played;

                        _currentComposition = composition;

                        result = composition;
                    }
                }
                else
                {
                    if (Shuffle)
                    {
                        RestorePlaylistState();

                        if (repeat)
                        {
                            result = GetNextUrl(false);
                        }
                    }
                }
            }

            return result;
        }

        private List<PlannerComposition> GetToBePlayed()
        {
            var toBePlayed = new List<PlannerComposition>();

            foreach (var composition in CurrentPlaylist)
            {
                if (composition.State == PlayingState.Ready)
                {
                    toBePlayed.Add(composition);
                }
            }

            if (Random)
            {
                toBePlayed.Shuffle();
            }

            return toBePlayed;
        }

        private void RestorePlaylistState()
        {
            foreach (var composition in CurrentPlaylist)
            {
                composition.State = PlayingState.Ready;
            }
        }

        private void BuildAllCompositionPlaylist()
        {   
            foreach (var artist in MediaStore.Artists)
            {
                BuildAllArtistCompositionPlaylist(artist);
            }
        }

        private void BuildAllArtistCompositionPlaylist(Artist artist)
        {
            if (artist != null)
            {
                foreach (var album in artist.Albums)
                {
                    BuildAllAlbumCompositionPlaylist(album);
                }
            }
        }

        private void BuildAllAlbumCompositionPlaylist(Album album)
        {
            if (album != null)
            {
                foreach (var composition in album.Compositions)
                {
                    CurrentPlaylist.Add(new PlannerComposition(composition));
                }                
            }
        }

        private Artist GetArtist(int activeArtistPosition)
        {
            Artist result = null;

            if (activeArtistPosition >= 0 && MediaStore.Artists.Count > activeArtistPosition)
            {
                result = MediaStore.Artists[activeArtistPosition];
            }

            return result;
        }

        internal void SetPlayListToReady()
        {
            foreach(var composition in CurrentPlaylist)
            {
                composition.State = PlayingState.Ready;
            }
        }

        private Album GetAlbum(Artist artist, int activeAlbumPosition)
        {
            Album result = null;

            if (artist != null && activeAlbumPosition >= 0 && artist.Albums.Count > activeAlbumPosition)
            {
                result = artist.Albums[activeAlbumPosition];
            }

            return result;
        }

        private Composition GetComposition(Album album, int activeCompositionPosition)
        {
            Composition result = null;

            if (activeCompositionPosition >= 0 && album.Compositions.Count > activeCompositionPosition)
            {
                result = album.Compositions[activeCompositionPosition];    
            }

            return result;
        }

        internal void BuildPlaylist(int activeArtistPosition, int activeAlbumPosition, int activeCompositionPosition)
        {
            CurrentPlaylist.Clear();

            var artist = GetArtist(activeArtistPosition);

            if (artist == null)
            {
                BuildAllCompositionPlaylist();
            }
            else 
            {
                var album = GetAlbum(artist, activeAlbumPosition);

                if (album == null)
                {
                    BuildAllArtistCompositionPlaylist(artist);
                }
                else
                {
                    var composition = GetComposition(album, activeCompositionPosition);

                    if (composition == null)
                    {
                        BuildAllAlbumCompositionPlaylist(album);
                    }
                    else
                    {
                        CurrentPlaylist.Add(new PlannerComposition(composition));
                    }
                }
            }
        }

        internal bool SetObject(ushort objectIndex, byte objectSubindex, byte[] data)
        {
            bool result = false;

            if (objectIndex == 0x3200)
            {
                switch (objectSubindex)
                {
                    case 0x03:
                        int val1 = BitConverter.ToInt32(data, 0);
                        if (_mediaActiveArtistPositionParameter != null && _mediaActiveArtistPositionParameter.SetValue(val1))
                        {
                            result = true;
                        }
                        break;
                    case 0x04:
                        int val2 = BitConverter.ToInt32(data, 0);
                        if (_mediaActiveAlbumPositionParameter != null && _mediaActiveAlbumPositionParameter.SetValue(val2))
                        {
                            result = true;
                        }
                        break;
                    case 0x05:
                        int val3 = BitConverter.ToInt32(data, 0);
                        if (_mediaActiveCompositionPositionParmeter != null && _mediaActiveCompositionPositionParmeter.SetValue(val3))
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
                    case 0x02:
                        {
                            bool state = BitConverter.ToBoolean(data, 0);

                            if (_mediaControlShuffle != null)
                            {
                                result = _mediaControlShuffle.SetValue(state);
                            }
                        }
                        break;
                    case 0x03:
                        {
                            bool state = BitConverter.ToBoolean(data, 0);

                            if (_mediaControlRandom != null)
                            {
                                result = _mediaControlRandom.SetValue(state);
                            }
                        }
                        break;
                }
            }

            return result;
        }

        #endregion
    }
}
