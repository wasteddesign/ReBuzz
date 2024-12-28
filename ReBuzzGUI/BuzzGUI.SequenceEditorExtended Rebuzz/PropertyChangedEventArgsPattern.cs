using BuzzGUI.Interfaces;
using System.ComponentModel;

namespace BuzzGUI.SequenceEditor
{
    public class PropertyChangedEventArgsPattern : PropertyChangedEventArgs
    {
        
        public PropertyChangedEventArgsPattern(string name, IPattern pattern = null, int index = 0) : base(name)
        {
            Name = name;
            Pattern = pattern;
            Index = index;
        }

        public string Name { get; }
        public IPattern Pattern { get; }
        public int Index { get; }
    }
}
