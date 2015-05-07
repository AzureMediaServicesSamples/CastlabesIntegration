using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace PlayReadyDynamicEncryption
{
    public static class CastLabs
    {
        private static string _merchant = ConfigurationManager.AppSettings["CastLabs.Merchant"];
        private static string _password = ConfigurationManager.AppSettings["CastLabs.Password"];

        public static bool DeleteKey(Guid keyId)
        {
            string keyIdAsHexString = keyId.ToString("N");

            string service = string.Format("/keyId/{0}", keyIdAsHexString);
            var ticket = PostKey(service);

            var myRequest = (HttpWebRequest)WebRequest.Create(string.Format("http://fe.staging.drmtoday.com/frontend/rest/keys/v1/cenc/merchant/{0}/key/keyId/{1}?ticket={2}", _merchant, keyIdAsHexString, ticket));
            myRequest.Method = "DELETE";

            bool result = false;
            try
            {
                var response = myRequest.GetResponse();
                result = true;
            }
            catch(WebException)
            {
                // 409 = key does not exist
                // 401 = not authorized
                // 412 = Precondition failed 

                result = false;
            }
            catch (Exception)
            {
                result = false;
            }
            
            return result;
        }

        private static string ConvertKeyIdToB64(Guid keyId)
        {
            byte[] keyIdAsString = StringToByteArray(keyId.ToString("N"));
            string keyIdAsBase64 = Convert.ToBase64String(keyIdAsString);

            return keyIdAsBase64;
        }

        public static string IngestKey(string assetId, string variantId, string key, Guid keyId, string type = "AES", string streamType = "VIDEO_AUDIO")
        {
            var ticket = PostKey();

            string keyIdAsBase64 = ConvertKeyIdToB64(keyId);

            var keyIngest = new KeyIngest
            {
                AssetId = assetId,
                VariantId = variantId,
                Key = key,
                Type = type,
                KeyId = keyIdAsBase64,
                StreamType = streamType
            };

            string postData = string.Empty;
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(KeyIngest)).WriteObject(stream, keyIngest);
                stream.Position = 0;
                using (StreamReader sr = new StreamReader(stream))
                {
                    postData = sr.ReadToEnd();
                }
            }

            byte[] data = Encoding.ASCII.GetBytes(postData);

            var myRequest = (HttpWebRequest)WebRequest.Create(string.Format("http://fe.staging.drmtoday.com/frontend/rest/keys/v1/cenc/merchant/{0}/key?ticket={1}", _merchant, ticket));
            myRequest.Method = "POST";
            myRequest.ContentType = "application/json";
            myRequest.ContentLength = data.Length;
            using (var newStream = myRequest.GetRequestStream())
            {
                newStream.Write(data, 0, data.Length);
            }

            var response = myRequest.GetResponse();
            string result = string.Empty;
            using (var responseStream = response.GetResponseStream())
            {
                using (var responseReader = new StreamReader(responseStream))
                {
                    result = responseReader.ReadToEnd();
                }
            }

            return result;
        }

        private static string GetCLTicket()
        {
            var postData = string.Format("username={0}&password={1}", _merchant, _password);
            byte[] data = Encoding.ASCII.GetBytes(postData);

            var myRequest = (HttpWebRequest)WebRequest.Create("https://auth.staging.drmtoday.com/cas/v1/tickets");
            myRequest.Method = "POST";
            myRequest.ContentType = "application/x-www-form-urlencoded";
            myRequest.ContentLength = data.Length;
            using (var newStream = myRequest.GetRequestStream())
            {
                newStream.Write(data, 0, data.Length);
            }

            var response = myRequest.GetResponse();
            var location = response.Headers["location"];

            return location;
        }

        private static string PostKey(string service = "")
        {
            var location = GetCLTicket();

            var postData = string.Format("service=https://fe.staging.drmtoday.com/frontend/rest/keys/v1/cenc/merchant/{0}/key{1}", _merchant, service);
            byte[] data = Encoding.ASCII.GetBytes(postData);

            var myRequest = (HttpWebRequest)WebRequest.Create(location);
            myRequest.Method = "POST";
            myRequest.ContentType = "application/x-www-form-urlencoded";
            myRequest.ContentLength = data.Length;
            using (var newStream = myRequest.GetRequestStream())
            {
                newStream.Write(data, 0, data.Length);
            }

            var response = myRequest.GetResponse();
            string result = string.Empty;
            using (var responseStream = response.GetResponseStream())
            {
                using (var responseReader = new StreamReader(responseStream))
                {
                    result = responseReader.ReadToEnd();
                }
            }

            return result;
        }

        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}
