using CardExchange.API.DTOs.Requests;
using FluentValidation;

namespace CardExchange.API.Validators
{
    public class CreateCardRequestValidator : AbstractValidator<CreateCardRequest>
    {
        public CreateCardRequestValidator()
        {
            RuleFor(x => x.CardInfoId)
                .GreaterThan(0).WithMessage("L'ID della carta è obbligatorio");

            RuleFor(x => x.Condition)
                .InclusiveBetween(1, 8).WithMessage("Condizione non valida (1-8)");

            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(1).WithMessage("La quantità deve essere almeno 1");

            RuleFor(x => x.Notes)
                .MaximumLength(500).When(x => x.Notes != null);

            RuleFor(x => x.EstimatedValue)
                .InclusiveBetween(0, 999999.99m).When(x => x.EstimatedValue.HasValue)
                .WithMessage("Il valore deve essere tra 0 e 999999.99");
        }
    }

    public class UpdateCardRequestValidator : AbstractValidator<UpdateCardRequest>
    {
        public UpdateCardRequestValidator()
        {
            RuleFor(x => x.Condition)
                .InclusiveBetween(1, 8).When(x => x.Condition.HasValue)
                .WithMessage("Condizione non valida (1-8)");

            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(1).When(x => x.Quantity.HasValue)
                .WithMessage("La quantità deve essere almeno 1");

            RuleFor(x => x.Notes)
                .MaximumLength(500).When(x => x.Notes != null);

            RuleFor(x => x.EstimatedValue)
                .InclusiveBetween(0, 999999.99m).When(x => x.EstimatedValue.HasValue)
                .WithMessage("Il valore deve essere tra 0 e 999999.99");
        }
    }
}
