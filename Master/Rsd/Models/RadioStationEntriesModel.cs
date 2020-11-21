using MPlayerCommon.Contracts;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MPlayerMaster.Rsd.Models
{
    [DataContract]
    class RadioStationEntriesModel
    {
        private List<RadioStationModel> _entries;

        [DataMember]
        public List<RadioStationModel> Entries => _entries ?? (_entries = new List<RadioStationModel>());

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Md5 { get; set; }

        [IgnoreDataMember]
        public int Count => Entries.Count;

        internal void AddEntries(List<RadioStationEntry> entries)
        {
            foreach(var entry in entries)
            {
                Entries.Add(new RadioStationModel() { Entry = entry });
            }            
        }
    }
}
