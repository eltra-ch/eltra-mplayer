namespace MPlayerMaster.Rsd.Models
{
    class RadioStationEntrySearch
    {
        public RadioStationEntrySearch(RadioStationModel entry)
        {
            Entry = entry;
        }

        public RadioStationModel Entry { get; set; }

        public bool Contains(string queryWord)
        {
            bool result = false;

            var lowQueryWord = queryWord.ToLower();

            if (Entry.Name.ToLower().Contains(lowQueryWord) ||
                                    Entry.Genre.ToLower().Contains(lowQueryWord) ||
                                    Entry.Country.ToLower().Contains(lowQueryWord) ||
                                    Entry.Language.ToLower().Contains(lowQueryWord))
            {
                result = true;
            }

            return result;      
        }
    }
}
