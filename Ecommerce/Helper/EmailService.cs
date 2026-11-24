using System.Net;
using System.Net.Mail;

namespace EcommerceWebApplication.Helper
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public void Send(string toEmail, string subject, string body)
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

                ServicePointManager.ServerCertificateValidationCallback =
            (sender, cert, chain, sslPolicyErrors) => true;

                using (var smtpClient = new SmtpClient(_config["Smtp:Host"]))
                {
                    smtpClient.Port = int.Parse(_config["Smtp:Port"]);
                    smtpClient.EnableSsl = true;
                    smtpClient.UseDefaultCredentials = false;
                    smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtpClient.Timeout = 30000;

                    smtpClient.Credentials = new NetworkCredential(
                        _config["Smtp:Username"],
                        _config["Smtp:Password"]
                    );

                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress(
                            _config["Smtp:FromEmail"],
                            _config["Smtp:FromName"]
                        );

                        mailMessage.To.Add(toEmail);
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = true;

                        smtpClient.Send(mailMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("SMTP send failed: " + ex.ToString(), ex);
            }
        }

        public void Send12(string toEmail, string subject, string body)
        {
            try
            {
                using (var smtpClient = new SmtpClient(_config["Smtp:Host"]))
                {
                    smtpClient.Port = int.Parse(_config["Smtp:Port"]);
                    smtpClient.EnableSsl = bool.Parse(_config["Smtp:EnableSsl"] ?? "true");
                    smtpClient.UseDefaultCredentials = false;
                    smtpClient.Credentials = new NetworkCredential(
                        _config["Smtp:Username"],
                        _config["Smtp:Password"]
                    );

                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress(
                            _config["Smtp:FromEmail"],
                            _config["Smtp:FromName"]
                        );

                        mailMessage.To.Add(new MailAddress(toEmail));
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = true;

                        smtpClient.Send(mailMessage);
                    }
                }
            }
            catch (SmtpException ex)
            {
                var error = ex.InnerException?.Message ?? ex.Message;
                throw new Exception("SMTP send failed: " + error, ex);
            }
        }

        public void Send23(string toEmail, string subject, string body)
        {
            try
            {
                using (var smtpClient = new SmtpClient(_config["Smtp:Host"]))
                {
                    smtpClient.Port = int.Parse(_config["Smtp:Port"]);
                    smtpClient.EnableSsl = bool.Parse(_config["Smtp:EnableSsl"] ?? "true");
                    smtpClient.UseDefaultCredentials = false;
                    smtpClient.Credentials = new NetworkCredential(
                        _config["Smtp:Username"],
                        _config["Smtp:Password"]
                    );

                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress(
    _config["Smtp:FromEmail"],
    _config["Smtp:FromName"]
);
                        mailMessage.To.Add(toEmail);
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = true;

                        smtpClient.Send(mailMessage);
                    }
                }
            }
            catch (SmtpException ex)
            {
                // THIS shows DNS / firewall / auth issues clearly
                var error = ex.InnerException?.Message ?? ex.Message;
                throw new Exception("SMTP send failed: " + error, ex);
            }
        }


        public void Send1(string toEmail, string subject, string body)
        {
            var smtpClient = new SmtpClient(_config["Smtp:Host"])
            {
                Port = int.Parse(_config["Smtp:Port"]),
                Credentials = new NetworkCredential(_config["Smtp:Username"], _config["Smtp:Password"]),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_config["Smtp:From"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(toEmail);

            smtpClient.Send(mailMessage);
        }

        public void SendBulkEmails(List<string> toEmails, string subject, string body)
        {
            var smtpClient = new SmtpClient(_config["Smtp:Host"])
            {
                Port = int.Parse(_config["Smtp:Port"]),
                EnableSsl = bool.Parse(_config["Smtp:EnableSsl"] ?? "true"),
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _config["Smtp:Username"],
                    _config["Smtp:Password"])
            };

            int batchSize = 10;  // Adjust according to your SMTP limit

            for (int i = 0; i < toEmails.Count; i += batchSize)
            {
                var batchEmails = toEmails.Skip(i).Take(batchSize).ToList();

                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(_config["Smtp:From"]);
                    mailMessage.Subject = subject;
                    mailMessage.Body = body;
                    mailMessage.IsBodyHtml = true;

                    foreach (var email in batchEmails)
                    {
                        mailMessage.Bcc.Add(email.Trim());
                    }

                    try
                    {
                        smtpClient.Send(mailMessage);
                    }
                    catch (SmtpException ex)
                    {
                        // Log error and rethrow to be caught by controller
                        Console.WriteLine($"SMTP Error: {ex.Message}");
                        throw;   // This will send the error back to your controller
                    }
                }

                Thread.Sleep(3000);  // Optional delay to avoid SMTP limits
            }
        }
        public void Sendinvoice(string toEmail, string subject, string body, byte[] attachmentBytes, string attachmentFileName)
        {
            var smtpClient = new SmtpClient(_config["Smtp:Host"])
            {
                Port = int.Parse(_config["Smtp:Port"]),
                EnableSsl = bool.Parse(_config["Smtp:EnableSsl"] ?? "true"),
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _config["Smtp:Username"],
                    _config["Smtp:Password"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_config["Smtp:From"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

          
            if (attachmentBytes != null && !string.IsNullOrEmpty(attachmentFileName))
            {
                using (var ms = new MemoryStream(attachmentBytes))
                {
                    ms.Position = 0;
                    var attachment = new Attachment(ms, attachmentFileName, "application/pdf");
                    mailMessage.Attachments.Add(attachment);

                    try
                    {
                        smtpClient.Send(mailMessage);
                    }
                    catch (SmtpException ex)
                    {
                        throw new Exception("SMTP send failed: " + ex.Message, ex);
                    }
                }
            }
            else
            {
                try
                {
                    smtpClient.Send(mailMessage);
                }
                catch (SmtpException ex)
                {
                    throw new Exception("SMTP send failed: " + ex.Message, ex);
                }
            }
        }
    }
}
