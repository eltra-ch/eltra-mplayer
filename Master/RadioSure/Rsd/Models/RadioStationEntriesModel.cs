using MPlayerCommon.Contracts;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RadioSureMaster.Rsd.Models
{
    [DataContract]
    public class RadioStationEntriesModel
    {
        private List<RadioStationModel> _entries;

        [DataMember]
        public List<RadioStationModel> Entries => _entries ?? (_entries = new List<RadioStationModel>());

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Md5 { get; set; }

        [IgnoreDataMember]
        [JsonIgnore]
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
