using EltraConnector.Master.Device;
using MPlayerMaster.Device.Runner;
using System;

namespace MPlayerMaster.Device.Players
{
    class Player
    {
        #region Private fields

        private PlayerControl _playerControl;

        #endregion

        #region Events

        private void OnPlayerControlChanged()
        {
            PlayerControl.MPlayerProcessExited -= OnMPlayerProcessExited;
            PlayerControl.MPlayerProcessExited += OnMPlayerProcessExited;

            PlayerControl.AddPlayer(this);
        }

        protected virtual void OnMPlayerProcessExited(object sender, EventArgs e)
        {

        }

        #endregion

            #region Properties

        public MasterVcs Vcs { get; internal set; }

        public MPlayerSettings Settings { get; set; }

        public MPlayerRunner Runner { private get; set; }

        public PlayerControl PlayerControl
        {
            get => _playerControl;
            set
            {
                _playerControl = value;
                OnPlayerControlChanged();
            }
        }

        #endregion
    }
}
