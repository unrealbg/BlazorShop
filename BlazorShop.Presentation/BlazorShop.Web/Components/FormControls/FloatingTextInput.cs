namespace BlazorShop.Web.Components.FormControls
{
    using System;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.AspNetCore.Components.Rendering;

    public class FloatingTextInput : InputText
    {
        private readonly string inputId = $"input-{Guid.NewGuid():N}";

        [Parameter]
        public string? Placeholder { get; set; }

        [Parameter]
        public string? Label { get; set; }

        [Parameter]
        public string? Type { get; set; } = "text";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var current = CurrentValueAsString ?? string.Empty;
            var resolvedInputId = AdditionalAttributes?.TryGetValue("id", out var configuredId) == true
                && !string.IsNullOrWhiteSpace(configuredId?.ToString())
                    ? configuredId.ToString()!
                    : inputId;

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "form-floating mb-3");

            builder.OpenElement(2, "input");
            builder.AddMultipleAttributes(3, AdditionalAttributes);
            builder.AddAttribute(4, "id", resolvedInputId);
            builder.AddAttribute(5, "class", CssClass);
            builder.AddAttribute(6, "value", BindConverter.FormatValue(current));
            builder.AddAttribute(7, "type", Type?.ToLowerInvariant() ?? "text");
            builder.AddAttribute(8, "aria-required", "true");
            builder.AddAttribute(9, "placeholder", Placeholder);
            builder.AddAttribute(10, "oninput", EventCallback.Factory.CreateBinder<string>(
                this,
                value => CurrentValueAsString = value,
                current));
            builder.CloseElement(); // input

            if (!string.IsNullOrEmpty(Label))
            {
                builder.OpenElement(11, "label");
                builder.AddAttribute(12, "for", resolvedInputId);
                builder.AddContent(13, Label);
                builder.CloseElement(); // label
            }

            builder.CloseElement(); // div
        }
    }
}
