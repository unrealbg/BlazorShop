namespace BlazorShop.Web.Components.FormControls
{
    using System;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.AspNetCore.Components.Rendering;

    public class NumberInput : InputNumber<int>
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "input");
            builder.AddMultipleAttributes(1, AdditionalAttributes);
            builder.AddAttribute(2, "class", CssClass);
            builder.AddAttribute(3, "value", BindConverter.FormatValue(CurrentValueAsString));
            builder.AddAttribute(4, "type", "number");
            builder.AddAttribute(
                5,
                "oninput",
                EventCallback.Factory.CreateBinder<string>(
                    this,
                    value => CurrentValueAsString = value,
                CurrentValueAsString));
            builder.CloseElement();
        }
    }
}