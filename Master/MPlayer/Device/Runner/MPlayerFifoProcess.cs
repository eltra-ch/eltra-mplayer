using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Runner.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MPlayerMaster.Device.Runner
{
    class MPlayerFifoProcess
    {
        #region

        private Process _process;
        private bool _started;
        private MPlayerConsoleParser _parser;
        private Queue<bool> _pauseQueue;
        int pauseToggleCount;

        #endregion

        #region Properties

        public MPlayerSettings Settings { get; set; }

        public int ProcessId { get; private set; }

        public MPlayerConsoleParser Parser 
        { 
            get => _parser;
            internal set
            {
                _parser = value;
                OnParserChanged();
            } 
        }

        protected bool IsStarted => _started;

        private Queue<bool> PauseQueue => _pauseQueue ?? (_pauseQueue = new Queue<bool>());

        #endregion

        #region Events

        private void OnProcessExited(object sender, EventArgs e)
        {
        }

        private void OnParserChanged()
        {
            if(_parser != null)
            {
                _parser.StartingPlayback += (o, e) =>
                {
                    _started = true;

                    while(PauseQueue.TryDequeue(out bool pause))
                    {
                        Pause(pause);
                    }
                };
            }
        }

        #endregion

        #region Methods

        public static bool IsOdd(int value)
        {
            return value % 2 != 0;
        }

        private bool WriteCommand(string command)
        {
            bool result = false;

            try
            {
                if (_process != null)
                {
                    _process.StandardInput.Write($"{command}\n");
                    _process.StandardInput.Flush();

                    result = true;
                }
            }
            catch(Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - WriteCommand", e);
            }

            return result;
        }

        public bool Play(string url)
        {
            bool result = false;

            if (IsStarted)
            {
                if (WriteCommand($"loadfile {url}"))
                {
                    result = true;
                }
            }

            return result;
        }

        public bool Pause(bool pause)
        {
            bool result = false;

            try
            {
                bool toggle = false;

                if(pause && !IsOdd(pauseToggleCount))
                {
                    toggle = true;
                }
                else if(!pause && IsOdd(pauseToggleCount))
                {
                    toggle = true;
                }

                if (toggle)
                {
                    if (IsStarted)
                    {
                        if (WriteCommand("pause"))
                        {
                            result = true;
                            pauseToggleCount++;
                        }
                    }
                    else
                    {
                        result = true;
                        PauseQueue.Enqueue(pause);
                    }                    
                }
                else
                {
                    result = true;
                }
            }
            catch(Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Pause", e);
            }
            
            return result;
        }

        private static bool GetPlayListFlag(string url, out string playlistFlag)
        {
            bool result = false;

            playlistFlag = string.Empty;

            url = url.TrimEnd();

            string[] playlistExtensions = { ".asx", ".m3u", ".m3u8", ".pls", ".plst", ".qtl", ".ram", ".wax", ".wpl", ".xspf" };
            foreach (var playlistExtension in playlistExtensions)
            {
                if (url.EndsWith(playlistExtension) || url.EndsWith(playlistExtension + "\""))
                {
                    playlistFlag = " -playlist ";
                    result = true;
                    break;
                }
            }

            return result;
        }

        public bool Create(string url)
        {
            bool result = false;

            try
            {
                var startInfo = new ProcessStartInfo();

                _process = new Process
                {
                    EnableRaisingEvents = true
                };

                _process.Exited += OnProcessExited;

                startInfo.UseShellExecute = false;

                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardInput = true;

                startInfo.WindowStyle = ProcessWindowStyle.Normal;

                string args = $" -slave -quiet";

                GetPlayListFlag(url, out string playlistFlag);

                startInfo.Arguments = Settings.AppArgs + playlistFlag  + args + $" {url}";

                startInfo.Arguments = startInfo.Arguments.Trim();

                startInfo.FileName = Settings.MPlayerProcessPath;

                _process.StartInfo = startInfo;

                _process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Parser?.ProcessLine(args.Data);
                    }
                };

                _process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        MsgLogger.WriteError($"{GetType().Name} - ErrorDataReceived", args.Data);
                    }
                };

                if (_process.Start())
                {
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();

                    ProcessId = _process.Id;

                    result = true;
                }

                MsgLogger.WriteFlow($"{GetType().Name} - Start", $"Start process: app = {startInfo.FileName},  args: {startInfo.Arguments}, result = {_process != null}");
            }
            catch(Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Create", e);
            }

            return result;
        }

        #endregion
    }
}
