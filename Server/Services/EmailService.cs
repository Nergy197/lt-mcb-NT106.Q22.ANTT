using System.Net;
using System.Net.Mail;

namespace PokemonMMO.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration config, ILogger<EmailService> log)
    {
        _config = config;
        _log = log;
    }

    public async Task SendResetTokenAsync(string toEmail, string resetToken)
    {
        try
        {
            var host = _config["Smtp:Host"] ?? "smtp.gmail.com";
            var port = int.Parse(_config["Smtp:Port"] ?? "587");
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];

            // Kiểm tra cấu hình có được điền hay không
            if (string.IsNullOrEmpty(username) || username.Contains("your_email_here"))
            {
                _log.LogWarning("Chưa cấu hình tài khoản SMTP Email trong appsettings.json. Hệ thống bỏ qua gửi mail thực tế.");
                return;
            }

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(username, "Pokemon MMO System"),
                Subject = "Mã Khôi Phục Mật Khẩu (Reset Token)",
                Body = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7f6; padding: 20px; color: #333;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.05);"">
        <!-- Header -->
        <div style=""background: linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%); padding: 30px 20px; text-align: center;"">
            <h1 style=""color: #ffffff; margin: 0; font-size: 24px; font-weight: 600; letter-spacing: 1px;"">Pokémon MMO</h1>
        </div>
        
        <!-- Content -->
        <div style=""padding: 40px 30px;"">
            <h2 style=""color: #2b2d42; font-size: 22px; margin-top: 0; margin-bottom: 20px;"">Khôi phục mật khẩu</h2>
            <p style=""font-size: 16px; line-height: 1.6; color: #555; margin-bottom: 25px;"">
                Xin chào huấn luyện viên,<br><br>
                Chúng tôi vừa nhận được yêu cầu khôi phục mật khẩu cho tài khoản liên kết với địa chỉ email này. Dưới đây là mã bí mật của bạn:
            </p>
            
            <!-- Code Box -->
            <div style=""background-color: #f8f9fa; border: 2px dashed #FF4B2B; border-radius: 8px; padding: 20px; text-align: center; margin-bottom: 30px;"">
                <span style=""font-family: monospace; font-size: 32px; font-weight: bold; letter-spacing: 2px; color: #FF4B2B;"">{resetToken}</span>
            </div>
            
            <p style=""font-size: 15px; line-height: 1.6; color: #666; margin-bottom: 10px;"">
                Nhập mã token này vào ứng dụng để đặt lại mật khẩu mới. <strong>Mã chỉ có hiệu lực trong vòng 1 giờ.</strong>
            </p>
            <p style=""font-size: 14px; line-height: 1.6; color: #999; margin-bottom: 0;"">
                Nếu bạn không yêu cầu điều này, xin vui lòng bỏ qua email. Tài khoản của bạn vẫn an toàn.
            </p>
        </div>
        
        <!-- Footer -->
        <div style=""background-color: #f8f9fa; padding: 20px; text-align: center; border-top: 1px solid #eeeeee;"">
            <p style=""font-size: 12px; color: #888; margin: 0;"">
                © {DateTime.Now.Year} Pokémon MMO Server. All rights reserved.
            </p>
        </div>
    </div>
</div>",
                IsBodyHtml = true
            };
            
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _log.LogInformation("[EmailService] Gửi email thành công tới {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[EmailService] Lỗi khi gửi email tới {Email}", toEmail);
        }
    }
}
