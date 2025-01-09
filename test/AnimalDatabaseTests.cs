using System;
using System.IO;
using System.Buffers.Binary;
using NUnit.Framework;
using Genelib;

namespace Genelib.Test {
    [TestFixture]
    public class AnimalDatabaseTests
    {
        [Test]
        public void FindInHeader_IdExists_ReturnsCorrectLocation()
        {
            // Arrange
            long[] ids = new long[] { 1001, 1003, 1005 };
            MemoryStream stream = CreateHeaderStream(ids);
            long idToFind = 1003;
            int insertAt = -1;

            // Act
            int result = AnimalDatabase.FindInHeader(stream, ids.Length, idToFind, ref insertAt);

            // Assert
            Assert.AreEqual(1, result);
            // Ignore insertAt since entry is already present
        }

        [Test]
        public void FindInHeader_IdExists_SingleEntry()
        {
            // Arrange
            long[] ids = new long[] { 1001 };
            MemoryStream stream = CreateHeaderStream(ids);
            long idToFind = 1001;
            int insertAt = -1;

            // Act
            int result = AnimalDatabase.FindInHeader(stream, ids.Length, idToFind, ref insertAt);

            // Assert
            Assert.AreEqual(0, result);
        }
        [Test]
        public void FindInHeader_IdExists_TwoEntries()
        {
            // Arrange
            long[] ids = new long[] { 1001, 1003 };
            MemoryStream stream = CreateHeaderStream(ids);
            long idToFind = 1003;
            int insertAt = -1;

            // Act
            int result = AnimalDatabase.FindInHeader(stream, ids.Length, idToFind, ref insertAt);

            // Assert
            Assert.AreEqual(1, result);
        }

        [Test]
        public void FindInHeader_IdDoesNotExist_ReturnsMinusOne_AndCorrectInsertAt()
        {
            // Arrange
            long[] ids = new long[] { 1001, 1003, 1005 };
            MemoryStream stream = CreateHeaderStream(ids);
            long idToFind = 1002;
            int insertAt = -1;

            // Act
            int result = AnimalDatabase.FindInHeader(stream, ids.Length, idToFind, ref insertAt);

            // Assert
            Assert.AreEqual(-1, result); // ID not found
            Assert.AreEqual(1, insertAt); // Should be inserted at index 1
        }

        [Test]
        public void FindInHeader_EmptyHeader_ReturnsMinusOne_AndInsertAtZero()
        {
            // Arrange
            MemoryStream stream = new MemoryStream();
            int entryCount = 0;
            long idToFind = 1001;
            int insertAt = -1;

            // Act
            int result = AnimalDatabase.FindInHeader(stream, entryCount, idToFind, ref insertAt);

            // Assert
            Assert.AreEqual(-1, result); // ID not found
            Assert.AreEqual(0, insertAt); // Should be inserted at index 0
        }

        [Test]
        public void FindInHeader_IdLargerThanAllEntries_ReturnsMinusOne_AndInsertAtEnd()
        {
            // Arrange
            long[] ids = new long[] { 1001, 1003, 1005 };
            MemoryStream stream = CreateHeaderStream(ids);
            long idToFind = 1006;
            int insertAt = -1;

            // Act
            int result = AnimalDatabase.FindInHeader(stream, ids.Length, idToFind, ref insertAt);

            // Assert
            Assert.AreEqual(-1, result); // ID not found
            Assert.AreEqual(3, insertAt); // Should be inserted at the end
        }

        [Test]
        public void FindInHeader_IdSmallerThanAllEntries_ReturnsMinusOne_AndInsertAtZero()
        {
            // Arrange
            long[] ids = new long[] { 1001, 1003, 1005 };
            MemoryStream stream = CreateHeaderStream(ids);
            long idToFind = 1000;
            int insertAt = -1;

            // Act
            int result = AnimalDatabase.FindInHeader(stream, ids.Length, idToFind, ref insertAt);

            // Assert
            Assert.AreEqual(-1, result); // ID not found
            Assert.AreEqual(0, insertAt); // Should be inserted at index 0
        }

        // Helper method to create a header stream with given IDs
        private MemoryStream CreateHeaderStream(long[] ids)
        {
            MemoryStream stream = new MemoryStream();

            // Write header (magic numbers, version, entry count)
            stream.Write(new byte[] { 0x42, 0x61, 0x64, 0x44, 0x61, 0x74, 0x61, 0x01 }); // 7 bytes magic + 1 byte version
            Span<byte> intb = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(intb, ids.Length);
            stream.Write(intb); // 4 bytes entry count

            // Write each entry (ID + int placeholder)
            foreach (var id in ids)
            {
                byte[] idBytes = new byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(idBytes, id);
                stream.Write(idBytes);
                stream.Write(new byte[4]); // Placeholder for the int part of the entry
            }

            // Reset the stream position to the beginning for reading
            stream.Position = 0;
            return stream;
        }
    }
}
