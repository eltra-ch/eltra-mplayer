using System;
using EltraCommon.Contracts.CommandSets;
using EltraCommon.Contracts.Devices;
using MPlayerCommon.Definitions;

namespace MPlayerMaster.Device.Commands
{
    public class MediaControlCommand : DeviceCommand
    {
        public MediaControlCommand()
        {
        }

        public MediaControlCommand(EltraDevice device)
            : base(device)
        {
            Name = "MediaControl";

            AddParameter("State", TypeCode.Int16, ParameterType.In);

            //Result
            AddParameter("Result", TypeCode.String, ParameterType.Out);
            AddParameter("ErrorCode", TypeCode.UInt32, ParameterType.Out);
        }

        public override DeviceCommand Clone()
        {
            Clone(out MediaControlCommand result);

            return result;
        }

        public override bool Execute(string channelId, string loginName)
        {
            bool result = false;
            var eposDevice = Device as MPlayerDevice;
            var communication = eposDevice?.Communication;
            
            if (communication is MPlayerDeviceCommunication deviceCommunication)
            {
                int stateValue = -1;

                GetParameterValue("State", ref stateValue);

                var commandResult = deviceCommunication.ControlMedia((MediaControlWordValue)stateValue);

                SetParameterValue("ErrorCode", communication.LastErrorCode);
                SetParameterValue("Result", commandResult);

                result = true;
            }

            return result;
        }
    }
}
