using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using EltraConnector.Master.Device;
using MPlayerCommon.Contracts.Media;
using MPlayerCommon.Definitions;
using MPlayerMaster.Runner;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static MPlayerMaster.MPlayerDefinitions;

namespace MPlayerMaster.Media
{
    class MediaPlayer : IDisposable
    {
        #region Private fields

        private bool disposedValue;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _workingTask = Task.CompletedTask;
        private MediaStore _mediaStore;
        private bool _random;
        private bool _shuffle;
        private MediaPlanner _mediaPlanner;
        private MPlayerRunner _runner;

        //media
        private XddParameter _mediaDataParameter;
        private XddParameter _mediaDataCompressedParameter;
        
        private XddParameter _mediaControlState;
        private XddParameter _mediaControlStateDisplay;
        
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

        public string MediaPath { get; set; }

        public TimeSpan UpdateInterval { get; set; }

        private MediaStore MediaStore => _mediaStore ?? (_mediaStore = new MediaStore());

        public MPlayerRunner Runner 
        { 
            get => _runner; 
            set
            {
                _runner = value;
                OnRunnerChanged();
            }
        }

        public MasterVcs Vcs { get; internal set; }

        #endregion

        #region Events handling

        private void OnRunnerChanged()
        {
            Runner.MPlayerProcessExited += OnMPlayerProcessExited;
        }

        private void OnMPlayerProcessExited(object sender, EventArgs e)
        {
            var url = MediaPlanner.GetNextUrl();

            if (!string.IsNullOrEmpty(url))
            {
                PlayMedia(url);
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

        private void OnMediaControlStateChanged(MediaControlWordValue state)
        {
            MsgLogger.WriteFlow($"{GetType().Name} - OnMediaControlStateChanged", $"media control state changed, new state = {state}");
        }

        #endregion

        #region Methods

        private MediaPlanner CreateMediaPlanner()
        {
            var mediaPlanner = new MediaPlanner() { MediaStore = MediaStore, Vcs = Vcs };

            return mediaPlanner;
        }

        internal bool Start()
        {
            bool result = false;
            
            if(Update())
            {
                _workingTask = Task.Run(async () => { await DoUpdate(); }, _cancellationTokenSource.Token);

                result = true;
            }

            return result;
        }

        private async Task DoUpdate()
        {
            const int minDelay = 250;

            while(!_cancellationTokenSource.IsCancellationRequested)
            {
                Update();

                var stopWatch = new Stopwatch();
                
                stopWatch.Start();

                while(stopWatch.Elapsed < UpdateInterval && !_cancellationTokenSource.IsCancellationRequested)
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
            _mediaDataParameter = Vcs.SearchParameter("PARAM_Media_Data") as XddParameter;
            _mediaDataCompressedParameter = Vcs.SearchParameter("PARAM_Media_Data_Compressed") as XddParameter;
            
            _mediaControlState = Vcs.SearchParameter("PARAM_MediaControlState") as XddParameter;
            _mediaControlStateDisplay = Vcs.SearchParameter("PARAM_MediaControlStateDisplay") as XddParameter;
            
            if (_mediaControlState != null)
            {
                await _mediaControlState.UpdateValue();

                _mediaControlState.ParameterChanged += OnMediaControlStateParameterChanged;
            }

            if (_mediaControlStateDisplay != null)
            {
                await _mediaControlStateDisplay.UpdateValue();
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

        public bool PlayMedia()
        {
            bool result = false;

            if(MediaPlanner.IsPlaylistEmpty)
            {
                if (MediaPlanner.GetPositions(out int activeArtistPosition, out int activeAlbumPosition, out int activeCompositionPosition))
                {
                    MediaPlanner.BuildPlaylist(activeArtistPosition, activeAlbumPosition, activeCompositionPosition);
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - PlayMedia", "planner get url positions failed!");
                }
            }
            else
            {
                var url = MediaPlanner.GetNextUrl();

                if (!string.IsNullOrEmpty(url))
                {
                    result = PlayMedia(url);
                }
            }                

            return result;
        }

        private bool PlayMedia(string url)
        {
            SetStatusWord(StatusWordEnums.PendingExecution);

            bool result = Runner.Start(url) >= 0;

            SetStatusWord(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

            return result;
        }

        internal bool StopMedia()
        {
            SetStatusWord(StatusWordEnums.PendingExecution);

            MediaPlanner.ClearPlaylist();

            bool result = Runner.Stop();

            SetStatusWord(result ? StatusWordEnums.ExecutedSuccessfully : StatusWordEnums.ExecutionFailed);

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
            else if (objectIndex == 0x3201)
            {
                switch (objectSubindex)
                {
                    case 0x01:
                        {
                            ushort state = BitConverter.ToUInt16(data, 0);

                            if (_mediaControlState != null)
                            {
                                result = _mediaControlState.SetValue(state);
                            }
                        }
                        break;                    
                }
            }

            if(!result)
            {
                result = MediaPlanner.SetObject(objectIndex, objectSubindex, data);
            }

            return result;
        }

        #endregion
    }
}
