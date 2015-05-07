using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PlayReadyDynamicEncryption
{
    class Program
    {
        private static string _mediaServiceAccountName = ConfigurationManager.AppSettings["MediaServiceAccountName"];
        private static string _mediaServiceAccountKey = ConfigurationManager.AppSettings["MediaServiceAccountKey"];
        private static string _merchant = ConfigurationManager.AppSettings["CastLabs.Merchant"];

        private static CloudMediaContext _context = null;
        private static MediaServicesCredentials _cachedCredentials = null;

        static void Main(string[] args)
        {
            // Create and cache the Media Services credentials in a static class variable.
            _cachedCredentials = new MediaServicesCredentials(
                            _mediaServiceAccountName,
                            _mediaServiceAccountKey);
            // Used the chached credentials to create CloudMediaContext.
            _context = new CloudMediaContext(_cachedCredentials);

            // Get the asset that we are going to protect
            var objIAsset = _context.Assets.Where(x => x.Id == "nb:cid:UUID:dac53a5d-1500-80bd-b864-f1e4b62594cf").FirstOrDefault();

            Console.WriteLine("Removing Locators");
            RemoveILocators(objIAsset.Id); //unpublish
            
            Console.WriteLine("Removing dynamic PlayReady protection");
            string oldKey = CryptoUtils.RemoveDynamicPlayReadyProtection(_context, objIAsset); //remove protection

            Console.WriteLine("Remove key from CastLabs");
            if(!string.IsNullOrEmpty(oldKey)) CastLabs.DeleteKey(ConvertKeyToGuid(oldKey)); //remove from CastLabs

            Console.WriteLine("Setup dynamic PlayReady protection");
            string assetId = "MyAsset4";
            var keys = CryptoUtils.SetupDynamicPlayReadyProtection(_context, objIAsset); //set protection
            Console.WriteLine("Content Key ID: {0}", keys.KeyId);

            Console.WriteLine("Adding Locators");
            GetStreamingOriginLocator(objIAsset); //publish

            Console.WriteLine("Adding key to CastLabs");
            CastLabs.IngestKey(assetId, "", keys.Key, keys.KeyId); //ingest key into CastLabs

            var jwt = ContentKeyAuthorizationHelper.GeneratePlaybackToken(assetId, _merchant, keys.KeyId); //get the JWT playback token
            Console.WriteLine("TOKEN:\r\n{0}\r\n", jwt);

            Console.ReadLine();
        }

        // Castlabs expects just the Guid part from the AMS keys
        // This function takes an AMS key, and only leaves the Guid part in the end.
        private static Guid ConvertKeyToGuid(string fullAMSKey)
        {
            string[] parts = fullAMSKey.Split(':');

            if (parts.Length == 4)
            {
                string key = fullAMSKey.Split(':')[3];
                Guid gKey = Guid.Parse(key);

                return gKey;
            }
            else throw new ArgumentException("Invalid input. Expected input like nb:kid:UUID:8bd40b3c-d456-4500-8647-dbbe990f7af1");
        }

        //the code for removing locators. 
        public static void RemoveILocators(string assetId)
        {
            var locators = _context.Locators.Where(l => l.AssetId == assetId);
            foreach (ILocator objILocator in locators)
            {
                objILocator.Delete();
            }
        }

        static public string GetStreamingOriginLocator(IAsset asset)
        {

            // Get a reference to the streaming manifest file from the  
            // collection of files in the asset. 

            var assetFile = asset.AssetFiles.Where(f => f.Name.ToLower().
                                        EndsWith(".ism")).
                                        FirstOrDefault();

            // Create a 30-day readonly access policy. 
            IAccessPolicy policy = _context.AccessPolicies.Create("Streaming policy",
                TimeSpan.FromDays(30),
                AccessPermissions.Read);

            // Create a locator to the streaming content on an origin. 
            ILocator originLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset,
                policy,
                DateTime.UtcNow.AddMinutes(-5));

            // Create a URL to the manifest file. 
            var streamurl =  originLocator.Path + assetFile.Name + "/Manifest";

            Console.WriteLine("URL:\r\n{0}\r\n", streamurl);

            return streamurl;
        }
    }
}
