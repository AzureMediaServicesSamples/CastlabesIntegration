using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace PlayReadyDynamicEncryption
{
    [DataContract]
    public class KeyIngest
    {
        [DataMember(Name = "assetId")]
        public string AssetId { get; set; }

        [DataMember(Name = "variantId")]
        public string VariantId { get; set; }

        [DataMember(Name = "key")]
        public string Key { get; set; }

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "keyId")]
        public string KeyId { get; set; }

        [DataMember(Name = "streamType")]
        public string StreamType { get; set; }
    }
}
