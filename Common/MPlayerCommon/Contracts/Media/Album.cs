using EltraCommon.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace MPlayerCommon.Contracts.Media
{
    [DataContract]
    public class Album
    {
        #region Private fields

        private List<Composition> _compositions;

        #endregion

        #region Constructors

        public Album()
        {
        }

        public Album(string path)
        {
            FullPath = path;
        }

        #endregion

        #region Properties

        [DataMember]
        public string Id { get; set; }

        [IgnoreDataMember]
        [JsonIgnore]
        public string FullPath { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public List<Composition> Compositions
        {
            get => _compositions ?? (_compositions = new List<Composition>());
            set
            {
                _compositions = value;
            }
        }

        #endregion

        #region Methods

        private string GetId()
        {
            return CryptHelpers.ToMD5(FullPath);
        }

        private void Add(Composition composition)
        {
            composition.Build();

            Compositions.Add(composition);
        }

        private string GetName()
        {
            string result = string.Empty;

            if (!string.IsNullOrEmpty(FullPath))
            {
                result = Path.GetFileName(FullPath);
            }

            return result;
        }

        public void Build()
        {
            Id = GetId();
            Name = GetName();

            var paths = Directory.GetFiles(FullPath, "*.mp3", SearchOption.AllDirectories);

            foreach (var path in paths)
            {
                var composition = new Composition(path);

                Add(composition);
            }

            Tag();
        }

        private void Tag()
        {
            if (Compositions.Count > 0)
            {
                string m3uFile = Path.Combine(FullPath, "album.m3u");

                string content = string.Empty;

                var sortedCompositions = Compositions.OrderBy(o => o.FileName).ToList();

                foreach (var composition in sortedCompositions)
                {
                    content += composition.FileName + Environment.NewLine;
                }

                File.WriteAllText(m3uFile, content);
            }
        }

        #endregion
    }
}
