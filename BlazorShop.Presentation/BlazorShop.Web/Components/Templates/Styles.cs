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

        public static string GetFont(FontStyles font) =>
            font switch
            {
                FontStyles.Arial => "font-family: Arial;",
                FontStyles.Calibri => "font-family: Calibri;",
                FontStyles.TimesNewRoman => "font-family: 'Times New Roman';",
                FontStyles.ComicSans => "font-family: 'Comic Sans MS';",
                FontStyles.Georgia => "font-family: Georgia;",
                _ => string.Empty
            };
    }
}