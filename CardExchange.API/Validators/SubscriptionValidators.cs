using CardExchange.API.DTOs.Requests;
using FluentValidation;

namespace CardExchange.API.Validators
{
    public class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
    {
        public CreateSubscriptionRequestValidator()
        {
            RuleFor(x => x.PlanId)
                .GreaterThan(0).WithMessage("Il piano è obbligatorio");

            RuleFor(x => x.PaymentReference)
                .MaximumLength(100).When(x => x.PaymentReference != null);
        }
    }
}
