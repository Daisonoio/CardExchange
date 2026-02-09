using CardExchange.API.DTOs.Requests;
using FluentValidation;

namespace CardExchange.API.Validators
{
    public class CreateWishlistItemRequestValidator : AbstractValidator<CreateWishlistItemRequest>
    {
        public CreateWishlistItemRequestValidator()
        {
            RuleFor(x => x.CardInfoId)
                .GreaterThan(0).WithMessage("L'ID della carta è obbligatorio");

            RuleFor(x => x.PreferredCondition)
                .InclusiveBetween(1, 8).When(x => x.PreferredCondition.HasValue)
                .WithMessage("Condizione non valida (1-8)");

            RuleFor(x => x.MaxPrice)
                .InclusiveBetween(0, 999999.99m).When(x => x.MaxPrice.HasValue)
                .WithMessage("Il prezzo deve essere tra 0 e 999999.99");

            RuleFor(x => x.Notes)
                .MaximumLength(500).When(x => x.Notes != null);

            RuleFor(x => x.Priority)
                .InclusiveBetween(1, 3).WithMessage("La priorità deve essere 1 (alta), 2 (media) o 3 (bassa)");
        }
    }
}
