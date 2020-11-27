using System.Runtime.Serialization;

namespace MPlayerMaster.Rsd.Models
{
    [DataContract]
    class RadioUrlModel
    {
        public RadioUrlModel()
        {
            IsValid = true;
        }

        [DataMember]
        public string Uri { get; set; }

        [DataMember]
        public bool IsValid { get; set; }
    }
}
