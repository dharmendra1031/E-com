using MF.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MF.Service
{
    public class MFClient : IMFClient
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClient;

        private string _apiKey;
        private string _apiUrl;
        public MFClient(IConfiguration config , IHttpClientFactory httpClient)
        {
            _config = config;
            _apiKey = _config[$"ApiKey"];
            _apiUrl = _config[$"ApiUrl"];
            _httpClient = httpClient;
        }
        public async Task<string> PerformRequest(string requestJSON, string url = "", string endPoint = "")
        {
            if (string.IsNullOrEmpty(url))
                url = _apiUrl + $"/v2/{endPoint}";
            var httpContent = new StringContent(requestJSON, System.Text.Encoding.UTF8, "application/json");
            var client = _httpClient.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            var responseMessage = await client.PostAsync(url, httpContent).ConfigureAwait(false);
            string response = string.Empty;
            if (!responseMessage.IsSuccessStatusCode)
            {
                response = JsonConvert.SerializeObject(new  
                {
                    IsSuccess = false,
                    Message = responseMessage.StatusCode.ToString()
                });
            }
            else
            {
                response = await responseMessage.Content.ReadAsStringAsync();
            }

            return response;
        }
        public async Task<string> PerformRequest(string requestUrl)
        {
            string url = _apiUrl + requestUrl;
            var client = _httpClient.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            var responseMessage = await client.PostAsync(url, null).ConfigureAwait(false);
            string response = string.Empty;
            if (!responseMessage.IsSuccessStatusCode)
            {
                response = JsonConvert.SerializeObject(new
                {
                    IsSuccess = false,
                    Message = responseMessage.StatusCode.ToString()
                });
            }
            else
            {
                response = await responseMessage.Content.ReadAsStringAsync();
            }

            return response;
        }

    }
}
