﻿using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Radio;
using MPlayerMaster.Device.Runner.Console;
using System;
using System.Diagnostics;

namespace MPlayerMaster.Device.Runner
{
    internal class MPlayerRunner
    {
        #region Private fields

        private MPlayerConsoleParser _parser;
        private Process _process;
        private MPlayerFifoManager _fifoManager;

        #endregion

        #region Constructors

        public MPlayerRunner()
        {
        }

        #endregion

        #region Properties

        public int ActiveStationValue
        {
            get
            {
                int result = -1;

                var activeStationParameter = RadioPlayer?.ActiveStationParameter;

                if (activeStationParameter != null && activeStationParameter.GetValue(out int activeStationValue))
                {
                    result = activeStationValue;
                }

                return result;
            }
        }

        public RadioPlayer RadioPlayer { get; set; }

        public MPlayerSettings Settings { get; set; }

        internal MPlayerConsoleParser Parser => _parser ?? (_parser = CreateParser());

        private MPlayerFifoManager FifoManager => _fifoManager ?? (_fifoManager = new MPlayerFifoManager() { Settings = Settings });

        #endregion

        #region Events

        public event EventHandler MPlayerProcessExited;

        #endregion

        #region Events handling

        private void OnProcessExited(object sender, EventArgs e)
        {
            MPlayerProcessExited?.Invoke(this, e);
        }

        #endregion

        #region Methods

        public void CreateStationFifo(ushort index, string url)
        {
            FifoManager.StreamTitleParameters = RadioPlayer.StreamTitleParameters;
            FifoManager.StationTitleParameters = RadioPlayer.StationTitleParameters;
            FifoManager.CustomStationTitleParameters = RadioPlayer.CustomStationTitleParameters;
            FifoManager.ProcessIdParameters = RadioPlayer.ProcessIdParameters;

            if (!FifoManager.Create(index))
            {
                MsgLogger.WriteError($"{GetType().Name} - CreateFifo", $"Fifo creation for index = {index} failed!");
            }
            else
            {
                FifoManager.OpenUrl(index, url, true);
            }
        }

        public bool PlayUrl(ushort index, string url)
        {
            return FifoManager.OpenUrl(index, url, false);
        }

        private MPlayerConsoleParser CreateParser()
        {
            var result = new MPlayerConsoleParser
            {
                StreamTitleParameter = RadioPlayer.StreamTitleParameters[0],
                StationTitleParameter = RadioPlayer.StationTitleParameters[0],
                CustomStationTitleParameter = RadioPlayer.CustomStationTitleParameters[0]
            };

            return result;
        }

        public bool Start(Parameter processParam, string url)
        {
            bool result = false;

            try
            {
                int processId = Start(url);

                if (processParam != null && processId >= 0)
                {
                    if (!processParam.SetValue(processId))
                    {
                        MsgLogger.WriteError($"{GetType().Name} - Start", "process id cannot be set");
                    }
                    else
                    {
                        result = true;
                    }
                }

                MsgLogger.WriteFlow($"{GetType().Name} - Start", $"Set Station request: {url}, processId = {processId}");
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Start", e);
            }

            return result;
        }

        public int Start(string url)
        {
            int result = -1;

            try
            {
                Stop();

                if(_process != null && !_process.HasExited)
                {
                    _process.EnableRaisingEvents = false;
                    _process.Exited -= OnProcessExited;

                    _process.Kill();
                }

                var startInfo = new ProcessStartInfo();
                
                _process = new Process();
                _process.EnableRaisingEvents = true;
                _process.Exited += OnProcessExited;

                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;

                startInfo.WindowStyle = ProcessWindowStyle.Normal;

                GetPlayListFlag(url, out string playlistFlag);

                startInfo.Arguments = Settings.AppArgs + playlistFlag + $" {url}";
                startInfo.Arguments = startInfo.Arguments.Trim();

                startInfo.FileName = Settings.MPlayerProcessPath;

                _process.StartInfo = startInfo;

                _process.OutputDataReceived += (sender, args) =>
                {
                    Parser.ProcessLine(args.Data);
                };
				
                _process.Start();

                _process.BeginOutputReadLine();

                result = _process.Id;

                MsgLogger.WriteFlow($"{GetType().Name} - Start", $"Start process: app = {startInfo.FileName},  args: {startInfo.Arguments}, result = {_process != null}");
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Start", e);
            }

            return result;
        }

        private static string GetPlayListFlag(string url, out string playlistFlag)
        {
            playlistFlag = string.Empty;

            url = url.TrimEnd();
            
            string[] playlistExtensions = { ".asx", ".m3u", ".m3u8", ".pls", ".plst", ".qtl", ".ram", ".wax", ".wpl", ".xspf" };
            foreach (var playlistExtension in playlistExtensions)
            {
                if (url.EndsWith(playlistExtension) || url.EndsWith(playlistExtension + "\""))
                {
                    playlistFlag = " -playlist ";
                    break;
                }
            }

            return playlistFlag;
        }

        public bool StopFifo()
        {
            return FifoManager.Stop();
        }

        public bool Stop()
        {
            bool result = false;

            if (_process != null)
            {
                _process.EnableRaisingEvents = false;
                _process.Exited -= OnProcessExited;
            }

            try
            {
                result = CloseActualStationProcess();

                if (!result)
                {
                    result = TryCloseAllProcesses();
                }

                if (!result)
                {
                    CloseBruteForce();

                    result = true;
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Stop [2]", e);
            }
                        
            return result;
        }

        private bool TryCloseAllProcesses()
        {
            bool result = false;

            var stationsCountParameter = RadioPlayer?.StationsCountParameter;
            var processIdParameters = RadioPlayer?.ProcessIdParameters;
                
            if (stationsCountParameter != null && stationsCountParameter.GetValue(out ushort maxCount))
            {
                for (ushort i = 0; i < maxCount; i++)
                {
                    if (processIdParameters != null && processIdParameters[i].GetValue(out int processId) && processId > 0)
                    {
                        if(CloseProcess(processId))
                        {
                            result = true;
                        }
                    }
                }
            }

            return result;
        }

        private bool CloseActualStationProcess()
        {
            bool result = false;
            int i = ActiveStationValue - 1;

            if (i >= 0)
            {
                var processIdParameters = RadioPlayer?.ProcessIdParameters;

                if (processIdParameters != null && processIdParameters[i].GetValue(out int processId) && processId > 0)
                {
                    result = CloseProcess(processId);
                }
            }

            return result;
        }

        

        private bool CloseBruteForce()
        {
            bool result = false;

            MsgLogger.WriteFlow($"close '{Settings.MPlayerProcessName}' brute force");

            try
            {
                foreach (var p in Process.GetProcessesByName(Settings.MPlayerProcessName))
                {
                    p.Kill();

                    result = true;
                }
            }
            catch(Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - CloseBruteForce", e);
            }
            
            return result;
        }

        private bool CloseProcess(int processId)
        {
            bool result = false;
            
            try
            {
                var p = Process.GetProcessById(processId);

                if (p != null)
                {
                    if (!p.HasExited)
                    {
                        const int MaxWaitTimeInMs = 10000;
                        var startInfo = new ProcessStartInfo("kill");

                        startInfo.WindowStyle = ProcessWindowStyle.Normal;
                        startInfo.Arguments = $"{processId}";

                        Process.Start(startInfo);

                        result = p.WaitForExit(MaxWaitTimeInMs);
                    }
                    else
                    {
                        MsgLogger.WriteError($"{GetType().Name} - Stop", $"process id exited - pid {processId}");
                    }
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - Stop", $"process id not found - pid {processId}");
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Stop [1]", e);
            }

            return result;
        }

        #endregion
    }
}
