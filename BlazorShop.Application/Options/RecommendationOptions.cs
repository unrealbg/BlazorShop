namespace BlazorShop.Application.Options
{
    public class RecommendationOptions
    {
        public const string SectionName = "Recommendations";

        /// <summary>
        /// Maximum number of recommendations to return
        /// </summary>
        public int MaxRecommendations { get; set; } = 6;

        /// <summary>
        /// Cache duration in hours
        /// </summary>
        public int CacheDurationHours { get; set; } = 1;

        /// <summary>
        /// Sliding expiration in minutes
        /// </summary>
        public int SlidingExpirationMinutes { get; set; } = 30;

        /// <summary>
        /// Enable order-based recommendations
        /// </summary>
        public bool EnableOrderBasedRecommendations { get; set; } = true;

        /// <summary>
        /// Minimum order count to use order-based recommendations
        /// </summary>
        public int MinimumOrderCount { get; set; } = 5;
    }
}
