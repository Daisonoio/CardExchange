using CardExchange.API.DTOs.Requests;
using CardExchange.API.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace CardExchange.Tests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors()
    {
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            Password = "Password1!",
            ConfirmPassword = "Password1!"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEmail_HasError()
    {
        var request = new RegisterRequest { Email = "", Username = "test", FirstName = "T", LastName = "U", Password = "P@ssw0rd!", ConfirmPassword = "P@ssw0rd!" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void InvalidEmail_HasError()
    {
        var request = new RegisterRequest { Email = "not-an-email", Username = "test", FirstName = "T", LastName = "U", Password = "P@ssw0rd!", ConfirmPassword = "P@ssw0rd!" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void ShortUsername_HasError()
    {
        var request = new RegisterRequest { Email = "t@t.com", Username = "ab", FirstName = "T", LastName = "U", Password = "P@ssw0rd!", ConfirmPassword = "P@ssw0rd!" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void UsernameWithSpaces_HasError()
    {
        var request = new RegisterRequest { Email = "t@t.com", Username = "has spaces", FirstName = "T", LastName = "U", Password = "P@ssw0rd!", ConfirmPassword = "P@ssw0rd!" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void WeakPassword_HasError()
    {
        var request = new RegisterRequest { Email = "t@t.com", Username = "test", FirstName = "T", LastName = "U", Password = "password", ConfirmPassword = "password" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void MismatchedPasswords_HasError()
    {
        var request = new RegisterRequest { Email = "t@t.com", Username = "test", FirstName = "T", LastName = "U", Password = "P@ssw0rd!", ConfirmPassword = "Different1!" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }
}

public class CreateTradeOfferRequestValidatorTests
{
    private readonly CreateTradeOfferRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors()
    {
        var request = new CreateTradeOfferRequest
        {
            ReceiverId = 2,
            Message = "Ti propongo uno scambio",
            OfferedCards = new() { new() { CardId = 1, Quantity = 1 } },
            RequestedCards = new() { new() { CardId = 2, Quantity = 1 } }
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyOfferedCards_HasError()
    {
        var request = new CreateTradeOfferRequest
        {
            ReceiverId = 2,
            OfferedCards = new(),
            RequestedCards = new() { new() { CardId = 2, Quantity = 1 } }
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.OfferedCards);
    }

    [Fact]
    public void EmptyRequestedCards_HasError()
    {
        var request = new CreateTradeOfferRequest
        {
            ReceiverId = 2,
            OfferedCards = new() { new() { CardId = 1, Quantity = 1 } },
            RequestedCards = new()
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.RequestedCards);
    }

    [Fact]
    public void InvalidCardId_HasError()
    {
        var request = new CreateTradeOfferRequest
        {
            ReceiverId = 2,
            OfferedCards = new() { new() { CardId = 0, Quantity = 1 } },
            RequestedCards = new() { new() { CardId = 2, Quantity = 1 } }
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void InvalidQuantity_HasError()
    {
        var request = new CreateTradeOfferRequest
        {
            ReceiverId = 2,
            OfferedCards = new() { new() { CardId = 1, Quantity = 101 } },
            RequestedCards = new() { new() { CardId = 2, Quantity = 1 } }
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }
}

public class CreateCardRequestValidatorTests
{
    private readonly CreateCardRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors()
    {
        var request = new CreateCardRequest
        {
            CardInfoId = 1,
            Condition = 3,
            Quantity = 2,
            EstimatedValue = 15.50m
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void InvalidCondition_HasError()
    {
        var request = new CreateCardRequest { CardInfoId = 1, Condition = 9 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Condition);
    }

    [Fact]
    public void NegativeEstimatedValue_HasError()
    {
        var request = new CreateCardRequest { CardInfoId = 1, Condition = 1, EstimatedValue = -5m };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.EstimatedValue);
    }
}

public class CreateTradeReviewRequestValidatorTests
{
    private readonly CreateTradeReviewRequestValidator _validator = new();

    [Fact]
    public void ValidReview_NoErrors()
    {
        var request = new CreateTradeReviewRequest
        {
            Rating = 5,
            Comment = "Ottimo scambio!"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RatingZero_HasError()
    {
        var request = new CreateTradeReviewRequest { Rating = 0 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [Fact]
    public void RatingSix_HasError()
    {
        var request = new CreateTradeReviewRequest { Rating = 6 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [Fact]
    public void CommentTooLong_HasError()
    {
        var request = new CreateTradeReviewRequest { Rating = 3, Comment = new string('x', 1001) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Comment);
    }
}

public class SendMessageRequestValidatorTests
{
    private readonly SendMessageRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_NoErrors()
    {
        var request = new SendMessageRequest { RecipientId = 1, Content = "Ciao!" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyContent_HasError()
    {
        var request = new SendMessageRequest { RecipientId = 1, Content = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void ContentTooLong_HasError()
    {
        var request = new SendMessageRequest { RecipientId = 1, Content = new string('x', 2001) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }
}
