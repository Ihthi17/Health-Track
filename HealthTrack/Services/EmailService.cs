using System.Net;
using System.Net.Mail;
using HealthTrack.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    public interface IEmailService
    {
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Send password reset email
        /// </summary>
        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
        {
            try
            {
                var subject = "HealthTrack - Password Reset Request";
                var htmlBody = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9; }}
                            .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                            .content {{ background-color: white; padding: 20px; border-radius: 0 0 5px 5px; }}
                            .button {{ display: inline-block; background-color: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                            .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                            .warning {{ color: #ff6b6b; font-weight: bold; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h2>HealthTrack Password Reset</h2>
                            </div>
                            <div class='content'>
                                <p>Hello {toName},</p>
                                <p>We received a request to reset your password for your HealthTrack account. If you did not make this request, you can safely ignore this email.</p>
                                
                                <p>To reset your password, click the button below or copy the link:</p>
                                
                                <center>
                                    <a href='{resetLink}' class='button'>Reset Password</a>
                                </center>
                                
                                <p>Or copy this link into your browser:</p>
                                <p style='word-break: break-all; background-color: #f5f5f5; padding: 10px; border-radius: 3px;'>{resetLink}</p>
                                
                                <p><span class='warning'>⚠️ Important:</span> This link will expire in 24 hours.</p>
                                
                                <p>If you did not request a password reset, please ignore this email. Your account is secure.</p>
                                
                                <hr>
                                <p>Best regards,<br><strong>HealthTrack Support Team</strong></p>
                            </div>
                            <div class='footer'>
                                <p>&copy; 2026 HealthTrack. All rights reserved.</p>
                                <p>This is an automated email. Please do not reply to this message.</p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                return await SendEmailAsync(toEmail, subject, htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending password reset email to {toEmail}");
                return false;
            }
        }

        /// <summary>
        /// Send generic email
        /// </summary>
        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                // Get email settings from database
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<HealthTrackContext>();
                    
                    var emailSettings = await context.EmailSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync();

                    if (emailSettings == null)
                    {
                        _logger.LogWarning("Email settings not found in database. Email not sent.");
                        return false;
                    }

                    // Validate settings
                    if (string.IsNullOrEmpty(emailSettings.SMTPServer) || 
                        string.IsNullOrEmpty(emailSettings.FromEmail) || 
                        string.IsNullOrEmpty(emailSettings.SMTPUsername) || 
                        string.IsNullOrEmpty(emailSettings.SMTPPassword))
                    {
                        _logger.LogWarning("Email settings incomplete in database. Email not sent.");
                        return false;
                    }

                    // Create SMTP client
                    using (var client = new SmtpClient(emailSettings.SMTPServer, emailSettings.SMTPPort))
                    {
                        client.EnableSsl = emailSettings.EnableSSL;
                        client.Credentials = new NetworkCredential(emailSettings.SMTPUsername, emailSettings.SMTPPassword);
                        client.Timeout = 10000; // 10 seconds

                        // Create email message
                        using (var mail = new MailMessage())
                        {
                            mail.From = new MailAddress(emailSettings.FromEmail, emailSettings.FromName ?? "HealthTrack");
                            mail.To.Add(toEmail);
                            mail.Subject = subject;
                            mail.Body = htmlBody;
                            mail.IsBodyHtml = true;

                            // Send email
                            await client.SendMailAsync(mail);

                            _logger.LogInformation($"Email sent successfully to {toEmail}");
                            return true;
                        }
                    }
                }
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, $"SMTP error sending email to {toEmail}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email to {toEmail}: {ex.Message}");
                return false;
            }
        }
    }
}
