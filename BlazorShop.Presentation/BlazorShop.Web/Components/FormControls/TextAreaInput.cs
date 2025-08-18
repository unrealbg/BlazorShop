namespace BlazorShop.Web.Components.FormControls
{
    using System;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.AspNetCore.Components.Rendering;

    public class TextAreaInput : InputTextArea
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var current = CurrentValueAsString ?? string.Empty;

            builder.OpenElement(0, "textarea");
            builder.AddMultipleAttributes(1, AdditionalAttributes);
            builder.AddAttribute(2, "class", CssClass);
            builder.AddAttribute(3, "value", BindConverter.FormatValue(current));
            builder.AddAttribute(
                4,
                "oninput",
                EventCallback.Factory.CreateBinder<string>(
                    this,
                    value => CurrentValueAsString = value,
                current));
            builder.CloseElement();
        }
    }
}