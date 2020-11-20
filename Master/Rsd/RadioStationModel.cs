using MPlayerCommon.Contracts;

namespace MPlayerMaster.Rsd
{
    class RadioStationModel
    {
        public RadioStationEntry Entry { get; set; }

        public string Name => Entry.Name;

        public string Genre => Entry.Genre;

        public string Country => Entry.Country;

        public string Language => Entry.Language;
    }
}
