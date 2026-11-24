namespace EcommerceWebApplication.Service
{
    public interface ISmsService
    {
        Task<string> SendSmsAsync(string mobile, string message);
        Task<string> SendSms(string mobile, string message);
        Task<string> BalanceAsync();
    }
}
