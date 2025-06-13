using AtmaFileSystem;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzzTests.Automation
{
    public class RecentFilesDriverExtension
    {
        private readonly FakeInMemoryRegistry fakeInMemoryRegistry;

        internal RecentFilesDriverExtension(FakeInMemoryRegistry fakeInMemoryRegistry)
        {
            this.fakeInMemoryRegistry = fakeInMemoryRegistry;
        }

        public void AssertHasNoEntryFor(AbsoluteFilePath songPath)
        {
            RecentFiles().Should().NotContain(songPath.ToString());
        }

        public void AssertHasEntry(int index, AbsoluteFilePath songPath)
        {
            RecentFiles().ElementAt(index).Should().Be(songPath.ToString());
        }

        private IEnumerable<string> RecentFiles()
        {
            return fakeInMemoryRegistry.ReadNumberedList<string>("File", "Recent File List");
        }
    }
}