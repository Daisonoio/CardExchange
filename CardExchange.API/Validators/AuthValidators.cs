using CardExchange.API.DTOs.Requests;
using FluentValidation;

namespace CardExchange.API.Validators
{
    public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
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

            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Il nome è obbligatorio")
                .MaximumLength(100);

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Il cognome è obbligatorio")
                .MaximumLength(100);

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("La password è obbligatoria")
                .MinimumLength(8).WithMessage("La password deve essere di almeno 8 caratteri")
                .Matches(@"[A-Z]").WithMessage("La password deve contenere almeno una lettera maiuscola")
                .Matches(@"[a-z]").WithMessage("La password deve contenere almeno una lettera minuscola")
                .Matches(@"\d").WithMessage("La password deve contenere almeno un numero")
                .Matches(@"[@$!%*?&]").WithMessage("La password deve contenere almeno un carattere speciale (@$!%*?&)");

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.Password).WithMessage("Le password non coincidono");
        }
    }

    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.UsernameOrEmail)
                .NotEmpty().WithMessage("Username o email obbligatorio");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("La password è obbligatoria");
        }
    }

    public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
    {
        public RefreshTokenRequestValidator()
        {
            RuleFor(x => x.AccessToken).NotEmpty().WithMessage("L'access token è obbligatorio");
            RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Il refresh token è obbligatorio");
        }
    }
}
