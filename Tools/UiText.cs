using System;
using System.Globalization;
using System.Text;

namespace ParkManager
{
    /// <summary>
    /// Builds language-neutral UI messages. The simulation publishes a message
    /// key plus arguments; the React UI resolves the key in <c>i18n.ts</c> for
    /// the active game language. Wire format:
    /// <c>{"k":"status.pointMoved","a":[3,"name",{"k":"…","a":[]}]}</c>.
    /// A string argument that is itself a message is embedded as a nested
    /// object, so composed texts ("Error: {reason}") stay translatable.
    /// Plain text without the prefix is still shown verbatim by the UI.
    /// </summary>
    internal static class UiText
    {
        private const string Prefix = "{\"k\":";

        internal static string Of(string key, params object[] args)
        {
            var builder = new StringBuilder(32 + key.Length);
            builder.Append(Prefix);
            AppendString(builder, key);
            builder.Append(",\"a\":[");
            if (args != null)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0) builder.Append(',');
                    AppendValue(builder, args[i]);
                }
            }
            builder.Append("]}");
            return builder.ToString();
        }

        internal static bool IsMessage(string text)
            => text != null && text.StartsWith(Prefix, StringComparison.Ordinal);

        private static void AppendValue(StringBuilder builder, object value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    return;
                case string text when IsMessage(text):
                    builder.Append(text);
                    return;
                case string text:
                    AppendString(builder, text);
                    return;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    return;
                case int _:
                case long _:
                case short _:
                case byte _:
                case uint _:
                case ulong _:
                case ushort _:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
                case float single:
                    AppendNumber(builder, single);
                    return;
                case double number:
                    AppendNumber(builder, number);
                    return;
                default:
                    AppendString(builder, value.ToString());
                    return;
            }
        }

        private static void AppendNumber(StringBuilder builder, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                builder.Append("null");
                return;
            }
            builder.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder builder, string text)
            => Json.AppendString(builder, text);
    }
}
