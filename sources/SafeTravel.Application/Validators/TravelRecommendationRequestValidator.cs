using FluentValidation;
using SafeTravel.Application.DTOs;

namespace SafeTravel.Application.Validators;

/// <summary>
/// Validates travel recommendation requests.
/// </summary>
public sealed class TravelRecommendationRequestValidator : AbstractValidator<TravelRecommendationRequest>
{
    public TravelRecommendationRequestValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90 degrees.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180 degrees.");

        RuleFor(x => x.DestinationDistrict)
            .NotEmpty()
            .WithMessage("Destination district is required.");

        RuleFor(x => x.TravelDate)
            .Must(BeWithinNext7Days)
            .WithMessage("Travel date must be within the next 7 days (today through today + 6 days).");
    }

    private static bool BeWithinNext7Days(DateOnly travelDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var maxDate = today.AddDays(6);
        return travelDate >= today && travelDate <= maxDate;
    }
}
