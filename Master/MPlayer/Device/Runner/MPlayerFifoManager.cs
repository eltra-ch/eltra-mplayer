using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Players;
using System;
using System.Collections.Generic;

namespace MPlayerMaster.Device.Runner
{
    class MPlayerFifoManager
    {
        #region Private fields

        private List<MPlayerFifo> _fifoList;
   
        #endregion

        #region Properties

        public MPlayerSettings Settings { get; set; }

        public List<MPlayerFifo> FifoList => _fifoList ?? (_fifoList = new List<MPlayerFifo>());
                
        #endregion

        #region Events

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

        internal bool Create(Player player, ushort index, Parameter streamTitleParameter, 
                                                          Parameter stationTitleParameter,
                                                          Parameter customStationTitleParameter)
        {
            bool result = false;

            try
            {
                if(!Exists(index))
                {
                    var fifo = new MPlayerFifo(player, index) { Settings = Settings };

                    if (streamTitleParameter != null)
                    {
                        fifo.StreamTitleParameter = streamTitleParameter;
                    }

                    if (stationTitleParameter != null)
                    {
                        fifo.StationTitleParameter = stationTitleParameter;
                    }

                    if (customStationTitleParameter != null)
                    {
                        fifo.CustomStationTitleParameter = customStationTitleParameter;
                    }

                    MsgLogger.WriteFlow($"fifo added - {fifo.Name}");

                    if (!fifo.IsCreated)
                    {
                        fifo.Create();
                    }

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

        internal bool Play(ushort index, string url)
        {
            bool result = false;
            var fifo = GetFifo(index);

            if(fifo != null)
            {
                Stop();

                if (!fifo.Play(url))
                {
                    MsgLogger.WriteError($"{GetType().Name} - OpenUrl", $"cannot open url {url}!");
                }
                else
                {
                    MsgLogger.WriteLine($"url {url} opened successfully!");
                    
                    result = true;
                }
            }

            return result;
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
