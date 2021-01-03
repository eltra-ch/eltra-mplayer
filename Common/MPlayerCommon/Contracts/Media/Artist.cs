using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace MPlayerCommon.Contracts.Media
{
    [DataContract]
    public class Artist
    {
        #region Private fields

        private List<Album> _albums;

        #endregion

        #region Constructors

        public Artist()
        {
        }

        public Artist(string path)
        {
            FullPath = path;
        }

        #endregion

        #region Properties

        [DataMember]
        public string Name { get; set; }

        [IgnoreDataMember]
        [JsonIgnore]
        public string FullPath { get; set; }

        [DataMember]
        public List<Album> Albums
        {
            get => _albums ?? (_albums = new List<Album>());
            set
            {
                _albums = value;
            }
        }

        #endregion

        #region Methods

        private void Add(Album album)
        {
            album.Build();

            Albums.Add(album);
        }

        private string GetName()
        {
            string result = string.Empty;

            if(!string.IsNullOrEmpty(FullPath))
            {
                result = Path.GetFileName(FullPath);
            }

            return result;
        }

        public void Build()
        {
            Name = GetName();

            var paths = Directory.GetDirectories(FullPath);

            foreach (var path in paths)
            {
                var album = new Album(path);

                Add(album);
            }
        }

        #endregion
    }
}
