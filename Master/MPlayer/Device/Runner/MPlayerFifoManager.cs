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

        #region Events

        private void OnFifoCheck(object sender, EventArgs e)
        {
            var sourceFifo = sender as MPlayerFifo;

            /*foreach (var fifo in FifoList)
            {
                if (fifo.Index == sourceFifo.Index && fifo.ProcessId > 0)
                {
                    MsgLogger.WriteLine($"begin search for running mplayer (index={fifo.Index}) process...");

                    try
                    {
                        foreach (var p in Process.GetProcessesByName(Settings.MPlayerProcessName))
                        {
                            if(p.Id == fifo.ProcessId)
                            {
                                p.Kill();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MsgLogger.Exception($"{GetType().Name} - TerminateProcess", ex);
                    }

                    MsgLogger.WriteLine($"end search for running mplayer (index={fifo.Index}) process...");
                }
            }*/           
        }

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
                /*int exitCode = EXIT_CODE_SUCCESS;
                
                
                if (!File.Exists(fifo.Path))
                {
                    var startInfo = new ProcessStartInfo("mkfifo", fifo.Path);

                    var process = Process.Start(startInfo);

                    if (process != null)
                    {
                        process.WaitForExit();

                        exitCode = process.ExitCode;
                    }
                }*/

                if(!Exists(index))
                {
                    var fifo = new MPlayerFifo(index) { Settings = Settings };

                    fifo.StreamTitleParameter = StreamTitleParameters[index];
                    fifo.StationTitleParameter = StationTitleParameters[index];
                    fifo.CustomStationTitleParameter = CustomStationTitleParameters[index];
                    fifo.ProcessIdParameter = ProcessIdParameters[index];

                    MsgLogger.WriteFlow($"fifo added - {fifo.Name}");

                    fifo.Check += OnFifoCheck;

                    FifoList.Add(fifo);

                    result = true;
                }
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

        internal bool OpenUrl(ushort index, string url, bool pause)
        {
            bool result = false;
            var fifo = GetFifo(index);

            if(fifo != null)
            {
                PauseAll(true);

                if (!fifo.Open(url))
                {
                    MsgLogger.WriteError($"{GetType().Name} - OpenUrl", $"cannot open url {url}!");
                }
                else
                {
                    MsgLogger.WriteLine($"url {url} opened successfully!");

                    result = fifo.Pause(pause);
                }
            }

            return result;
        }

        private void PauseAll(bool mute)
        {
            foreach (var f in FifoList)
            {
                f.Pause(mute);
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
