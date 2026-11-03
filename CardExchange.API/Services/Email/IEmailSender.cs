namespace CardExchange.API.Services.Email
{
    public interface IEmailSender
    {
        Task SendAsync(string toAddress, string toName, EmailMessage message, CancellationToken cancellationToken = default);
    }

    public record EmailMessage(string Subject, string HtmlBody, string TextBody);
}
