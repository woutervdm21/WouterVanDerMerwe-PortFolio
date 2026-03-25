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

        // 1. Read the Form Data (sent from your HTML form)
        var content = await req.ReadAsFormDataAsync();
        string name = content["name"] ?? "Anonymous";
        string email = content["email"] ?? "No Email";
        string subject = content["subject"] ?? "No Subject";
        string message = content["message"] ?? "";

        // 2. Pull settings from Environment (local.settings.json)
        string smtpServer = Environment.GetEnvironmentVariable("SmtpServer") ?? "";
        int smtpPort = int.Parse(Environment.GetEnvironmentVariable("SmtpPort") ?? "587");
        string senderEmail = Environment.GetEnvironmentVariable("SenderEmail") ?? "";
        string senderPassword = Environment.GetEnvironmentVariable("SenderPassword") ?? "";
        string recipientEmail = Environment.GetEnvironmentVariable("RecipientEmail") ?? "";

        // 3. Send the Email
        bool success = await SendContactEmailAsync(name, email, subject, message, 
                                                 smtpServer, smtpPort, senderEmail, 
                                                 senderPassword, recipientEmail);

        // 4. Return Response
        var response = req.CreateResponse(success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
        
        // This tells the browser where to go after the user clicks "Submit"
        // You can create a "success.html" in your frontend folder
        response.Headers.Add("Location", "/success.html"); 
        response.StatusCode = HttpStatusCode.Redirect;

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