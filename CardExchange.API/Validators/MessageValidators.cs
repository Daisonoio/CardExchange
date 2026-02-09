using CardExchange.API.DTOs.Requests;
using FluentValidation;

namespace CardExchange.API.Validators
{
    public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
    {
        public SendMessageRequestValidator()
        {
            RuleFor(x => x.RecipientId)
                .GreaterThan(0).WithMessage("Il destinatario è obbligatorio");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Il contenuto del messaggio è obbligatorio")
                .MaximumLength(2000).WithMessage("Il messaggio non può superare 2000 caratteri");
        }
    }
}
