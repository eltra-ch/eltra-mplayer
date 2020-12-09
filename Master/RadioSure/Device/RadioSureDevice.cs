using EltraCommon.Contracts.Parameters;
using EltraConnector.Master.Device;
using System;
using RadioSureMaster.Device.Commands;

namespace RadioSureMaster.Device
{
    internal class RadioSureDevice : MasterDevice
    {
        private RadioSureSettings _settings;

        public RadioSureDevice(string deviceDescriptionFilePath, int nodeId, RadioSureSettings settings) 
            : base("RADIOSURE", deviceDescriptionFilePath, nodeId)
        {
            _settings = settings;

            Identification.SerialNumber = 0x102;

            AddCommand(new QueryStationCommand(this));
        }

        protected override void OnStatusChanged()
        {
            Console.WriteLine($"device (node id = {NodeId}) status changed: new status = {Status}");

            base.OnStatusChanged();
        }

        protected override void CreateCommunication()
        {
            var communication = new RadioSureDeviceCommunication(this, _settings);

            Communication = communication;
        }

        public override int GetUpdateInterval(ParameterUpdatePriority priority)
        {
            int result;

            switch (priority)
            {
                case ParameterUpdatePriority.High:
                    result = 10000;
                    break;
                case ParameterUpdatePriority.Medium:
                    result = 30000;
                    break;
                case ParameterUpdatePriority.Low:
                    result = 60000;
                    break;
                case ParameterUpdatePriority.Lowest:
                    result = 180000;
                    break;
                default:
                    result = 120000;
                    break;
            }

            return result;
        }
    }
}
