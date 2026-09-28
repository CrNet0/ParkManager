using System.Globalization;
using System.Text;

namespace ParkManager
{
    /// <summary>
    /// Minimal JSON writing for the UI bridge. The payloads are small and
    /// flat, so a serializer dependency would only add mod-loader risk.
    /// </summary>
    internal static class Json
    {
        /// <summary>Appends <paramref name="text"/> as a quoted JSON string.</summary>
        internal static StringBuilder AppendString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (var c in text ?? string.Empty)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            builder.Append("\\u").Append(((int)c).ToString("x4",
                                CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            return builder.Append('"');
        }
    }
}
