using System.Net;
using System.Net.Mail;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class ContactForm
{
    private readonly ILogger _logger;

    public ContactForm(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ContactForm>();
    }

  [Function("ContactForm")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
{
    _logger.LogInformation("Processing contact form submission.");

    // 1. Read the raw body string
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

    // 2. Parse the x-www-form-urlencoded string into a dictionary
    // This handles the "name=Wouter&email=test@test.com" format
    var formData = requestBody.Split('&')
        .Select(x => x.Split('='))
        .ToDictionary(
            x => Uri.UnescapeDataString(x[0]), 
            x => x.Length > 1 ? Uri.UnescapeDataString(x[1].Replace("+", " ")) : ""
        );

    string name = formData.GetValueOrDefault("name", "Anonymous");
    string email = formData.GetValueOrDefault("email", "No Email");
    string subject = formData.GetValueOrDefault("subject", "No Subject");
    string message = formData.GetValueOrDefault("message", "");

    // 3. Pull settings from Environment
    string smtpServer = Environment.GetEnvironmentVariable("SmtpServer") ?? "";
    int smtpPort = int.Parse(Environment.GetEnvironmentVariable("SmtpPort") ?? "587");
    string senderEmail = Environment.GetEnvironmentVariable("SenderEmail") ?? "";
    string senderPassword = Environment.GetEnvironmentVariable("SenderPassword") ?? "";
    string recipientEmail = Environment.GetEnvironmentVariable("RecipientEmail") ?? "";

    // 4. Send the Email
    bool success = await SendContactEmailAsync(name, email, subject, message, 
                                             smtpServer, smtpPort, senderEmail, 
                                             senderPassword, recipientEmail);

    // 5. Return Response
    var response = req.CreateResponse(success ? HttpStatusCode.Redirect : HttpStatusCode.InternalServerError);
    
    response.Headers.Add("Content-Type", "application/json");
    await response.WriteAsJsonAsync(new { success = success, message = success ? "Email sent successfully!" : "Failed to send email." });
    return response;
}

    private async Task<bool> SendContactEmailAsync(string visitorName, string visitorEmail, string subject, string message, 
        string server, int port, string user, string pass, string receiver)
    {
        try
        {
            using var client = new SmtpClient(server, port);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(user, pass);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(user),
                Subject = $"Portfolio Contact: {subject}",
                Body = $"Name: {visitorName}\nEmail: {visitorEmail}\n\nMessage:\n{message}",
                IsBodyHtml = false
            };
            mailMessage.To.Add(receiver);

            await client.SendMailAsync(mailMessage);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"SMTP Error: {ex.Message}");
            return false;
        }
    }
}