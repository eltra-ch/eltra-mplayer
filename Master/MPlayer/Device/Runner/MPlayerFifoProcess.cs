using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Runner.Console;
using System;
using System.Diagnostics;

namespace MPlayerMaster.Device.Runner
{
    class MPlayerFifoProcess
    {
        #region

        private Process _process;
        private bool _pause;

        #endregion

        #region Properties

        public MPlayerSettings Settings { get; set; }

        public int ProcessId { get; private set; }

        public Parameter ProcessIdParameter { get; internal set; }

        public MPlayerConsoleParser Parser { get; internal set; }

        public bool IsPaused => _pause;

        #endregion

        #region Events

        private void OnProcessExited(object sender, EventArgs e)
        {
        }

        #endregion

        #region Methods

        public bool Pause(bool pause)
        {
            bool result = false;

            try
            {
                if (pause != _pause)
                {
                    if (_process != null)
                    {
                        _process.StandardInput.WriteLine("pause");
                    }

                    _pause = pause;
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
                startInfo.RedirectStandardOutput = true;

                startInfo.WindowStyle = ProcessWindowStyle.Normal;

                string args = $" -slave -quiet";

                startInfo.Arguments = Settings.AppArgs + args + $" {url}";
                startInfo.Arguments = startInfo.Arguments.Trim();

                startInfo.FileName = Settings.MPlayerProcessPath;

                _process.StartInfo = startInfo;

                _process.OutputDataReceived += (sender, args) =>
                {
                    Parser.ProcessLine(args.Data);
                };

                if (_process.Start())
                {
                    _process.BeginOutputReadLine();

                    ProcessId = _process.Id;

                    SetProcessId();

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

        private bool SetProcessId()
        {
            bool result = false;

            if (ProcessIdParameter != null && ProcessId >= 0)
            {
                if (!ProcessIdParameter.SetValue(ProcessId))
                {
                    MsgLogger.WriteError($"{GetType().Name} - SetProcessId", $"process id {ProcessId} cannot be set");
                }
                else
                {
                    result = true;
                }
            }

            return result;
        }

        public void Abort()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.EnableRaisingEvents = false;
                    _process.Exited -= OnProcessExited;

                    _process.Kill();

                    if (!_process.HasExited)
                    {
                        _process.WaitForExit();
                    }
                }

                _process = null;
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Abort", e);
            }
        }

        #endregion
    }
}
