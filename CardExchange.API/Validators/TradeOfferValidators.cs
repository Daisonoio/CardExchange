using CardExchange.API.DTOs.Requests;
using FluentValidation;

namespace CardExchange.API.Validators
{
    public class CreateTradeOfferRequestValidator : AbstractValidator<CreateTradeOfferRequest>
    {
        public CreateTradeOfferRequestValidator()
        {
            RuleFor(x => x.ReceiverId)
                .GreaterThan(0).WithMessage("Il destinatario è obbligatorio");

            RuleFor(x => x.Message)
                .MaximumLength(1000).When(x => x.Message != null);

            RuleFor(x => x.OfferedCards)
                .NotEmpty().WithMessage("Devi offrire almeno una carta");

            RuleFor(x => x.RequestedCards)
                .NotEmpty().WithMessage("Devi richiedere almeno una carta");

            RuleForEach(x => x.OfferedCards).SetValidator(new TradeOfferItemRequestValidator());
            RuleForEach(x => x.RequestedCards).SetValidator(new TradeOfferItemRequestValidator());
        }
    }

    public class TradeOfferItemRequestValidator : AbstractValidator<TradeOfferItemRequest>
    {
        public TradeOfferItemRequestValidator()
        {
            RuleFor(x => x.CardId).GreaterThan(0).WithMessage("ID carta non valido");
            RuleFor(x => x.Quantity).InclusiveBetween(1, 100).WithMessage("Quantità deve essere tra 1 e 100");
        }
    }

    public class CounterOfferRequestValidator : AbstractValidator<CounterOfferRequest>
    {
        public CounterOfferRequestValidator()
        {
            RuleFor(x => x.Message)
                .MaximumLength(1000).When(x => x.Message != null);

            RuleFor(x => x.OfferedCards)
                .NotEmpty().WithMessage("Devi offrire almeno una carta");

            RuleFor(x => x.RequestedCards)
                .NotEmpty().WithMessage("Devi richiedere almeno una carta");

            RuleForEach(x => x.OfferedCards).SetValidator(new TradeOfferItemRequestValidator());
            RuleForEach(x => x.RequestedCards).SetValidator(new TradeOfferItemRequestValidator());
        }
    }

    public class CreateTradeReviewRequestValidator : AbstractValidator<CreateTradeReviewRequest>
    {
        public CreateTradeReviewRequestValidator()
        {
            RuleFor(x => x.Rating)
                .InclusiveBetween(1, 5).WithMessage("Il rating deve essere tra 1 e 5");

            RuleFor(x => x.Comment)
                .MaximumLength(1000).When(x => x.Comment != null);
        }
    }
}
