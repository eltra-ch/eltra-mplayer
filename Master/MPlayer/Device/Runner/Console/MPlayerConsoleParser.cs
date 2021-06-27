using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Players;
using MPlayerMaster.Helpers;
using System;
using System.Collections.Generic;

namespace MPlayerMaster.Device.Runner.Console
{
    class MPlayerConsoleParser
    {
        #region Private fields

        private string _streamTitle;

        #endregion

        #region Properties

        public Parameter StreamTitleParameter { get; set; }
        public Parameter StationTitleParameter { get; set; }
        public Parameter CustomStationTitleParameter { get; set; }

        public bool IsPlaybackStarted { get; private set; }
        public List<Player> PlayerList { get; internal set; }

        #endregion

        #region Events

        public event EventHandler StartingPlayback;

        private void OnStartingPlayback()
        {
            StartingPlayback?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Methods

        public void ProcessLine(string line)
        {
            try
            {
                var formattedLine = StringHelpers.RemoveWhitespace(line).ToLower();

                if (formattedLine.StartsWith("name:"))
                {
                    ParseMPlayerStationName(line);
                }
                else if (formattedLine.StartsWith("icyinfo:streamtitle"))
                {
                    ParseMPlayerStreamTitle(line);
                }
                else if (formattedLine.StartsWith("startingplayback..."))
                {
                    IsPlaybackStarted = true;

                    OnStartingPlayback();
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - ProcessLine", e);
            }
        }

        private void ParseMPlayerStreamTitle(string line)
        {
            try
            {
                int sep = line.IndexOf('=');

                if (sep >= 0 && line.Length > sep + 2)
                {
                    string streamTitle = line.Substring(sep + 2).Trim();

                    if (!string.IsNullOrEmpty(streamTitle))
                    {
                        int end = streamTitle.IndexOf('\'');
                        if (end >= 0 && streamTitle.Length > end)
                        {
                            streamTitle = streamTitle.Substring(0, end);

                            if (!string.IsNullOrEmpty(streamTitle))
                            {
                                if (streamTitle != _streamTitle)
                                {
                                    MsgLogger.WriteLine($"stream title: {streamTitle}");
                                    _streamTitle = streamTitle;
                                }

                                if (StreamTitleParameter != null && !StreamTitleParameter.SetValue(streamTitle))
                                {
                                    MsgLogger.WriteError($"{GetType().Name} - ParseMPlayerStationName", $"cannot set new stream title: {streamTitle}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - ParseMPlayerStreamTitle", e);
            }
        }

        private void ParseMPlayerStationName(string line)
        {
            try
            {
                int sep = line.IndexOf(':');

                if (sep >= 0 && line.Length > sep + 1)
                {
                    string stationName = line.Substring(sep + 1).Trim();

                    if (!string.IsNullOrEmpty(stationName))
                    {
                        MsgLogger.WriteLine($"station name: {stationName}");

                        {
                            if (CustomStationTitleParameter != null && CustomStationTitleParameter.GetValue(out string customTitle))
                            {
                                if (!string.IsNullOrEmpty(customTitle))
                                {
                                    stationName = customTitle;
                                }

                                if (StationTitleParameter != null && !StationTitleParameter.SetValue(stationName))
                                {
                                    MsgLogger.WriteError($"{GetType().Name} - ParseMPlayerStationName", $"cannot set new station title: {stationName}");
                                }
                            }
                            else
                            {
                                MsgLogger.WriteError($"{GetType().Name} - ParseMPlayerStationName", $"cannot get custom station title: {stationName}");
                            }                            
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - ParseMPlayerStationName", e);
            }
        }

        #endregion
    }
}
