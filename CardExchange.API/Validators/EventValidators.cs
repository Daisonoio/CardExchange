using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using FluentValidation;

namespace CardExchange.API.Validators
{
    public class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
    {
        public CreateEventRequestValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Il titolo è obbligatorio")
                .MaximumLength(200).WithMessage("Il titolo non può superare 200 caratteri");

            RuleFor(x => x.StartDate)
                .GreaterThan(DateTime.UtcNow).WithMessage("La data di inizio deve essere nel futuro");

            RuleFor(x => x.EndDate)
                .GreaterThan(x => x.StartDate).When(x => x.EndDate.HasValue)
                .WithMessage("La data di fine deve essere successiva alla data di inizio");

            RuleFor(x => x.Address)
                .NotEmpty().WithMessage("L'indirizzo è obbligatorio")
                .MaximumLength(300);

            RuleFor(x => x.City)
                .NotEmpty().WithMessage("La città è obbligatoria")
                .MaximumLength(100);

            RuleFor(x => x.Country)
                .NotEmpty().WithMessage("Il paese è obbligatorio")
                .MaximumLength(100);

            RuleFor(x => x.Type)
                .InclusiveBetween(1, 99).WithMessage("Tipo di evento non valido");

            RuleFor(x => x.MaxParticipants)
                .GreaterThan(0).When(x => x.MaxParticipants.HasValue)
                .WithMessage("Il numero massimo di partecipanti deve essere positivo");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
                .WithMessage("Latitudine non valida");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
                .WithMessage("Longitudine non valida");

            RuleFor(x => x.Description)
                .MaximumLength(2000).When(x => x.Description != null);
        }
    }

    public class CreatePriceAlertRequestValidator : AbstractValidator<CreatePriceAlertRequest>
    {
        public CreatePriceAlertRequestValidator()
        {
            RuleFor(x => x.CardInfoId)
                .GreaterThan(0).WithMessage("L'ID della carta è obbligatorio");

            RuleFor(x => x.TargetPrice)
                .GreaterThan(0).WithMessage("Il prezzo target deve essere maggiore di zero")
                .LessThanOrEqualTo(999999.99m).WithMessage("Il prezzo target non può superare 999999.99");

            RuleFor(x => x.Direction)
                .InclusiveBetween(1, 2).WithMessage("Direzione non valida (1=sopra, 2=sotto)");

            RuleFor(x => x.Currency)
                .InclusiveBetween(1, 2).WithMessage("Valuta non valida (1=USD, 2=EUR)");
        }
    }

    public class TradeAnalyzeRequestValidator : AbstractValidator<TradeAnalyzeRequest>
    {
        public TradeAnalyzeRequestValidator()
        {
            RuleFor(x => x)
                .Must(x => x.OfferedCardIds.Any() || x.RequestedCardIds.Any())
                .WithMessage("Devi specificare almeno una carta offerta o richiesta");

            RuleFor(x => x.OfferedCardIds)
                .ForEach(id => id.GreaterThan(0).WithMessage("ID carta non valido"));

            RuleFor(x => x.RequestedCardIds)
                .ForEach(id => id.GreaterThan(0).WithMessage("ID carta non valido"));
        }
    }
}
