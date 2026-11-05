using AwesomeAssertions;
using ReBuzz.FileOps;
using System;
using System.IO;

namespace ReBuzzTests
{
    /// <summary>
    /// The helpers BMXFile uses to read values from a song file.
    /// </summary>
    public class BMXFileReadTests
    {
        [Test]
        public void ReadsLittleEndianValues()
        {
            using var file = new TemporaryFile([0x34, 0x12, 0x78, 0x56, 0x34, 0x12, 0xAB]);
            using var fs = file.OpenRead();

            BMXFile.ReadUShort(fs).Should().Be(0x1234);
            BMXFile.ReadUInt(fs).Should().Be(0x12345678);
            BMXFile.ReadByte(fs).Should().Be(0xAB);
        }

        [Test]
        public void ThrowsWhenAValueIsCutOffByTheEndOfTheFile()
        {
            using var file = new TemporaryFile([0x34]);
            using var fs = file.OpenRead();

            Action readUShort = () => BMXFile.ReadUShort(fs);
            Action readByte = () => BMXFile.ReadByte(fs);

            readUShort.Should().Throw<EndOfStreamException>();
            readByte.Should().Throw<EndOfStreamException>();
        }

        [Test]
        public void ReadsZeroTerminatedStringsOneBytePerCharacter()
        {
            using var file = new TemporaryFile([(byte)'P', (byte)'e', 0xE9, 0, 0, (byte)'x']);
            using var fs = file.OpenRead();

            BMXFile.ReadString(fs).Should().Be("Pe\u00e9");
            BMXFile.ReadString(fs).Should().BeEmpty();
            BMXFile.ReadString(fs).Should().BeNull("the last string is cut off by the end of the file");
        }

        private sealed class TemporaryFile : IDisposable
        {
            private readonly string path = Path.GetTempFileName();

            public TemporaryFile(byte[] contents)
            {
                File.WriteAllBytes(path, contents);
            }

            public FileStream OpenRead() => new FileStream(path, FileMode.Open, FileAccess.Read);

            public void Dispose() => File.Delete(path);
        }
    }
}
