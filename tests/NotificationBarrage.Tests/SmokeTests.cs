using NotificationBarrage;

namespace NotificationBarrage.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void AppAssemblyHasExpectedName()
    {
        Assert.Equal("NotificationBarrage", typeof(App).Assembly.GetName().Name);
    }
}
