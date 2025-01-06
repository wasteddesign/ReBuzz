using BuzzGUI.Common;
using System.Collections.Generic;

namespace WDE.ModernPatternEditor.ColumnRenderer
{
    static class BeatVisualAllocator
    {
        static Stack<BeatVisual> freeStack = new Stack<BeatVisual>();

        public static BeatVisual Allocate()
        {
            if (freeStack.Count > 0)
                return freeStack.Pop();

            DebugConsole.WriteLine("BeatVisual.Allocate new");
            return new BeatVisual();
        }

        public static void Free(BeatVisual bv)
        {
            freeStack.Push(bv);
        }
    }
}
