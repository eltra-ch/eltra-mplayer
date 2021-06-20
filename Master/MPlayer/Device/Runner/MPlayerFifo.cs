using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Runner.Console;
using System;
using System.Diagnostics;

namespace MPlayerMaster.Device.Runner
{
    class MPlayerFifo
    {
        #region Private fields

        const int EXIT_CODE_SUCCESS = 0;

        private Process _process;
        private MPlayerConsoleParser _parser;

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

        private void OnProcessExited(object sender, EventArgs e)
        {
            
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
            var result = Mute(true);

            return result;
        }

        public bool Mute(bool mute)
        {
            bool result = false;

            try
            {
                int exitCode = EXIT_CODE_SUCCESS;
                int m = mute ? 1 : 0;
                string processName = "echo";
                var startInfo = new ProcessStartInfo($"{processName}", $"echo mute {m} >> {Path}");

                var process = Process.Start(startInfo);

                if (process != null)
                {
                    process.WaitForExit();

                    exitCode = process.ExitCode;

                    MsgLogger.WriteFlow($"{GetType().Name} - Mute", $"process {processName} exit code = {exitCode}");
                }
                else
                {
                    MsgLogger.WriteError($"{GetType().Name} - Mute", $"cannot start {processName} process!");
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
                var processId = Start(url);

                SetProcessId(processId);

                if (processId != -1)
                {
                    Url = url;
                    result = true;                    
                }
            }
            else
            {
                result = Mute(false);
            }

            return result;
        }

        private bool SetProcessId(int processId)
        {
            bool result = false;

            if (ProcessIdParameter != null && processId >= 0)
            {
                if (!ProcessIdParameter.SetValue(processId))
                {
                    MsgLogger.WriteError($"{GetType().Name} - SetProcessId", $"process id {processId} cannot be set");
                }
                else
                {
                    result = true;
                }
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

                var startInfo = new ProcessStartInfo();

                _process = new Process
                {
                    EnableRaisingEvents = true
                };

                _process.Exited += OnProcessExited;

                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;

                startInfo.WindowStyle = ProcessWindowStyle.Normal;

                string args = $" -slave -input file={Path}";

                startInfo.Arguments = Settings.AppArgs + args + $" {url}";
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

                Mute(true);

                MsgLogger.WriteFlow($"{GetType().Name} - Start", $"Start process: app = {startInfo.FileName},  args: {startInfo.Arguments}, result = {_process != null}");
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Start", e);
            }

            return result;
        }

        private void TerminateProcess()
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
            }
            catch(Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - TerminateProcess", e);
            }

            try
            {
                string args = $"-input file={Path}";

                foreach (var p in Process.GetProcessesByName(Settings.MPlayerProcessName))
                {
                    if (p.StartInfo.Arguments.Contains(Path))
                    {
                        p.Kill();
                    }                    
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - CloseBruteForce", e);
            }
        }

        #endregion
    }
}
