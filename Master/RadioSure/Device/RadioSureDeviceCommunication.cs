using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters;
using EltraMaster.DeviceManager.Events;
using System;
using EltraConnector.Master.Device;
using EltraCommon.Logger;
using EltraCommon.ObjectDictionary.Xdd.DeviceDescription.Profiles.Application.Parameters;

using static RadioSureMaster.RadioSureDefinitions;
using RadioSureMaster.Rsd;
using EltraCommon.ObjectDictionary.Common.DeviceDescription.Profiles.Application.Parameters.Events;

namespace RadioSureMaster.Device
{
    public class RadioSureDeviceCommunication : MasterDeviceCommunication
    {
        #region Private fields

        private Parameter _statusWordParameter;
        private XddParameter _minQueryLengthParameter;
        private XddParameter _maxRadioStationEntries;
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

        public RsdManager RsdManager => _rsdManager ?? (_rsdManager = CreateRsdManager());

        #endregion

        #region Events handling

        private void OnRsdFileNameChanged(object sender, string e)
        {
            var rsdFileNameParameter = Vcs.SearchParameter("PARAM_RsdFileName") as XddParameter;

            if(rsdFileNameParameter!=null)
            {
                rsdFileNameParameter.SetValue(e);
            }
        }

        protected override void OnStatusChanged(DeviceCommunicationEventArgs e)
        {
            Console.WriteLine($"status changed, status = {e.Device.Status}, error code = {e.LastErrorCode}");

            base.OnStatusChanged(e);
        }

        private void OnMinQueryLengthParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if(e.Parameter != null && e.Parameter.GetValue(out byte minQueryLength))
            {
                RsdManager.MinQueryLength = minQueryLength;
            }
        }

        private void OnMaxRadioStationParameterChanged(object sender, ParameterChangedEventArgs e)
        {
            if (e.Parameter != null && e.Parameter.GetValue(out byte maxRadioStations))
            {
                RsdManager.MaxRadioStationEntries = maxRadioStations;
            }
        }

        #endregion

        #region Methods

        protected virtual RsdManager CreateRsdManager()
        {
            var result = new RsdManager() { RsdxUrl = _settings.RsdxUrl };

            result.RsdFileNameChanged += OnRsdFileNameChanged;

            return result;
        }

        protected override void OnInitialized()
        {
            Console.WriteLine($"device (node id={Device.NodeId}) initialized, processing ...");

            RsdManager.Init(Vcs);

            InitStateMachine();

            InitSettings();

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

        private async void InitSettings()
        {
            _maxRadioStationEntries = Vcs.SearchParameter("PARAM_MaxRadioStationEntries") as XddParameter;
            _minQueryLengthParameter = Vcs.SearchParameter("PARAM_MinQueryLength") as XddParameter;

            if (_maxRadioStationEntries != null)
            {
                _maxRadioStationEntries.ParameterChanged += OnMaxRadioStationParameterChanged;

                await _maxRadioStationEntries.UpdateValue();

                if (_maxRadioStationEntries.GetValue(out byte maxRadioStationEntries))
                {
                    RsdManager.MaxRadioStationEntries = maxRadioStationEntries;
                }
            }

            if (_minQueryLengthParameter != null)
            {
                _minQueryLengthParameter.ParameterChanged += OnMinQueryLengthParameterChanged;

                await _minQueryLengthParameter.UpdateValue();

                if(_minQueryLengthParameter.GetValue(out byte minQueryLength))
                {
                    RsdManager.MinQueryLength = minQueryLength;
                }
            }
        }
        
        public override bool GetObject(ushort objectIndex, byte objectSubindex, ref byte[] data)
        {
            bool result = false;
            var parameterEntry = Vcs.SearchParameter(objectIndex, objectSubindex) as XddParameter;

            if (parameterEntry != null)
            {
                //PARAM_StatusWord
                if (objectIndex == 0x6041)
                {
                    if (_statusWordParameter.GetValue(out byte[] v))
                    {
                        data = v;
                        result = true;
                    }
                }
                else if (parameterEntry.UniqueId == "PARAM_RsdFileName")
                {
                    if (parameterEntry.GetValue(out byte[] v))
                    {
                        data = v;
                        result = true;
                    }
                }
                else if (parameterEntry.UniqueId == "PARAM_MinQueryLength")
                {
                    if (parameterEntry.GetValue(out byte[] v))
                    {
                        data = v;
                        result = true;
                    }
                }
                else if (parameterEntry.UniqueId == "PARAM_MaxRadioStationEntries")
                {
                    if (parameterEntry.GetValue(out byte[] v))
                    {
                        data = v;
                        result = true;
                    }
                }
            }

            return result;
        }

        public override bool SetObject(ushort objectIndex, byte objectSubindex, byte[] data)
        {
            bool result = false;
            var parameterEntry = Vcs.SearchParameter(objectIndex, objectSubindex) as XddParameter;

            if (parameterEntry != null)
            {
                if (parameterEntry.UniqueId == "PARAM_MinQueryLength")
                {
                    if (parameterEntry.SetValue(data))
                    {                        
                        result = true;
                    }
                }
                else if (parameterEntry.UniqueId == "PARAM_MaxRadioStationEntries")
                {
                    if (parameterEntry.SetValue(data))
                    {
                        result = true;
                    }
                }
            }

            return result;
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
