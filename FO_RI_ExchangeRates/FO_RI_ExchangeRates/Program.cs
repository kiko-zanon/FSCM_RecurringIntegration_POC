using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dynamics.AX.Framework.Tools.DataManagement.Serialization;

namespace FO_RI_ExchangeRates
{
    class Program
    {
        #region helper methods

        /// <summary>
        /// Get submitted job status using the Enqueue response -
        /// MessageId
        /// </summary>
        /// <param name="messageId">Correlation identifier for the submitted job returned as the Enqueue response</param>
        /// <returns>DataJobStatusDetail object that includes detailed job status</returns>
        public static DataJobStatusDetail getStatus(string messageId)
        {
            DataJobStatusDetail jobStatusDetail = null;

            /// get status
            UriBuilder statusUri = new UriBuilder(ConfigurationManager.AppSettings["FOUri"]);
            statusUri.Path = "api/connector/jobstatus/" + ConfigurationManager.AppSettings["RecurringJobId"];
            statusUri.Query = "jobId=" + messageId.Replace(@"""", "");

            //send a request to get the message status
            HttpClientHelper clientHelper = new HttpClientHelper();

            var response = clientHelper.GetRequestAsync(statusUri.Uri).Result;
            if (response.IsSuccessStatusCode)
            {
                // Deserialize response to the DataJobStatusDetail object
                jobStatusDetail = JsonConvert.DeserializeObject<DataJobStatusDetail>(response.Content.ReadAsStringAsync().Result);
            }
            else
            {
                Console.WriteLine("Status call failed. Status code: " + response.StatusCode + " , Reason: " + response.ReasonPhrase);
            }

            return jobStatusDetail;
        }

        /// <summary>
        /// enqueue request to import
        /// </summary>
        /// <param name="file">filepath to .csv file to be sent to API</param>
        /// <returns>HttpResponseMessage response of request to enqueue</returns>
        public static HttpResponseMessage enqueueImport(string file, string correlationId)
        {
            //get stream from file
            FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            Stream sourceStream = fileStream;
            sourceStream.Seek(0, SeekOrigin.Begin);

            //instantiate http client helper
            var httpClientHelper = new HttpClientHelper();

            Uri enqueueUri = httpClientHelper.GetEnqueueUri();

            // Post Enqueue request
            var response = httpClientHelper.SendPostRequestAsyncStream(enqueueUri, sourceStream, correlationId).Result;
            return response;
        }

        #endregion
        static void Main(string[] args)
        {
            try
            {
                #region Currencies
                string filePathtoEnqueue = @"C:\temp\Input\RI_CurEx_Export-Exchange rates.csv";
                string correlationId = string.Format("{0}_{1}", Path.GetFileName(filePathtoEnqueue), Guid.NewGuid().ToString());

                #region add initial info
                Console.WriteLine("********************************************************************************");
                Console.WriteLine("               RECURRING INTEGRATION POC EXCHANGE RATES                      ");
                Console.WriteLine("********************************************************************************");
                Console.WriteLine("");

                Console.WriteLine("1. Call REST API api/connector/enqueue/ With .csv as memoryStream");
                Console.WriteLine("********************************************************************************");
                Console.WriteLine("");
                #endregion
                // Post Enqueue request
                var response = enqueueImport(filePathtoEnqueue, correlationId);
                //check response
                if (response.IsSuccessStatusCode)
                {
                    // Log success and add to Enqueued jobs for further processing
                    var messageId = response.Content.ReadAsStringAsync().Result;
                    // Log enqueue success                    
                    Console.WriteLine("File:  " + filePathtoEnqueue + " - enqueued successfully.");
                    Console.WriteLine("Message identifier: " + messageId);
                    Console.WriteLine("");

                    #region check status by message ID
                    Console.WriteLine("2. Call REST API api/connector/jobstatus with Message id until final status");
                    Console.WriteLine("********************************************************************************");
                    Console.WriteLine("");
                    string status = "";
                    DataJobStatusDetail statusret = null;
                    while (status != "Processed" && status != "PreProcessingError" && status != "ProcessedWithErrors" && status != "PostProcessingFailed")
                    {
                        //check status
                        Console.WriteLine("  >> Waiting 20 seconds to retrieve status");
                        Thread.Sleep(30000);
                        Console.WriteLine("  >> Check for status");
                        statusret = getStatus(messageId);
                        status = statusret.DataJobStatus.DataJobState.ToString();
                        Console.WriteLine(string.Format("  >> Current status: {0}", status));

                    }
                    #endregion

                    #region show execution results
                    Console.WriteLine("");
                    Console.WriteLine("3. Show Execution results");
                    Console.WriteLine("********************************************************************************");
                    Console.WriteLine("");
                    //show execution results
                    Console.WriteLine(statusret.ExecutionLog);
                    if (statusret.ExecutionDetail != null)
                    {
                        Console.WriteLine("");
                        foreach (EntityExecutionStatus ees in statusret.ExecutionDetail)
                        {
                            Console.WriteLine(string.Format("Company         : {0}", ees.Company));
                            Console.WriteLine(string.Format("Entity Name     : {0}", ees.EntityName));
                            Console.WriteLine(string.Format("Total Records   : {0}", ees.TotalRecords));
                            Console.WriteLine(string.Format("Exec. start     : {0}", ees.ExecutionStartedDateTime));
                            Console.WriteLine(string.Format("Exec. completed : {0}", ees.ExecutionCompletedDateTime));
                            Console.WriteLine("");
                            Console.WriteLine(string.Format("Staging status  : {0}", ees.StagingStatus));
                            Console.WriteLine(string.Format("Staging records : {0}", ees.StagingRecords));
                            Console.WriteLine(string.Format("Staging errors  : {0}", ees.StagingErrorCount));
                            Console.WriteLine("");
                            Console.WriteLine(string.Format("Target status   : {0}", ees.TargetStatus));
                            Console.WriteLine(string.Format("Target records  : {0}", ees.TargetRecords));
                            Console.WriteLine(string.Format("Target errors   : {0}", ees.TargetErrorCount));
                            Console.WriteLine("");
                        }
                    }
                    Console.WriteLine("********************************************************************************");
                    Console.WriteLine("");
                    #endregion                    

                }
                else
                {
                    Console.WriteLine("Enqueue failed for file:  " + filePathtoEnqueue);
                    Console.WriteLine("Failure response:  Status: " + response.StatusCode + ", Reason: " + response.ReasonPhrase);
                }
                #endregion

            }
            catch (Exception ex)
            {
                Console.WriteLine("Enqueue failed :  " + ex.Message);
            }


            Console.WriteLine("");
            Console.WriteLine("Completed!, press a key to close");
            Console.ReadKey();
        }
    }
}
