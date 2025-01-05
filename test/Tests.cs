using NUnit.Framework;
using Genelib;

namespace Genelib.Test {
    [TestFixture]
    public class Tests
    {
        [Test]
        public void SexDetermination_ParseXY()
        {
            SexDetermination result = SexDeterminationExtensions.Parse("xy");
            Assert.AreEqual(SexDetermination.XY, result);
        }
    }
}
