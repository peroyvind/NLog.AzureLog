﻿using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using NLog.Config;
using NLog.Targets;
using NLog.Common;

namespace NLog.AzureLog
{
    [Target("Azure")]
    public sealed class Azure : TargetWithLayout
    {
        private static HttpClient _client;

        public Azure()
        {
            if(_client == null)
                _client = new System.Net.Http.HttpClient();
        }

        private static TaskQueue _taskQueue = new TaskQueue(2, 10000);
        [RequiredParameter]
        public string CustomerId { get; set; }
        public string SharedKey { get; set; }
        public string LogName { get; set; }

        protected override void Write(LogEventInfo logEvent)
        {
            string logMessage = this.Layout.Render(logEvent);

            // Create a hash for the API signature
            var datestring = DateTime.UtcNow.ToString("r");
            var jsonBytes = Encoding.UTF8.GetBytes(logMessage);
            string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            string hashedString = BuildSignature(stringToHash, this.SharedKey);
            string signature = "SharedKey " + this.CustomerId + ":" + hashedString;


            if (_taskQueue.Queue(() => PostData(signature, datestring, logMessage)))
            {
                _taskQueue.ProcessBackground();
            }

        }

        public string BuildSignature(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }
        public async Task PostData(string signature, string date, string json)
        {
            try
            {
                string url = "https://" + CustomerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";
                
                _client.DefaultRequestHeaders.Clear();
                _client.DefaultRequestHeaders.Add("Accept", "application/json");
                _client.DefaultRequestHeaders.Add("Log-Type", LogName);
                _client.DefaultRequestHeaders.Add("Authorization", signature);
                _client.DefaultRequestHeaders.Add("x-ms-date", date);
                _client.DefaultRequestHeaders.Add("time-generated-field", "");

                System.Net.Http.HttpContent httpContent = new StringContent(json, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                System.Net.Http.HttpResponseMessage response = await _client.PostAsync(new Uri(url), httpContent);

                //System.Net.Http.HttpContent responseContent = response.Content;
                //string result = responseContent.ReadAsStringAsync().Result;
                if(response != null && response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    InternalLogger.Error("API Post Failed status: " + response.StatusCode.ToString());
                }
            }
            catch (Exception excep)
            {
                InternalLogger.Error("API Post Exception: " + excep.Message);
            }
        }
    }
}
