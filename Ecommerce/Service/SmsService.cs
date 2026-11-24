// SmsService.cs
using EcommerceWebApplication.Service;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

public class SmsService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _senderId;
    private readonly string _baseUrl; 

    public SmsService(HttpClient httpClient,IConfiguration config)
    {
  
        _httpClient = httpClient;
        _baseUrl = config["SmsSettings:BaseUrl"];
        _apiKey = config["SmsSettings:ApiKey"];
        _senderId = config["SmsSettings:SenderId"];
    }

    public async Task<string> SendSmsAsync(string mobile, string message)
    { // Get server IP
        var serverIp = GetServerIp();
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["apikey"] = _apiKey;
        query["language"] = "1"; // Hard-coded per your query
        query["sender"] = _senderId;
        query["mobile"] = mobile;
        query["message"] = message;
        var url = $"{_baseUrl}?{query}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
        //var query = HttpUtility.ParseQueryString(string.Empty);
        //query["apikey"] = _apiKey;
        //query["language"] = "1";
        //query["sender"] = _senderId;
        //query["mobile"] = mobile;
        //query["message"] = HttpUtility.UrlEncode(message);

        //var url = $"{_baseUrl}?{query}";
        //var response = await _httpClient.GetAsync(url);
        //var content = await response.Content.ReadAsStringAsync();
        //return $"StatusCode: {response.StatusCode}, Response: {content}";
    }
    public async Task<string> BalanceAsync()
    { 
        var  _baseUrl1 = "https://api-server14.com/api/balance.aspx";
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["apikey"] = _apiKey;

        var url = $"{_baseUrl1}?{query}";
        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        return $"StatusCode: {response.StatusCode}, Response: {content}";
    }
    public async Task<string> SendSms(string mobile, string message)
    {
        // Get server IP
        var serverIp = GetServerIp();

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["apikey"] = _apiKey;
        query["language"] = "1";
        query["sender"] = _senderId;
        query["mobile"] = mobile;
        query["message"] = HttpUtility.UrlEncode(message);
        // Add server IP as an extra param, e.g. "senderip" or "ip"
        query["senderip"] = serverIp;

        var url = $"{_baseUrl}?{query}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private string GetServerIp()
    {
        string hostName = Dns.GetHostName();
        var ipAddresses = Dns.GetHostEntry(hostName).AddressList;

        // Pick first IPv4
        var serverIp = ipAddresses
            .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?
            .ToString();

        return serverIp ?? "IP not found";
    }

}
