using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MPlayerMaster.Device.Runner
{
    class MPlayerFifoManager
    {
        #region Private fields

        const int EXIT_CODE_SUCCESS = 0;

        private List<MPlayerFifo> _fifoList;

        #endregion

        #region Properties

        public MPlayerSettings Settings { get; set; }

        public List<MPlayerFifo> FifoList => _fifoList ?? (_fifoList = new List<MPlayerFifo>());
                
        public List<Parameter> StreamTitleParameters { get; internal set; }
        public List<Parameter> StationTitleParameters { get; internal set; }
        public List<Parameter> CustomStationTitleParameters { get; internal set; }
        public List<Parameter> ProcessIdParameters { get; internal set; }

        #endregion

        #region Methods

        public bool Exists(ushort index)
        {
            bool result = false;

            foreach(var fifo in FifoList)
            {
                if (fifo.Index == index)
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        internal bool Create(ushort index)
        {
            bool result = false;

            try
            {
                int exitCode = EXIT_CODE_SUCCESS;
                var fifo = new MPlayerFifo(index) { Settings = Settings };
                
                if (!File.Exists(fifo.Path))
                {
                    var startInfo = new ProcessStartInfo("mkfifo", fifo.Path);

                    var process = Process.Start(startInfo);

                    if (process != null)
                    {
                        process.WaitForExit();

                        exitCode = process.ExitCode;
                    }
                }

                if(!Exists(index) && exitCode == EXIT_CODE_SUCCESS)
                {
                    fifo.StreamTitleParameter = StreamTitleParameters[index];
                    fifo.StationTitleParameter = StationTitleParameters[index];
                    fifo.CustomStationTitleParameter = CustomStationTitleParameters[index];
                    fifo.ProcessIdParameter = ProcessIdParameters[index];

                    MsgLogger.WriteFlow($"fifo added - {fifo.Name}");

                    FifoList.Add(fifo);
                }

                result = (exitCode == EXIT_CODE_SUCCESS);
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Create", e);
            }

            return result;
        }

        private MPlayerFifo GetFifo(ushort index)
        {
            MPlayerFifo result = null;

            foreach(var fifo in FifoList)
            {
                if(fifo.Index == index)
                {
                    result = fifo;
                    break;
                }                
            }

            return result;
        }

        internal bool OpenUrl(ushort index, string url, bool unmute)
        {
            bool result = false;
            var fifo = GetFifo(index);

            if(fifo != null)
            {
                MuteAll(true);

                if (!fifo.Open(url))
                {
                    MsgLogger.WriteError($"{GetType().Name} - OpenUrl", $"cannot open url {url}!");
                }
                else
                {
                    MsgLogger.WriteLine($"url {url} opened successfully!");

                    result = fifo.Mute(!unmute);
                }
            }

            return result;
        }

        private void MuteAll(bool mute)
        {
            foreach (var f in FifoList)
            {
                f.Mute(mute);
            }
        }

        internal bool Stop()
        {
            bool result = true;

            foreach(var fifo in FifoList)
            {
                if(!fifo.Stop())
                {
                    MsgLogger.WriteError($"{GetType().Name} - Stop", $"stop fifo {fifo.Name} failed!");
                    result = false;
                    break;
                }
            }

            return result;
        }

        #endregion
    }
}
