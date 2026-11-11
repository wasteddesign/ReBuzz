using AwesomeAssertions;
using BuzzGUI.Common;
using System.Collections.Generic;

namespace ReBuzzTests
{
    /// <summary>
    /// VUMeterLevels replaces a Tuple that was reassigned on every VU update. Theme bindings
    /// use VUMeterLevel.Item1/Item2, so it must keep those names and raise PropertyChanged for
    /// exactly the level that changed.
    /// </summary>
    public class VUMeterLevelsTests
    {
        [Test]
        public void RaisesPropertyChangedOnlyForLevelsThatChange()
        {
            var levels = new VUMeterLevels();
            var raised = new List<string>();
            levels.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            levels.Item1.Should().Be(0.0);
            levels.Item2.Should().Be(0.0);

            levels.Set(0.0, 0.0);
            raised.Should().BeEmpty();

            levels.Set(0.5, 0.0);
            raised.Should().Equal("Item1");

            levels.Set(0.5, 0.25);
            raised.Should().Equal("Item1", "Item2");

            levels.Set(0.75, 0.5);
            raised.Should().Equal("Item1", "Item2", "Item1", "Item2");
            levels.Item1.Should().Be(0.75);
            levels.Item2.Should().Be(0.5);
        }
    }
}
