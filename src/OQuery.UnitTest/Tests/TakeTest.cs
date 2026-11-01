namespace OQuery.Test.Tests
{
    [TestFixture]
    public class TakeTest : TestBase
    {
        [Test]
        public void Simple()
        {
            var data = _db.Query("user")
                .Take(3);
            Assert.AreEqual(data.Count(), 3);
        }

        [Test]
        public void Projection()
        {
            var data = _db.Query("user")
                .Select("Id", "Age")
                .Take(4);

            Assert.AreEqual(data.Count(), 4);
            Assert.AreEqual(data[0].Count(), 2);
        }

        [Test]
        public void Filter()
        {
            var data = _db.Query("user")
                .Where(e => e["Age"] > 30)
                .Select("Id", "Age")
                .Take(2);

            Assert.AreEqual(data.Count(), 2);
            Assert.AreEqual(data[0].Count(), 2);
        }
    }
}
