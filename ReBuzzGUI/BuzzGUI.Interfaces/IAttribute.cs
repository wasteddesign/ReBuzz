namespace BuzzGUI.Interfaces
{
    public interface IAttribute
    {
        string Name { get; }
        int MinValue { get; }
        int MaxValue { get; }
        int DefValue { get; }

        int Value { get; set; }

        // gear.xml
        bool HasUserDefValue { get; }
        int UserDefValue { get; }
        bool UserDefValueOverridesPreset { get; }
    }
}
