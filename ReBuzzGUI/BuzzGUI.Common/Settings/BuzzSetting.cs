using System;

namespace BuzzGUI.Common.Settings
{
    public enum BuzzSettingType { Plain, FontFamily, FontStyle, FontWeight, FontStretch };

    [AttributeUsage(AttributeTargets.Property)]
    public class BuzzSetting : Attribute
    {
        public BuzzSetting(object defvalue) { DefaultValue = defvalue; }
        public object DefaultValue { get; private set; }

        public string Description { get; set; }
        public BuzzSettingType Type { get; set; }
        public int Minimum { get; set; }
        public int Maximum { get; set; }

        public object[] Presets;
    }
}
