using EltraCommon.Contracts.Parameters;
using EltraConnector.Master.Device;
using System;
using MPlayerMaster.Device.Commands;

namespace MPlayerMaster.Device
{
    internal class MPlayerDevice : MasterDevice
    {
        private MPlayerSettings _settings;

        public MPlayerDevice(string deviceDescriptionFilePath, int nodeId, MPlayerSettings settings) 
            : base("MPLAYER", deviceDescriptionFilePath, nodeId)
        {
            _settings = settings;
            
            DeviceToolPayloadsPath = settings.NavigoPluginsPath;

            Identification.SerialNumber = 0x106;

            AddCommand(new QueryStationCommand(this));
            AddCommand(new MediaControlCommand(this));
        }

        protected override void OnStatusChanged()
        {
            Console.WriteLine($"device (node id = {NodeId}) status changed: new status = {Status}");

            base.OnStatusChanged();
        }

        protected override void CreateCommunication()
        {
            var communication = new MPlayerDeviceCommunication(this, _settings);

            Communication = communication;
        }

        public override int GetUpdateInterval(ParameterUpdatePriority priority)
        {
            int result;

            switch (priority)
            {
                case ParameterUpdatePriority.High:
                    result = 500;
                    break;
                case ParameterUpdatePriority.Medium:
                    result = 750;
                    break;
                case ParameterUpdatePriority.Low:
                    result = 1000;
                    break;
                case ParameterUpdatePriority.Lowest:
                    result = 3000;
                    break;
                default:
                    result = 1000;
                    break;
            }

            return result;
        }
    }
}
