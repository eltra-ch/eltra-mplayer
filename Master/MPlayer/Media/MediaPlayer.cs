using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;
using MPlayerCommon.Contracts.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MPlayerMaster.Media
{
    class MediaPlayer : IDisposable
    {
        #region Private fields

        private bool disposedValue;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _workingTask = Task.CompletedTask;
        private MediaStore _mediaStore;

        #endregion

        #region Constructors

        public MediaPlayer()
        {
            const double defaultUpdateIntervalInSec = 60;

            UpdateInterval = TimeSpan.FromSeconds(defaultUpdateIntervalInSec);
        }

        #endregion

        #region Properties

        public string MediaPath { get; set; }

        public XddParameter DataParameter { get; set; }

        public TimeSpan UpdateInterval { get; set; }

        private MediaStore MediaStore => _mediaStore ?? (_mediaStore = new MediaStore());

        #endregion

        #region Methods

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
            
            if (DataParameter != null)
            {
                if (MediaStore.Build(MediaPath))
                {
                    var data = MediaStore.Serialize();

                    result = DataParameter.SetValue(data);
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

        internal string GetUrl(int activeArtistPosition, int activeAlbumPosition, int activeCompositionPosition)
        {
            string result = string.Empty;

            if (activeAlbumPosition >= 0 && MediaStore.Artists.Count > activeAlbumPosition)
            {
                var artist = MediaStore.Artists[activeArtistPosition];
            
                if(artist!=null && activeAlbumPosition >= 0 && artist.Albums.Count > activeAlbumPosition)
                {
                    var album = artist.Albums[activeAlbumPosition];

                    if(album != null)
                    {
                        if(activeCompositionPosition == -1)
                        {
                            string playlistPath = Path.Combine(album.FullPath, "album.m3u");

                            result = $"\"{playlistPath}\"";
                        }
                        else if(activeCompositionPosition >= 0 && album.Compositions.Count > activeCompositionPosition)
                        {
                            var composition = album.Compositions[activeCompositionPosition];

                            if (composition != null)
                            {
                                result = $"\"{composition.FullPath}\"";
                            }
                        }
                    }
                }                
            }

            return result;
        }

        #endregion
    }
}
