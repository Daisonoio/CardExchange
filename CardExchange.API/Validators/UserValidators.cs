using CardExchange.API.DTOs.Requests;
using FluentValidation;

namespace CardExchange.API.Validators
{
    public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
    {
        public CreateUserRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("L'email è obbligatoria")
                .EmailAddress().WithMessage("Formato email non valido")
                .MaximumLength(256);

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Lo username è obbligatorio")
                .MinimumLength(3).WithMessage("Lo username deve essere di almeno 3 caratteri")
                .MaximumLength(50)
                .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("Lo username può contenere solo lettere, numeri, trattini e underscore");

            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("La password è obbligatoria")
                .MinimumLength(8)
                .Matches(@"[A-Z]").WithMessage("La password deve contenere almeno una lettera maiuscola")
                .Matches(@"[a-z]").WithMessage("La password deve contenere almeno una lettera minuscola")
                .Matches(@"\d").WithMessage("La password deve contenere almeno un numero")
                .Matches(@"[@$!%*?&]").WithMessage("La password deve contenere almeno un carattere speciale");
        }
    }

    public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
    {
        public UpdateUserRequestValidator()
        {
            RuleFor(x => x.FirstName).MaximumLength(100).When(x => x.FirstName != null);
            RuleFor(x => x.LastName).MaximumLength(100).When(x => x.LastName != null);
            RuleFor(x => x.Bio).MaximumLength(500).When(x => x.Bio != null);
            RuleFor(x => x.PaypalUsername).MaximumLength(120).When(x => x.PaypalUsername != null);
            RuleFor(x => x.SatispayUsername).MaximumLength(120).When(x => x.SatispayUsername != null);
            RuleFor(x => x.PaymentQrCodeUrl)
                .MaximumLength(600)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .When(x => !string.IsNullOrWhiteSpace(x.PaymentQrCodeUrl))
                .WithMessage("L'URL QR pagamento non è valido");
        }
    }

    public class CreateUserLocationRequestValidator : AbstractValidator<CreateUserLocationRequest>
    {
        public CreateUserLocationRequestValidator()
        {
            RuleFor(x => x.City).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Province).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
            RuleFor(x => x.PostalCode).MaximumLength(20).When(x => x.PostalCode != null);
            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
            RuleFor(x => x.MaxDistanceKm).InclusiveBetween(1, 1000);
        }
    }
}
