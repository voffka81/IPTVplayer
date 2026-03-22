namespace TV_Player.MAUI
{
    /// <summary>
    /// Configuration settings for playlist and EPG sources
    /// </summary>
    public class PlaylistSettings
    {
        /// <summary>
        /// URL to the M3U playlist file
        /// </summary>
        public string M3UUrl { get; set; }

        /// <summary>
        /// URL to the EPG (Electronic Program Guide) XML file
        /// Can be overridden by x-tvg-url in M3U file
        /// </summary>
        public string EpgUrl { get; set; }

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to cache EPG data locally
        /// </summary>
        public bool CacheEpgLocally { get; set; } = true;

        /// <summary>
        /// Cache validity period in days
        /// </summary>
        public int CacheValidityDays { get; set; } = 3;

        /// <summary>
        /// Load default settings
        /// </summary>
        public static PlaylistSettings Default => new PlaylistSettings
        {
            M3UUrl = "http://pl.da-tv.vip/a71e77fa/835b3216/tv.m3u",
            EpgUrl = string.Empty  // Will be extracted from M3U file
        };
    }
}
