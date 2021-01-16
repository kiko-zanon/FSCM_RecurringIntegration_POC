using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace FO_RI_ExchangeRates
{
    class HttpClientHelper
    {

        /// <summary>
        /// get AuthenticationHeaderValue based on client/secret
        /// </summary>
        /// <returns></returns>
        public static AuthenticationHeaderValue GetAuthHeaderValue()
        {
            UriBuilder uri = new UriBuilder(ConfigurationManager.AppSettings["AzureAuthEndpoint"]);
            uri.Path = ConfigurationManager.AppSettings["AadTenant"];

            AuthenticationContext authenticationContext = new AuthenticationContext(uri.ToString());
            var credential = new ClientCredential(ConfigurationManager.AppSettings["AzureAppID"], ConfigurationManager.AppSettings["AzureClientsecret"]);
            string uriFO = ConfigurationManager.AppSettings["FOUri"];
            AuthenticationResult authenticationResult = authenticationContext.AcquireTokenAsync(ConfigurationManager.AppSettings["FOUri"], credential).Result;

            string a = authenticationResult.CreateAuthorizationHeader();

            string[] split = a.Split(' ');
            string scheme = split[0];
            string parameter = split[1];
            AuthenticationHeaderValue ahv = new AuthenticationHeaderValue(scheme, parameter);
            return ahv;
        }
        /// <summary>
        /// Post request stream
        /// </summary>
        /// <param name="uri">Enqueue endpoint URI</param>
        /// <param name="authenticationHeader">Authentication header</param>
        /// <param name="bodyStream">Body stream</param>        
        /// <param name="message">ActivityMessage context</param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendPostRequestAsyncStream(Uri uri, Stream bodyStream, string externalCorrelationHeaderValue = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls12;

            using (HttpClientHandler handler = new HttpClientHandler() { UseCookies = false })
            {
                using (HttpClient httpClient = new HttpClient(handler))
                {
                    httpClient.DefaultRequestHeaders.Authorization = GetAuthHeaderValue();
                    // Add external correlation id header id specified and valid
                    if (!string.IsNullOrEmpty(externalCorrelationHeaderValue))
                        httpClient.DefaultRequestHeaders.Add("x-ms-dyn-externalidentifier", externalCorrelationHeaderValue);

                    if (bodyStream != null)
                    {
                        using (StreamContent content = new StreamContent(bodyStream))
                        {
                            return await httpClient.PostAsync(uri, content);
                        }
                    }
                }
            }
            return new HttpResponseMessage()
            {
                Content = new StringContent("Request failed at client.", Encoding.ASCII),
                StatusCode = System.Net.HttpStatusCode.PreconditionFailed
            };
        }
        /// <summary>
        /// Http Get requests for use with JobStatus API
        /// </summary>
        /// <param name="uri">Request URI</param>
        /// <returns>Task of type HttpResponseMessage</returns>
        public async Task<HttpResponseMessage> GetRequestAsync(Uri uri)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls12;

            HttpResponseMessage responseMessage;

            using (HttpClientHandler handler = new HttpClientHandler() { UseCookies = false })
            {
                using (HttpClient httpClient = new HttpClient(handler))
                {
                    httpClient.DefaultRequestHeaders.Authorization = GetAuthHeaderValue();

                    responseMessage = await httpClient.GetAsync(uri).ConfigureAwait(false);
                }
            }
            return responseMessage;
        }
        /// <summary>
        /// Get the Enqueue URI
        /// </summary>
        /// <returns>Enqueue URI</returns>
        public Uri GetEnqueueUri()
        {
            //access the Connector API
            UriBuilder enqueueUri = new UriBuilder(ConfigurationManager.AppSettings["FOUri"]);
            enqueueUri.Path = "api/connector/enqueue/" + ConfigurationManager.AppSettings["RecurringJobId"];
            // Individual file
            string enqueueQuery = "entity=" + ConfigurationManager.AppSettings["EntityName"];
            // Append company if specified
            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["Company"]))
            {
                enqueueQuery += "&company=" + ConfigurationManager.AppSettings["Company"];
            }
            enqueueUri.Query = enqueueQuery;
            return enqueueUri.Uri;
        }
    }
}
