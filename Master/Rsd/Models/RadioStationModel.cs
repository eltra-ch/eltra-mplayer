using System;
using System.Collections.Generic;
using MPlayerCommon.Contracts;

namespace MPlayerMaster.Rsd.Models
{
    class RadioStationModel
    {
        #region Private fields

        private RadioStationEntry _entry;
        private List<RadioUrlModel> _urls;

        #endregion

        #region Properties

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

        public string Name => Entry?.Name;

        public string Genre => Entry?.Genre;

        public string Country => Entry?.Country;

        public string Language => Entry?.Language;

        public List<RadioUrlModel> Urls => _urls ?? (_urls = new List<RadioUrlModel>());
        
        public bool IsValid { get; set; }

        public DateTime LastValidation { get; set; }

        #endregion

        #region Methods

        private void OnEntryChanged()
        {
            LastValidation = DateTime.MinValue;
            IsValid = true;

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