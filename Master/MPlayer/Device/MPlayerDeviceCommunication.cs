using EltraMaster.DeviceManager.Events;
using System;
using EltraConnector.Master.Device;
using System.Threading.Tasks;
using MPlayerMaster.Device.Runner;
using MPlayerMaster.Device.Media;
using MPlayerMaster.Device.Radio;
using EltraCommon.Logger;
using MPlayerCommon.Definitions;

namespace MPlayerMaster.Device
{
    public class MPlayerDeviceCommunication : MasterDeviceCommunication
    {
        #region Private fields

        private MediaPlayer _mediaPlayer;
        private RadioPlayer _radioPlayer;
        private PlayerControl _playerControl;

        private MPlayerSettings _settings;
        
        #endregion

        #region Constructors

        public MPlayerDeviceCommunication(MasterDevice device, MPlayerSettings settings)
            : base(device)
        {
            _settings = settings;
        }

        #endregion

        #region Properties

        private MediaPlayer MediaPlayer => _mediaPlayer ?? (_mediaPlayer = CreateMediaPlayer());

        private RadioPlayer RadioPlayer => _radioPlayer ?? (_radioPlayer = CreateRadioPlayer());

        private PlayerControl PlayerControl => _playerControl ?? (_playerControl = CreatePlayerControl());

        #endregion

        #region Init

        private MediaPlayer CreateMediaPlayer()
        {
            var result = new MediaPlayer()
            {
                Settings = _settings,
                PlayerControl = PlayerControl,
                Vcs = Vcs
            };

            return result;
        }

        internal object ControlMedia(MediaControlWordValue state)
        {
            bool result = MediaPlayer.ControlMedia(state);

            return result;
        }

        private RadioPlayer CreateRadioPlayer()
        {
            var result = new RadioPlayer()
            {
                Settings = _settings,
                PlayerControl = PlayerControl,
                Vcs = Vcs
            };
            
            return result;
        }

        private PlayerControl CreatePlayerControl()
        {
            var result = new PlayerControl() { Vcs = Vcs, Settings = _settings };

            return result;
        }

        protected override async void OnInitialized()
        {
            MsgLogger.WriteLine($"device (node id={Device.NodeId}) initialized, processing ...");

            await PlayerControl.InitParameters();

            await MediaPlayer.InitParameters();

            await RadioPlayer.InitParameters();

            base.OnInitialized();
        }

        #endregion

        #region Events

        protected override void OnStatusChanged(DeviceCommunicationEventArgs e)
        {
            Console.WriteLine($"status changed, status = {e.Device.Status}, error code = {e.LastErrorCode}");

            base.OnStatusChanged(e);
        }

        #endregion

        #region Methods

        #region SDO

        public override bool GetObject(ushort objectIndex, byte objectSubindex, ref byte[] data)
        {
            bool result = base.GetObject(objectIndex, objectSubindex, ref data);

            if(PlayerControl.GetObject(objectIndex, objectSubindex, ref data))
            {
                result = true;
            }
            else if (MediaPlayer.GetObject(objectIndex, objectSubindex, ref data))
            {
                result = true;
            }
            else if (RadioPlayer.GetObject(objectIndex, objectSubindex, ref data))
            {
                result = true;
            }

            return result;
        }

        public override bool SetObject(ushort objectIndex, byte objectSubindex, byte[] data)
        {
            bool result = false;

            if (PlayerControl.SetObject(objectIndex, objectSubindex, data))
            {
                result = true;
            }
            else if (MediaPlayer.SetObject(objectIndex, objectSubindex, data))
            {
                result = true;
            }
            else if (RadioPlayer.SetObject(objectIndex, objectSubindex, data))
            {
                result = true;
            }

            return result;
        }

        #endregion

        public string QueryStation(string query)
        {
            var result = string.Empty;

            var t = Task.Run(async ()=> {

                result = await RadioPlayer.QueryStation(query);
                           
            });

            t.Wait();

            return result;
        }

        #endregion
    }
}
