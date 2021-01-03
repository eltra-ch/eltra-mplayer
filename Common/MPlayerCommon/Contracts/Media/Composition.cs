using EltraCommon.Helpers;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace MPlayerCommon.Contracts.Media
{
    [DataContract]
    public class Composition
    {
        #region Constructors

        public Composition()
        {
        }

        public Composition(string path)
        {
            FullPath = path;
        }

        #endregion

        #region Properties

        [IgnoreDataMember]
        [JsonIgnore]
        public string FullPath { get; set; }

        [IgnoreDataMember]
        [JsonIgnore]
        public string FileName { get; set; }

        [DataMember]
        public string Extension { get; set; }

        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public int Position { get; set; }

        #endregion

        #region Methods

        private string GetId()
        {
            return CryptHelpers.ToMD5(FullPath);
        }

        private int GetPosition()
        {
            int result = 0;
            int prefixLength = 3;

            if (!string.IsNullOrEmpty(FullPath))
            {
                if (FileName.Length > prefixLength)
                {
                    var positionAsString = FileName.Substring(0, prefixLength- 1);

                    if(int.TryParse(positionAsString, out int p))
                    {
                        result = p;
                    }
                }
            }

            return result;
        }

        private string GetTitle()
        {
            string result = string.Empty;
            int prefixLength = 3;

            if(!string.IsNullOrEmpty(FullPath))
            {
                if (FileName.Length > prefixLength)
                {
                    result = FileName.Substring(prefixLength, FileName.Length - Extension.Length - prefixLength);
                }
            }

            return result;
        }

        public void Build()
        {            
            FileName = Path.GetFileName(FullPath);
            Extension = Path.GetExtension(FullPath);
            Title = GetTitle();
            Position = GetPosition();
        }

        #endregion
    }
}
