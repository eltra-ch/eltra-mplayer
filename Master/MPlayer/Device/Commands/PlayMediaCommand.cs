using System;
using EltraCommon.Contracts.CommandSets;
using EltraCommon.Contracts.Devices;

namespace MPlayerMaster.Device.Commands
{
    public class PlayMediaCommand : DeviceCommand
    {
        public PlayMediaCommand()
        {
        }

        public PlayMediaCommand(EltraDevice device)
            : base(device)
        {
            Name = "PlayMedia";

            //Result
            AddParameter("Result", TypeCode.String, ParameterType.Out);
            AddParameter("ErrorCode", TypeCode.UInt32, ParameterType.Out);
        }

        public override DeviceCommand Clone()
        {
            Clone(out PlayMediaCommand result);

            return result;
        }

        public override bool Execute(string channelId, string loginName)
        {
            bool result = false;
            var eposDevice = Device as MPlayerDevice;
            var communication = eposDevice?.Communication;
            
            if (communication is MPlayerDeviceCommunication deviceCommunication)
            {
                var commandResult = deviceCommunication.PlayMedia();

                SetParameterValue("ErrorCode", communication.LastErrorCode);
                SetParameterValue("Result", commandResult);

                result = true;
            }

            return result;
        }
    }
}
