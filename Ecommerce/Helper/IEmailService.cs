namespace EcommerceWebApplication.Helper
{
    public interface IEmailService
    {
        void Send(string toEmail, string subject, string body);
        void SendBulkEmails(List<string> toEmails, string subject, string body);
        void Sendinvoice(string toEmail, string subject, string body, byte[] attachmentBytes = null, string attachmentFileName = null);
    }

}
