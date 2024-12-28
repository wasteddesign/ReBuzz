using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.InterfaceExtensions
{
    public static class ParameterExtensions
    {
        public static string GetHexValueString(this IParameter parameter, int v)
        {
            switch (parameter.Type)
            {
                case ParameterType.Switch: return v.ToString("X1");
                case ParameterType.Byte: return parameter.Flags.HasFlag(ParameterFlags.Ascii) ? ((char)v).ToString() : v.ToString("X2");
                case ParameterType.Word: return v.ToString("X4");
                case ParameterType.Note: return BuzzNote.TryToString(v) ?? "<invalid>";
                default: return "?";
            }

        }

        public static string GetValueDescriptionWithHexValue(this IParameter parameter, int value)
        {
            return parameter.DescribeValue(value) + " (" + GetHexValueString(parameter, value) + ")";
        }
    }
}
