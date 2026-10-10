using System.Globalization;
using Clinic.Application.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Clinic.Web.Localization;

/// <summary>
/// Binds numbers the same way in both languages: Persian or Latin digits, with a dot, a Persian
/// decimal sign or a slash as the decimal point. The Persian culture would otherwise reject "72.5".
/// </summary>
public sealed class NumberBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }
        context.ModelState.SetModelValue(context.ModelName, value);

        var text = JalaliDate.ToLatinDigits(value.FirstValue ?? "").Trim().Replace('٫', '.').Replace('/', '.').Replace(',', '.');
        var type = Nullable.GetUnderlyingType(context.ModelType) ?? context.ModelType;
        if (text.Length == 0)
        {
            if (type == context.ModelType)
            {
                context.ModelState.TryAddModelError(context.ModelName, context.ModelMetadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(value.FirstValue ?? ""));
            }
            else
            {
                context.Result = ModelBindingResult.Success(null);
            }
            return Task.CompletedTask;
        }

        object? parsed = type == typeof(int)
            ? int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var i) ? i : null
            : double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (parsed is null)
        {
            context.ModelState.TryAddModelError(context.ModelName,
                context.ModelMetadata.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor(value.FirstValue ?? "", context.ModelMetadata.GetDisplayName()));
            return Task.CompletedTask;
        }
        context.Result = ModelBindingResult.Success(parsed);
        return Task.CompletedTask;
    }

    public sealed class Provider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
            return type == typeof(int) || type == typeof(double) ? new NumberBinder() : null;
        }
    }

    /// <summary>A number as a form field value.</summary>
    public static string Format(double? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
}
