using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Runner.Console;
using System;
using System.IO;

namespace MPlayerMaster.Device.Runner
{
    class MPlayerFifo
    {
        #region Private fields

        const int EXIT_CODE_SUCCESS = 0;
                
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

        public string Path => GetFifoPath();

        public string Url { get; set; }

        internal MPlayerConsoleParser Parser => _parser ?? (_parser = CreateParser());

        public MPlayerSettings Settings { get; set; }
        public Parameter StreamTitleParameter { get; internal set; }
        public Parameter StationTitleParameter { get; internal set; }
        public Parameter CustomStationTitleParameter { get; internal set; }
        public Parameter ProcessIdParameter { get; internal set; }

        #endregion

        #region Events

        public event EventHandler Check;

        

        private void OnCheck()
        {
            Check?.Invoke(this, EventArgs.Empty);
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

        private string GetFifoPath()
        {
            var tempPath = System.IO.Path.GetTempPath();

            string fifoPath = System.IO.Path.Combine(tempPath, Name);

            return fifoPath;
        }

        internal bool Stop()
        {
            var result = _process.Pause(true);

            return result;
        }

        public bool Pause(bool pause)
        {
            bool result = false;

            if (_process != null)
            {
                result = _process.Pause(pause);
            }

            return result;
        }

        public bool Mute(bool mute)
        {
            bool result = false;

            try
            {
                int exitCode = EXIT_CODE_SUCCESS;
                int m = mute ? 1 : 0;
              
                if (File.Exists(Path))
                {
                    using (var fifoFile = new StreamWriter(Path, append: false))
                    {
                        fifoFile.WriteLine("mute {m}");
                    }
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - Mute", $"fifo file {Path} doesn't exist!");
                }

                result = (exitCode == EXIT_CODE_SUCCESS);
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Mute", e);
            }

            return result;
        }

        internal bool Open(string url)
        {
            bool result = false;

            if (Url != url)
            {
                Start(url);

                Url = url;
                result = true;
            }
            else if(_process != null)
            {
                result = _process.Pause(false);
            }

            return result;
        }
        
        public int Start(string url)
        {
            int result = -1;

            try
            {
                Stop();

                TerminateProcess();

                _process = new MPlayerFifoProcess() { Settings = Settings, ProcessIdParameter = ProcessIdParameter, Parser = Parser };

                _process.Create(url);

                result = _process.ProcessId;

                _process.Pause(true);
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

            OnCheck();            
        }

        #endregion
    }
}
