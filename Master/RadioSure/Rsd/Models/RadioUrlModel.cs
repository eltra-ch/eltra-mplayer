using System.Runtime.Serialization;

namespace RadioSureMaster.Rsd.Models
{
    [DataContract]
    public class RadioUrlModel
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
