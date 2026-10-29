using QuestLock.Core.Network;
using Xunit;

namespace QuestLock.Core.Tests;

public class NetworkHelperTests
{
    [Fact]
    public void GetActiveNetworkInfo_DoesNotThrowAndReturnsObject()
    {
        var info = NetworkHelper.GetActiveNetworkInfo();

        Assert.NotNull(info);
        // V beznem testovacim prostredi bud existuje LAN IP, nebo je null (napr. izolacni kontejner), ale nesmi vyhodit vyjimku
        if (info.TailscaleIp != null)
        {
            Assert.True(info.HasTailscale);
        }
    }
}
