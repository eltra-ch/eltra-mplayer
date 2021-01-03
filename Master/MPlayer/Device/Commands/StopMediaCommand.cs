using System;
using EltraCommon.Contracts.CommandSets;
using EltraCommon.Contracts.Devices;

namespace MPlayerMaster.Device.Commands
{
    public class StopMediaCommand : DeviceCommand
    {
        public StopMediaCommand()
        {
        }

        public StopMediaCommand(EltraDevice device)
            : base(device)
        {
            Name = "StopMedia";

            //Result
            AddParameter("Result", TypeCode.String, ParameterType.Out);
            AddParameter("ErrorCode", TypeCode.UInt32, ParameterType.Out);
        }

        public override DeviceCommand Clone()
        {
            Clone(out StopMediaCommand result);

            return result;
        }

        public override bool Execute(string channelId, string loginName)
        {
            bool result = false;
            var eposDevice = Device as MPlayerDevice;
            var communication = eposDevice?.Communication;
            
            if (communication is MPlayerDeviceCommunication deviceCommunication)
            {
                var commandResult = deviceCommunication.StopMedia();

                SetParameterValue("ErrorCode", communication.LastErrorCode);
                SetParameterValue("Result", commandResult);

                result = true;
            }

            return result;
        }
    }
}
