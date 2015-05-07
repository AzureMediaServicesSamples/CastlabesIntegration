using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace STS.Controllers
{
    public class TokenController : ApiController
    {
        private static string _merchant = ConfigurationManager.AppSettings["CastLabs.Merchant"];

        [HttpGet]
        public HttpResponseMessage Get(string assetId, string contentKeyId)
        {
            string token = PlayReadyDynamicEncryption.ContentKeyAuthorizationHelper.GeneratePlaybackToken(assetId, _merchant, Guid.Parse(contentKeyId));
            
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new StringContent(token, Encoding.UTF8, "text/plain");
            return resp;
        }
    }
}
