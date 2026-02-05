namespace SafeTravel.Domain.Models;

/// <summary>
/// Represents the result of a travel recommendation evaluation.
/// Contains both the decision and human-readable reasoning.
/// </summary>
public sealed record RecommendationResult
{
    public bool IsRecommended { get; }
    public string Reason { get; }

    private RecommendationResult(bool isRecommended, string reason)
    {
        IsRecommended = isRecommended;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>
    /// Creates a positive recommendation result.
    /// </summary>
    public static RecommendationResult Recommended(string reason) =>
        new(true, reason);

    /// <summary>
    /// Creates a negative recommendation result.
    /// </summary>
    public static RecommendationResult NotRecommended(string reason) =>
        new(false, reason);

    public override string ToString() =>
        IsRecommended ? $"✓ Recommended: {Reason}" : $"✗ Not Recommended: {Reason}";
}
