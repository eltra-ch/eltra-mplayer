using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using MPlayerMaster.Device.Players;
using MPlayerMaster.Device.Runner.Console;
using System;
using System.Collections.Generic;

namespace MPlayerMaster.Device.Runner
{
    internal class MPlayerRunner
    {
        #region Private fields

        private MPlayerFifoManager _fifoManager;
        private List<Player> _playerList;

        #endregion

        #region Constructors

        public MPlayerRunner()
        {
        }

        #endregion

        #region Properties

        public MPlayerSettings Settings { get; set; }

        private MPlayerFifoManager FifoManager => _fifoManager ?? (_fifoManager = new MPlayerFifoManager() { Settings = Settings });

        public List<Player> PlayerList
        {
            get => _playerList ?? (_playerList = new List<Player>());
        }

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

        public void CreateFifo(Player player, ushort index, Parameter streamTitleParameter,
                                                          Parameter stationTitleParameter,
                                                          Parameter customStationTitleParameter)
        {
            if (!FifoManager.Create(player, index, streamTitleParameter, stationTitleParameter, customStationTitleParameter))
            {
                MsgLogger.WriteError($"{GetType().Name} - CreateFifo", $"Fifo creation for index = {index} failed!");
            }
        }

        internal void AddPlayer(Player player)
        {
            PlayerList.Add(player);
        }

        public bool Play(ushort index, string url)
        {
            return FifoManager.Play(index, url);
        }

        private MPlayerConsoleParser CreateParser()
        {
            var result = new MPlayerConsoleParser();

            return result;
        }

        public bool StopFifo()
        {
            return FifoManager.Stop();
        }

        #endregion
    }
}
