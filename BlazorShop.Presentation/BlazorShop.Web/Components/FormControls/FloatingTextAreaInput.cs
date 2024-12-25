namespace BlazorShop.Web.Components.FormControls
{
    using System;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.AspNetCore.Components.Rendering;

    public class FloatingTextAreaInput : InputTextArea
    {
        [Parameter]
        public string? Placeholder { get; set; }

        [Parameter]
        public string? Label { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "form-floating mb-3");

            builder.OpenElement(2, "textarea");
            builder.AddMultipleAttributes(3, AdditionalAttributes);
            builder.AddAttribute(4, "class", CssClass);
            builder.AddAttribute(5, "value", BindConverter.FormatValue(CurrentValueAsString));
            builder.AddAttribute(6, "aria-required", "true");
            builder.AddAttribute(7, "placeholder", Placeholder);
            builder.AddAttribute(
                8,
                "oninput",
                EventCallback.Factory.CreateBinder<string>(
                    this,
                    value => CurrentValueAsString = value,
                CurrentValueAsString));
            builder.CloseElement();

            if (!string.IsNullOrEmpty(Label))
            {
                builder.OpenElement(9, "label");
                builder.AddContent(10, Label);
                builder.CloseElement();
            }

            builder.CloseElement();
        }
    }
}
