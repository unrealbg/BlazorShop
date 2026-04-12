namespace BlazorShop.Web.Components.Templates
{
    public static class Styles
    {
        public enum BackgroundColors { Primary, Secondary, Danger, Success, Dark, Light, Warning, Info }
        public enum TextColors { Primary, Secondary, Danger, Success, Dark, Light, Warning, Info }
        public enum TableFormats { Striped, Hover, Bordered, Dark, Light }
        public enum FontStyles { Arial, Calibri, TimesNewRoman, ComicSans, Georgia }

        // Deprecated Bootstrap mapping retained for backward compatibility (no-op)
        public static string GetBackgroundColor(BackgroundColors color) => string.Empty;
        public static string GetTextColor(TextColors color) => string.Empty;
        public static string GetTableFormat(TableFormats format) => string.Empty;

        public static string GetFontClass(FontStyles font) =>
            font switch
            {
                FontStyles.Arial => "font-arial",
                FontStyles.Calibri => "font-calibri",
                FontStyles.TimesNewRoman => "font-times",
                FontStyles.ComicSans => "font-comic",
                FontStyles.Georgia => "font-georgia",
                _ => string.Empty
            };

        public static string GetFontSizeClass(string? fontSize)
        {
            if (string.IsNullOrWhiteSpace(fontSize))
            {
                return string.Empty;
            }

            return fontSize.Trim().ToLowerInvariant() switch
            {
                "12px" => "font-size-12",
                "13px" => "font-size-13",
                "14px" => "font-size-14",
                "15px" => "font-size-15",
                "16px" => "font-size-16",
                "18px" => "font-size-18",
                "20px" => "font-size-20",
                "24px" => "font-size-24",
                _ => string.Empty,
            };
        }
    }
}