using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using MPlayerCommon.Contracts;

namespace RadioSureMaster.Rsd.Models
{
    [DataContract]
    public class RadioStationModel
    {
        #region Private fields

        private RadioStationEntry _entry;
        private List<RadioUrlModel> _urls;

        #endregion

        #region Constructors

        public RadioStationModel()
        {
            IsValid = true;
            LastValidation = DateTime.MinValue;
        }

        #endregion

        #region Properties

        [DataMember]
        public RadioStationEntry Entry
        {
            get => _entry;
            set
            {
                if (_entry != value)
                {
                    _entry = value;

                    OnEntryChanged();
                }
            }
        }

        [IgnoreDataMember]
        [JsonIgnore]
        public string Name => Entry?.Name;

        [IgnoreDataMember]
        [JsonIgnore]
        public string Genre => Entry?.Genre;

        [IgnoreDataMember]
        [JsonIgnore]
        public string Country => Entry?.Country;

        [IgnoreDataMember]
        [JsonIgnore]
        public string Language => Entry?.Language;

        [DataMember]
        public List<RadioUrlModel> Urls => _urls ?? (_urls = new List<RadioUrlModel>());

        [DataMember]
        public bool IsValid { get; set; }

        [DataMember]
        public DateTime LastValidation { get; set; }

        #endregion

        #region Methods

        private void OnEntryChanged()
        {
            if (Entry != null)
            {
                Urls.Clear();

                foreach (var url in Entry.Urls)
                {
                    Urls.Add(new RadioUrlModel { Uri = url });
                }
            }
        }

        #endregion
    }
}