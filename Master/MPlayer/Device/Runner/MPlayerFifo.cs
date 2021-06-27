using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Players;
using MPlayerMaster.Device.Runner.Console;
using MPlayerMaster.Helpers;
using System;
using System.IO;

namespace MPlayerMaster.Device.Runner
{
    class MPlayerFifo
    {
        #region Private fields

        private MPlayerConsoleParser _parser;
        private MPlayerFifoProcess _process;
        private Player _player;

        #endregion

        #region Constructors

        public MPlayerFifo(Player player, ushort index)
        {
            _player = player;
            Index = index;
        }

        #endregion

        #region Properties

        public ushort Index { get; private set; }

        public string Name => GetFifoName();

        public string Url { get; set; }

        internal MPlayerConsoleParser Parser => _parser ?? (_parser = CreateParser());

        public MPlayerSettings Settings { get; set; }
        public Parameter StreamTitleParameter { get; internal set; }
        public Parameter StationTitleParameter { get; internal set; }
        public Parameter CustomStationTitleParameter { get; internal set; }
        
        public bool IsCreated => _process != null;

        #endregion

        #region Events

        public event EventHandler Opened;

        private void OnOpened()
        {
            Opened?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Methods

        private MPlayerConsoleParser CreateParser()
        {
            var result = new MPlayerConsoleParser()
            {
                StreamTitleParameter = StreamTitleParameter,
                StationTitleParameter = StationTitleParameter,
                CustomStationTitleParameter = CustomStationTitleParameter
            };

            return result;
        }


        private string GetFifoName()
        {
            return $"MPM{Index:X4}";
        }

        internal bool Stop()
        {
            var result = Pause(true);

            return result;
        }

        public bool Pause(bool pause)
        {
            bool result = true;

            if (_process != null)
            {
                result = _process.Pause(pause);
            }

            return result;
        }

        private bool PlayProcess(string url)
        {
            bool result = true;

            if (_process != null)
            {
                result = _process.Play(url);
            }

            return result;
        }

        internal bool Play(string url)
        {
            bool result = false;

            if (Url != url)
            {
                PlayProcess(url);

                Url = url;
            }

            if (_process != null)
            {
                result = _process.Pause(false);
            }

            return result;
        }

        private string GetSilenceUri()
        {
            string result = string.Empty;
            string embeddedFileName = "mp_silence_77F7.mp3";
            string tempPath = Path.GetTempPath();
            string targetFile = Path.Combine(tempPath, embeddedFileName);

            if (!File.Exists(targetFile))
            {
                if(AssembyHelpers.CreateFileFromResource(embeddedFileName, targetFile))
                {
                    result = targetFile;
                }
            }
            else
            {
                result = targetFile;
            }

            return result;
        }

        public bool Create()
        {
            bool result = false;

            try
            {
                Stop();

                string url = GetSilenceUri();

                _process = new MPlayerFifoProcess() { Settings = Settings, Parser = Parser };
                                
                _process.Create(url);

                result = _process.ProcessId > 0;                
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Start", e);
            }

            return result;
        }

        #endregion
    }
}
