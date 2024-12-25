namespace BlazorShop.Web.Components.Templates
{
    public static class Styles
    {
        public enum BackgroundColors { Primary, Secondary, Danger, Success, Dark, Light, Warning, Info }
        public enum TextColors { Primary, Secondary, Danger, Success, Dark, Light, Warning, Info }
        public enum TableFormats { Striped, Hover, Bordered, Dark, Light }
        public enum FontStyles { Arial, Calibri, TimesNewRoman, ComicSans, Georgia }

        public static string GetBackgroundColor(BackgroundColors color) =>
            color switch
                {
                    BackgroundColors.Primary => "bg-primary",
                    BackgroundColors.Secondary => "bg-secondary",
                    BackgroundColors.Danger => "bg-danger",
                    BackgroundColors.Success => "bg-success",
                    BackgroundColors.Dark => "bg-dark",
                    BackgroundColors.Light => "bg-light",
                    BackgroundColors.Warning => "bg-warning",
                    BackgroundColors.Info => "bg-info",
                    _ => string.Empty
                };

        public static string GetTextColor(TextColors color) =>
            color switch
                {
                    TextColors.Primary => "text-primary",
                    TextColors.Secondary => "text-secondary",
                    TextColors.Danger => "text-danger",
                    TextColors.Success => "text-success",
                    TextColors.Dark => "text-dark",
                    TextColors.Light => "text-white",
                    TextColors.Warning => "text-warning",
                    TextColors.Info => "text-info",
                    _ => string.Empty
                };

        public static string GetTableFormat(TableFormats format) =>
            format switch
                {
                    TableFormats.Striped => "table table-striped",
                    TableFormats.Hover => "table table-hover",
                    TableFormats.Bordered => "table table-bordered",
                    TableFormats.Dark => "table table-dark",
                    TableFormats.Light => "table table-light",
                    _ => "table"
                };

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