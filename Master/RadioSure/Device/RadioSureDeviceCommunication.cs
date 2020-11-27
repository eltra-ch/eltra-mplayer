using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraMaster.DeviceManager.Events;
using System;
using EltraConnector.Master.Device;
using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;

using static RadioSureMaster.RadioSureDefinitions;
using RadioSureMaster.Rsd;

namespace RadioSureMaster.Device
{
    public class RadioSureDeviceCommunication : MasterDeviceCommunication
    {
        #region Private fields

        private Parameter _statusWordParameter;        
        private RadioSureSettings _settings;        
        private RsdManager _rsdManager;

        #endregion

        #region Constructors

        public RadioSureDeviceCommunication(MasterDevice device, RadioSureSettings settings)
            : base(device)
        {
            _settings = settings;
        }

        #endregion

        #region Properties

        internal RsdManager RsdManager => _rsdManager ?? (_rsdManager = CreateRsdManager());

        #endregion

        #region Init

        private RsdManager CreateRsdManager()
        {
            var result = new RsdManager() { RsdxUrl = _settings.RsdxUrl };

            return result;
        }

        protected override void OnInitialized()
        {
            Console.WriteLine($"device (node id={Device.NodeId}) initialized, processing ...");

            RsdManager.Init(Vcs);

            InitStateMachine();

            base.OnInitialized();
        }

        private void InitStateMachine()
        {
            _statusWordParameter = Vcs.SearchParameter("PARAM_StatusWord") as XddParameter;

            if(!SetExecutionStatus(StatusWordEnums.Waiting))
            {
                MsgLogger.WriteError($"{GetType().Name} - InitStateMachine", "Set execution state (waiting) failed!");
            }
        }

        #endregion

        #region Events

        #endregion

        #region SDO

        public override bool GetObject(ushort objectIndex, byte objectSubindex, ref byte[] data)
        {
            bool result = false;

            //PARAM_StatusWord
            if (objectIndex == 0x6041)
            {
                if (_statusWordParameter.GetValue(out byte[] v))
                {
                    data = v;
                    result = true;
                }
            } 
            
            return result;
        }

        public override bool SetObject(ushort objectIndex, byte objectSubindex, byte[] data)
        {
            bool result = false;

            return result;
        }

        #endregion

        #region Events

        protected override void OnStatusChanged(DeviceCommunicationEventArgs e)
        {
            Console.WriteLine($"status changed, status = {e.Device.Status}, error code = {e.LastErrorCode}");

            base.OnStatusChanged(e);
        }

        private bool SetExecutionStatus(StatusWordEnums status)
        {
            bool result = false;

            if(_statusWordParameter!=null)
            {
                result = _statusWordParameter.SetValue((ushort)status);
            }

            return result;
        }

        public string QueryStation(string query)
        {
            var result = RsdManager.QueryStation(query);

            return result;
        }

        #endregion
    }
}
