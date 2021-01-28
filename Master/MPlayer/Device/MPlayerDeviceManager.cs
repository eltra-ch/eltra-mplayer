using EltraCommon.Os.Linux;
using EltraConnector.Master.Device;
using MPlayerMaster.Device.Runner.Wrapper;

namespace MPlayerMaster.Device
{
    class MPlayerDeviceManager : MasterDeviceManager
    {
        public MPlayerDeviceManager(string deviceDescriptionFilePath, MPlayerSettings settings)
        {
            if (SystemHelper.IsLinux)
            {
                EltraRelayWrapper.Initialize();
                EltraRelayWrapper.RelayPinMode((ushort)settings.RelayGpioPin, EltraRelayWrapper.GPIOpinmode.Output);
            }

            AddDevice(new MPlayerDevice(deviceDescriptionFilePath, 1, settings));
        }
    }
}
