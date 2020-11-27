using EltraConnector.Master.Device;

namespace RadioSureMaster.Device
{
    class RadioSureDeviceManager : MasterDeviceManager
    {
        public RadioSureDeviceManager(string deviceDescriptionFilePath, RadioSureSettings settings)
        {
            AddDevice(new RadioSureDevice(deviceDescriptionFilePath, 1, settings));
        }
    }
}
