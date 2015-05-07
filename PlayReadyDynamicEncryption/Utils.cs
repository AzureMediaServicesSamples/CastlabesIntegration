using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using System.Security.Cryptography;
using System.Net;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens;


namespace PlayReadyDynamicEncryption
{
    // This class was taken from William Zhangs example
    // Please refer to this blog for more info: http://azure.microsoft.com/blog/2014/11/17/an-end-to-end-prototype-of-playready-protection-with-acs-authentication-and-acs-token-authorization/
    public class CryptoUtils
    {
        //given an unprotected IAsset, set up dynamic PR protection
        //you have to publish AFTER this setup
        public static Keys SetupDynamicPlayReadyProtection(CloudMediaContext objCloudMediaContext, IAsset objIAsset)
        {
            string keySeedB64, contentKeyB64;
            Guid keyId = Guid.NewGuid();
            //Different ways to create content key:
            //Method 1: Without using key seed, generete content key directly
            //contentKeyB64 = GeneratePlayReadyContentKey();
            //Method 2: With a given key seed and generated key ID (Key Identifiers are unique in the system and there can only be one key with a given Guid within a cluster (even across accounts for now although that may change to be account scoped in the future).  If you try to submit a protection job with a keyId that already exists but a different key value that will cause the PlayReady protection job to fail (the same keyId and keyValue is okay). 
            keySeedB64 = "XVBovsmzhP9gRIZxWfFta3VVRPzVEWmJsazEJ46I";
            contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            //Method 3: With a randomly generated key seed, create content key from the key ID and key seed
            //keySeedB64 = GeneratePlayReadyKeySeed();
            //contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            //Method 4: Reuse an existing key ID
            //keyId = new Guid("a7586184-40ff-4047-9edd-6a8273ac50fc");
            //keySeedB64 = "XVBovsmzhP9gRIZxWfFta3VVRPzVEWmJsazEJ46I";
            //contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            //Console.WriteLine(string.Format("STEP 1: Key ID = {0}, Content Key = {1}, Key Seed = {2}", contentKeyB64, keyId.ToString(), keySeedB64));

            IContentKey objIContentKey = ConfigureKeyDeliveryService(objCloudMediaContext, keyId, contentKeyB64);

            //associate IAsset with the IContentkey
            objIAsset.ContentKeys.Add(objIContentKey);
            CreateAssetDeliveryPolicy(objCloudMediaContext, objIAsset, objIContentKey);

            var keys = new Keys
                {
                    Key = contentKeyB64,
                    KeyId = keyId
                };

            return keys;
        }

        public static string RemoveDynamicPlayReadyProtection(CloudMediaContext objCloudMediaContext, IAsset objIAsset)
        {
            
            //objIAsset.DeliveryPolicies.Clear();
            //objIAsset.ContentKeys.Clear();
            //must remove the locator first by unpublishing
            var deliveryPolicyCount = objIAsset.DeliveryPolicies.Count;
            if (deliveryPolicyCount > 0)
            {
                for (int i = 0; i < deliveryPolicyCount; i++)
                {
                    objIAsset.DeliveryPolicies.Remove(objIAsset.DeliveryPolicies[0]);
                }
            }

            var contentKeyCount = objIAsset.ContentKeys.Count;
            string contentKey = string.Empty;
            if (contentKeyCount > 0)
            {
                contentKey = objIAsset.ContentKeys[0].Id;

                for (int i = 0; i < contentKeyCount; i++)
                {
                    objIAsset.ContentKeys.Remove(objIAsset.ContentKeys[0]);
                }
            }

            return contentKey;
            //objIAsset.Update();
        }

        //configure dynamic PlayReady protection of an IAsset using an IContentkey
        static public void CreateAssetDeliveryPolicy(CloudMediaContext objCloudMediaContext, IAsset objIAsset, IContentKey objIContentKey)
        {
            Uri acquisitionUrl = objIContentKey.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense);

            Dictionary<AssetDeliveryPolicyConfigurationKey, string> assetDeliveryPolicyConfiguration = new Dictionary<AssetDeliveryPolicyConfigurationKey, string>
            {
                {AssetDeliveryPolicyConfigurationKey.PlayReadyLicenseAcquisitionUrl, acquisitionUrl.ToString()},
            };

            var assetDeliveryPolicy = objCloudMediaContext.AssetDeliveryPolicies.Create(
                "AssetDeliveryPolicy",
                AssetDeliveryPolicyType.DynamicCommonEncryption,
                AssetDeliveryProtocol.SmoothStreaming | AssetDeliveryProtocol.Dash,
                assetDeliveryPolicyConfiguration);

            // Add AssetDelivery Policy to the asset
            objIAsset.DeliveryPolicies.Add(assetDeliveryPolicy);

            Console.WriteLine("Adding Asset Delivery Policy: " + assetDeliveryPolicy.AssetDeliveryProtocol);
        }

        public static byte[] GenerateCryptographicallyStrongRandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            //This type implements the IDisposable interface. When you have finished using the type, you should dispose of it either directly or indirectly. To dispose of the type directly, call its Dispose method in a try/catch block. To dispose of it indirectly, use a language construct such as using (in C#) 
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }

        //This API works the same as AESContentKey constructor in PlayReady Server SDK 
        public static string GetPlayReadyContentKeyFromKeyIdKeySeed(string keyIdString, string keySeedB64)
        {
            Guid keyId = new Guid(keyIdString);
            byte[] keySeed = Convert.FromBase64String(keySeedB64);

            byte[] contentKey = CommonEncryption.GeneratePlayReadyContentKey(keySeed, keyId);

            string contentKeyB64 = Convert.ToBase64String(contentKey);

            return contentKeyB64;
        }

        public static IContentKey ConfigureKeyDeliveryService(CloudMediaContext objCloudMediaContext, Guid keyId, string contentKeyB64)
        {
            //check if the keyId exists
            var keys = objCloudMediaContext.ContentKeys.Where(k => k.Id == "nb:kid:UUID:" + keyId.ToString());
            if (keys.Count() > 0)
            {
                Console.WriteLine("Key Delivery for Key ID = {0} exists.", string.Format("nb:kid:UUID:{0}", keyId.ToString()));
                return null;
            }

            byte[] keyValue = Convert.FromBase64String(contentKeyB64);

            var contentKey = objCloudMediaContext.ContentKeys.Create(keyId, keyValue, string.Format("KID_{0}", keyId.ToString()), ContentKeyType.CommonEncryption);

            var restrictions = new List<ContentKeyAuthorizationPolicyRestriction>
                {
                    //ContentKeyRestrictionType.Open
                    //new ContentKeyAuthorizationPolicyRestriction { Requirements = null, 
                    //                                               Name = "Open", 
                    //                                               KeyRestrictionType = (int) ContentKeyRestrictionType.Open
                    //                                             }
                    
                    //ContentKeyRestrictionType.IPRestricted,    sample asset: http://willzhanmediaservice.origin.mediaservices.windows.net/394ade06-2d0b-4d5d-9fcc-dca67f9116ae/Anna.ism/Manifest
                    //new ContentKeyAuthorizationPolicyRestriction { Requirements = string.Format("<Allowed addressType=\"IPv4\"><AddressRange start=\"{0}\" end=\"{0}\" /></Allowed>", "67.186.67.74"), 
                    //                                               Name = "IPRestricted",                                    
                    //                                               KeyRestrictionType = (int) ContentKeyRestrictionType.IPRestricted 
                    //                                             }   

                    //ContentKeyRestrictionType.TokenRestricted, sample asset: http://willzhanmediaservice.origin.mediaservices.windows.net/0bd8e2fd-e508-4eac-b5b8-f10d95cbe9de/BigBuckBunny.ism/manifest
                    new ContentKeyAuthorizationPolicyRestriction { Requirements = TokenRestrictionTemplateSerializer.Serialize(ContentKeyAuthorizationHelper.CreateRestrictionRequirementsJWT()),  //(ContentKeyAuthorizationHelper.AccessPolicyTemplateKeyClaim), 
                                                                   Name = "TokenRestricted", 
                                                                   KeyRestrictionType = (int) ContentKeyRestrictionType.TokenRestricted
                                                                 }
                };

            IContentKeyAuthorizationPolicy contentKeyAuthorizationPolicy = objCloudMediaContext.ContentKeyAuthorizationPolicies.CreateAsync("ContentKeyAuthorizationPolicy").Result;

            //Hard-coded string
            //string newLicenseTemplate = "<ContentProtectionSystemsRequest xmlns=\"http://schemas.microsoft.com/PlayReady/LicenseTemplate/v1\"><PlayReadyProtectionSystemRequest><AcquireLicenseRequest><MediaLicenseData LicenseType=\"Non-Persistent\"  BindingKeyType=\"Client\" MinimumSecurityLevel=\"150\"><ContentEncryptionKeyFromHeader /><PlayRight /></MediaLicenseData></AcquireLicenseRequest></PlayReadyProtectionSystemRequest></ContentProtectionSystemsRequest>";
            //Use License Template API
            string newLicenseTemplate = CreatePRLicenseResponseTemplate();

            /*
            string newLicenseTemplate = 
                @"<ContentProtectionSystemsRequest xmlns=\'http://schemas.microsoft.com/PlayReady/LicenseTemplate/v1\'>
                    <PlayReadyProtectionSystemRequest>
                        <AcquireLicenseRequest>
                            <MediaLicenseData 
                                 LicenseType=\'Non-Persistent\'  BindingKeyType=\'Client\' MinimumSecurityLevel=\'150\'>
                                <ContentEncryptionKeyFromHeader />
                                    <PlayRight />
                            </MediaLicenseData>
                        </AcquireLicenseRequest>
                    </PlayReadyProtectionSystemRequest>
                </ContentProtectionSystemsRequest>";
            */

            IContentKeyAuthorizationPolicyOption policyOption = objCloudMediaContext.ContentKeyAuthorizationPolicyOptions.Create("Dynamic PlayReady Protection With Token Restriction", ContentKeyDeliveryType.PlayReadyLicense, restrictions, newLicenseTemplate);

            contentKeyAuthorizationPolicy.Options.Add(policyOption);

            // Associate the content key authorization policy with the content key
            contentKey.AuthorizationPolicyId = contentKeyAuthorizationPolicy.Id;   //or contentKey.AuthorizationPolicy = policy;        
            contentKey = contentKey.UpdateAsync().Result;

            // Update the MediaEncryptor_PlayReadyProtection.xml file with the key and URL info.
            Uri keyDeliveryServiceUri = contentKey.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense);

            return contentKey;
        }

        static string CreatePRLicenseResponseTemplate()
        {
            PlayReadyLicenseResponseTemplate objPlayReadyLicenseResponseTemplate = new PlayReadyLicenseResponseTemplate();
            objPlayReadyLicenseResponseTemplate.ResponseCustomData = string.Format("WAMS-SecureKeyDelivery, Time = {0}", DateTime.UtcNow.ToString("MM-dd-yyyy HH:mm:ss"));
            PlayReadyLicenseTemplate objPlayReadyLicenseTemplate = new PlayReadyLicenseTemplate();
            objPlayReadyLicenseResponseTemplate.LicenseTemplates.Add(objPlayReadyLicenseTemplate);

            //objPlayReadyLicenseTemplate.BeginDate        = DateTime.Now.AddHours(-1).ToUniversalTime();
            //objPlayReadyLicenseTemplate.ExpirationDate   = DateTime.Now.AddHours(10.0).ToUniversalTime();
            objPlayReadyLicenseTemplate.LicenseType      = PlayReadyLicenseType.Nonpersistent;
            objPlayReadyLicenseTemplate.AllowTestDevices = true;  //MinmumSecurityLevel: 150 vs 2000

            //objPlayReadyLicenseTemplate.PlayRight.CompressedDigitalAudioOpl = 300;
            //objPlayReadyLicenseTemplate.PlayRight.CompressedDigitalVideoOpl = 400;
            //objPlayReadyLicenseTemplate.PlayRight.UncompressedDigitalAudioOpl = 250;
            //objPlayReadyLicenseTemplate.PlayRight.UncompressedDigitalVideoOpl = 270;
            //objPlayReadyLicenseTemplate.PlayRight.AnalogVideoOpl = 100;
            //objPlayReadyLicenseTemplate.PlayRight.AgcAndColorStripeRestriction = new AgcAndColorStripeRestriction(1);
            objPlayReadyLicenseTemplate.PlayRight.AllowPassingVideoContentToUnknownOutput = UnknownOutputPassingOption.Allowed;
            //objPlayReadyLicenseTemplate.PlayRight.ExplicitAnalogTelevisionOutputRestriction = new ExplicitAnalogTelevisionRestriction(0, true);
            //objPlayReadyLicenseTemplate.PlayRight.ImageConstraintForAnalogComponentVideoRestriction = true;
            //objPlayReadyLicenseTemplate.PlayRight.ImageConstraintForAnalogComputerMonitorRestriction = true;
            //objPlayReadyLicenseTemplate.PlayRight.ScmsRestriction = new ScmsRestriction(2);

            string serializedPRLicenseResponseTemplate = MediaServicesLicenseTemplateSerializer.Serialize(objPlayReadyLicenseResponseTemplate);

            //PlayReadyLicenseResponseTemplate responseTemplate2 = MediaServicesLicenseTemplateSerializer.Deserialize(serializedPRLicenseResponseTemplate);

            return serializedPRLicenseResponseTemplate;
        }
    }  //class

    // This class creates the actual tokens used to setup the PlayReady License server, 
    // and also to give tokens out for playback 
    public static class ContentKeyAuthorizationHelper
    {
        // Setup the restriction template
        public static TokenRestrictionTemplate CreateRestrictionRequirementsJWT()
        {
            string primarySymmetricKey = System.Configuration.ConfigurationManager.AppSettings["PrimarySymmetricKey"];
            string scope = System.Configuration.ConfigurationManager.AppSettings["Scope"];
            string issuer = System.Configuration.ConfigurationManager.AppSettings["Issuer"];

            byte[] symkeyhex = StringToByteArray(primarySymmetricKey);
            TokenRestrictionTemplate tokenTemplate = new TokenRestrictionTemplate(TokenType.JWT);
            tokenTemplate.Audience = scope;
            tokenTemplate.Issuer = issuer;
            tokenTemplate.PrimaryVerificationKey = new SymmetricVerificationKey(symkeyhex);
            // When you are adding the content key ID to the token, it limits the license validity to only that video
            tokenTemplate.RequiredClaims.Add(TokenClaim.ContentKeyIdentifierClaim);

            return tokenTemplate;
        }

        // The CastLabs system handles the strings as Hex. Therefore we need to be careful when using these 'strings' 
        // as in CastLabs system they are Hex Arrays. So convert them before use
        public static byte[] StringToByteArray(string hex) {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        } 

        // Generating the JWT token that works for both MS PlayReady License server, as the CastLabs Widevine License server
        public static string GeneratePlaybackToken(string assetId, string merchant, Guid contentKeyId)
        {
            // use the token template as a base
            var tokenTemplate = CreateRestrictionRequirementsJWT();

            var expiryDate = DateTime.Now.AddDays(365); // change this for shorter validity
            var currentDate = DateTime.Now;
            var expiryInEpoch = GetEpochTime(expiryDate);
            var currentDateInEpoc = GetEpochTime(currentDate);
            
            var claims = new List<Claim>()
            {
                // Add the content key ID (this is a required claim, because we said so in the token template)
                new Claim(TokenClaim.ContentKeyIdentifierClaimType, contentKeyId.ToString()),
                // A JSON string containing information about the merchant (you). Refer to CastLabs documentation for more info.
                new Claim("optData", string.Format("{{\"userId\":\"testuser\", \"sessionId\":\"testsession\",\"merchant\":\"{0}\"}}", merchant)),
                // A JSON string containing information about the asset, with license info and playback rights. Refer to CastLabs documentation for more info.
                new Claim("crt", string.Format("[{{\"accountingId\":\"1\", \"assetId\" : \"{0}\",\"profile\" : {{\"rental\" : {{\"absoluteExpiration\" : \"2015-12-12T00:00:00Z\", \"playDuration\" : 360000}}}},\"storeLicense\" : false}}]", assetId)),
                // The current datetime in epoch. Must be as an integer.
                new Claim("iat", currentDateInEpoc, ClaimValueTypes.Integer64),
                // A unique identifier about this token (every token can only used once in the CastLabs system)
                new Claim("jti", Convert.ToBase64String(Guid.NewGuid().ToByteArray()))
            };

            // Create the token signing key
            InMemorySymmetricSecurityKey tokenSigningKey = new InMemorySymmetricSecurityKey((tokenTemplate.PrimaryVerificationKey as SymmetricVerificationKey).KeyValue);
            // Create the credentials based on the key and a SHA256 algorythm
            SigningCredentials cred = new SigningCredentials(tokenSigningKey, SecurityAlgorithms.HmacSha256Signature, SecurityAlgorithms.Sha256Digest);
            // Create the actual token
            JwtSecurityToken token = new JwtSecurityToken(
                issuer: tokenTemplate.Issuer,
                audience: tokenTemplate.Audience,
                claims: claims,
                notBefore: DateTime.Now.AddMinutes(-5),
                expires: expiryDate,
                signingCredentials: cred);
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();

            // For playing back the video with the PlayReady license, add the "Bearer=" prefix.
            // For playing back the video with the Widevine license, omit the "Bearer=" prefix!
            string jwt = "Bearer=" + handler.WriteToken(token);
            
            return jwt;
        }

        private static readonly DateTime TokenBaseTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        private static string GetEpochTime(DateTime expiry)
        {
            long totalSeconds = (long)((expiry.ToUniversalTime() - TokenBaseTime).TotalSeconds);

            return Convert.ToString(totalSeconds);
        }
    } //class

}  //namespace

