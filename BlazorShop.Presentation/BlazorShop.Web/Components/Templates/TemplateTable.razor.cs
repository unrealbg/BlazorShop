namespace BlazorShop.Web.Components.Templates
{
    using Microsoft.AspNetCore.Components;

    public partial class TemplateTable
    {
        [Parameter]
        public RenderFragment HeaderTemplate { get; set; } = default!;

        [Parameter]
        public RenderFragment ContentTemplate { get; set; } = default!;

        [Parameter]
        public RenderFragment? ButtonTemplate { get; set; } = null!;

        [Parameter]
        public RenderFragment? ModalContent { get; set; } = null!;

        [Parameter]
        public Styles.TableFormats TableFormat { get; set; } = Styles.TableFormats.Light;

        [Parameter]
        public Styles.BackgroundColors HeaderBackground { get; set; } = Styles.BackgroundColors.Primary;

        [Parameter]
        public Styles.TextColors HeaderTextColor { get; set; } = Styles.TextColors.Light;

        [Parameter]
        public Styles.BackgroundColors ContentBackground { get; set; } = Styles.BackgroundColors.Light;

        [Parameter]
        public Styles.TextColors ContentTextColor { get; set; } = Styles.TextColors.Dark;

        [Parameter]
        public Styles.FontStyles HeaderFont { get; set; } = Styles.FontStyles.Arial;

        [Parameter]
        public Styles.FontStyles ContentFont { get; set; } = Styles.FontStyles.Calibri;

        [Parameter]
        public string HeaderFontSize { get; set; } = "16px";

        [Parameter]
        public string ContentFontSize { get; set; } = "14px";

        [Parameter]
        public string CssClass { get; set; } = string.Empty;

        private bool ShowModal { get; set; } = false;
    }
}