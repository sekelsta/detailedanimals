using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

using Genelib.Extensions;

namespace Genelib {
    public class AnimalDatabase {
        public static string GetPath() {
            ICoreServerAPI api = GenelibSystem.ServerAPI;
            string folder = api.GetOrCreateDataPath(Path.Combine("ModData", api.World.SavegameIdentifier));
            return Path.Combine(folder, "detailedanimals.db");
        }

        protected static void CreateOrOverwriteFile() {
            // "animals" = file type identifier
            // "\0" = format version byte
            // "\0\0\0\0" = int number of animals recorded (none)
            File.WriteAllBytes(GetPath(), Encoding.ASCII.GetBytes("animals\0\0\0\0\0"));
        }

        protected static int ReadEntryCount(Stream file) {
            Span<byte> intb = stackalloc byte[4];
            file.Seek(8, SeekOrigin.Begin);
            int bytesRead = file.Read(intb);
            if (bytesRead != 4) {
                GenelibSystem.ServerAPI.Logger.Warning("detailedanimals database expected 4 bytes, read " + bytesRead);
            }
            return BinaryPrimitives.ReadInt32LittleEndian(intb);
        }

        public static int FindInHeader(Stream file, int entryCount, long id, ref int insertAt) {
            if (entryCount == 0) {
                insertAt = 0;
                return -1;
            }
            Span<byte> longb = stackalloc byte[8];
            // Inclusive lower bound, exclusive upper bound
            int lowerBound = 0;
            int upperBound = entryCount;
            int location = -1;
            // Binary search entries sorted by increasing entity ID
            while (location < 0 && upperBound > lowerBound) {
                int entryNum = (lowerBound + upperBound) / 2;
                int headerOffset = 12; // 7 bytes magic numbers, 1 byte format version, 4 bytes entry count
                // Each entry contains a long (8 bytes) and an int (4 bytes) for a total of 12 bytes
                file.Seek(headerOffset + 12 * entryNum, SeekOrigin.Begin);
                int bytesRead = file.Read(longb);
                if (bytesRead != 8) {
                    GenelibSystem.ServerAPI.Logger.Warning("detailedanimals database expected 8 bytes, read " + bytesRead);
                }
                long currentEntry = BinaryPrimitives.ReadInt64LittleEndian(longb);
                if (currentEntry == id) {
                    location = entryNum;
                    break;
                }
                else if (currentEntry < id) {
                    lowerBound = entryNum + 1;
                }
                else {
                    upperBound = entryNum;
                }
            }
            insertAt = lowerBound;
            return location;
        }

        public static int FindInHeader(Stream file, int entryCount, long id) {
            int insertAt = 0;
            return FindInHeader(file, entryCount, id, ref insertAt);
        }

        public static void Record(Entity entity) {/*
            // TODO: How do I pick a size?
            using (MemoryStream stream = new MemoryStream(1024)) {
                using (BinaryWriter writer = new BinaryWriter()) {
                    Record(entity.UniqueID(), entity.ToBytes(writer, false));
                }
            }*/
        }

        public static void RecordBytes(long id, byte[] bytes) {
            string path = GetPath();
            if (!File.Exists(path)) {
                CreateOrOverwriteFile();
            }
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite)) {
                int entryCount = ReadEntryCount(file);
                int insertAt = 0;
                int location = FindInHeader(file, entryCount, id, ref insertAt);
                if (location < 0) {
                    // TODO: Add header entry (entity id and file location) at insertAt and increment count
                }

                // TODO: Add entity data to body
            }
        }

        public static byte[] GetBytes(long id) {
            string path = GetPath();
            if (!File.Exists(path)) {
                return null;
            }
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                int entryCount = ReadEntryCount(file);
                int location = FindInHeader(file, entryCount, id);
                if (location < 0) {
                    return null;
                }

                // TODO: Read and return entity data
                return null;
            }
        }
    }
}
