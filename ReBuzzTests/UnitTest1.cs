using AtmaFileSystem;
using ReBuzz.Core;

namespace ReBuzzTests
{
  public class Tests
  {
    [Test]
    [Apartment(ApartmentState.STA)]
    public void Test1()
    {
      var reBuzzCore = new ReBuzzCore();
      Assert.Pass();
    }
  }
}
