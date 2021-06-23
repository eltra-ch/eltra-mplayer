using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Runner.Console;
using System;

namespace MPlayerMaster.Device.Runner
{
    class MPlayerFifo
    {
        #region Private fields

        private MPlayerConsoleParser _parser;
        private MPlayerFifoProcess _process;

        #endregion

        #region Constructors

        public MPlayerFifo(ushort index)
        {
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
        public Parameter ProcessIdParameter { get; internal set; }

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
            bool result = false;

            if (_process != null)
            {
                result = _process.Pause(true);
            }

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

        internal bool Open(string url, bool pause)
        {
            bool result = false;

            if (Url != url)
            {
                Start(url);

                Url = url;
            }
            
            if(_process != null)
            {
                result = _process.Pause(pause);
            }

            return result;
        }
        
        private int Start(string url)
        {
            int result = -1;

            try
            {
                Stop();

                TerminateProcess();

                _process = new MPlayerFifoProcess() { Settings = Settings, ProcessIdParameter = ProcessIdParameter, Parser = Parser };
                                
                _process.Create(url);

                result = _process.ProcessId;                
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Start", e);
            }

            return result;
        }

        private void TerminateProcess()
        {
            _process?.Abort(); 
        }

        #endregion
    }
}
