using EltraCommon.Logger;
using MPlayerMaster.Device.Radio;
using System;
using System.Linq;

namespace MPlayerMaster.Device.Runner.Console
{
    class MPlayerConsoleParser
    {
        #region Private fields

        private string _streamTitle;

        #endregion

        #region Properties

        public RadioPlayer RadioPlayer { get; set; }
        
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
        
        #endregion

        private static string RemoveWhitespace(string input)
        {
            string result = string.Empty;

            if (!string.IsNullOrWhiteSpace(input))
            {
                result = new string(input.ToCharArray()
                .Where(c => !Char.IsWhiteSpace(c))
                .ToArray());
            }

            return result;
        }

        public void ProcessLine(string line)
        {
            try
            {
                var formattedLine = RemoveWhitespace(line).ToLower();

                if (formattedLine.StartsWith("name:"))
                {
                    ParseMPlayerStationName(line);
                }
                else if (formattedLine.StartsWith("icyinfo:streamtitle"))
                {
                    ParseMPlayerStreamTitle(line);
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - ParseMPlayerOutputLine", e);
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

                                if (ActiveStationValue > 0)
                                {
                                    int index = ActiveStationValue - 1;

                                    var streamTitleParameters = RadioPlayer.StreamTitleParameters;

                                    if (streamTitleParameters != null && !streamTitleParameters[index].SetValue(streamTitle))
                                    {
                                        MsgLogger.WriteError($"{GetType().Name} - ParseMPlayerStationName", $"cannot set new stream title: {streamTitle}");
                                    }
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

                        if (ActiveStationValue > 0)
                        {
                            int index = ActiveStationValue - 1;

                            var customStationTitleParameters = RadioPlayer?.CustomStationTitleParameters;

                            if (customStationTitleParameters != null && customStationTitleParameters[index].GetValue(out string customTitle))
                            {
                                if (!string.IsNullOrEmpty(customTitle))
                                {
                                    stationName = customTitle;
                                }

                                var stationTitleParameters = RadioPlayer?.StationTitleParameters;

                                if (stationTitleParameters!=null && !stationTitleParameters[index].SetValue(stationName))
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
    }
}
