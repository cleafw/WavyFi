using System.Windows;
using System.Windows.Markup;

namespace WavyFi.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class TextExtension : MarkupExtension
{
    public TextExtension(string key) => Key = key;
    public string Key { get; }
    public override object ProvideValue(IServiceProvider serviceProvider) => UiText.Get(Key);
}

/// <summary>Language-independent identity for saved column layouts and descriptions.</summary>
public static class ColumnKey
{
    public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
        "Id", typeof(string), typeof(ColumnKey), new PropertyMetadata(null));
    public static string? GetId(DependencyObject value) => (string?)value.GetValue(IdProperty);
    public static void SetId(DependencyObject value, string id) => value.SetValue(IdProperty, id);
}
