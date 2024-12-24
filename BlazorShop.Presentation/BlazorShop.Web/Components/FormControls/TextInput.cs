namespace BlazorShop.Web.Components.FormControls
{
    using System;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.AspNetCore.Components.Rendering;

    public class TextInput : InputText
    {
        [Parameter]
        public string? Placeholder { get; set; }

        [Parameter]
        public string? Label { get; set; }

        [Parameter]
        public string? Type { get; set; } = "text";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "input");
            builder.AddMultipleAttributes(1, AdditionalAttributes);
            builder.AddAttribute(2, "class", CssClass);
            builder.AddAttribute(3, "value", BindConverter.FormatValue(CurrentValueAsString));
            builder.AddAttribute(4, "type", Type?.ToLowerInvariant() ?? "text");
            builder.AddAttribute(5, "placeholder", Placeholder);
            builder.AddAttribute(
                6,
                "oninput",
                EventCallback.Factory.CreateBinder<string>(
                    this,
                    value => CurrentValueAsString = value,
                CurrentValueAsString));
            builder.CloseElement();

            if (!string.IsNullOrEmpty(Label))
            {
                builder.OpenElement(7, "label");
                builder.AddContent(8, Label);
                builder.CloseElement();
            }
        }
    }
}