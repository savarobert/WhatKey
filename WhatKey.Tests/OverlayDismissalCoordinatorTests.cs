using System.Diagnostics;
using WhatKey.Services;
using Xunit;

namespace WhatKey.Tests;

public sealed class OverlayDismissalCoordinatorTests
{
    [Fact]
    public async Task FiftyRapidOverlayRequestsUseOnlyTheLatestDismissalTimer()
    {
        using var coordinator = new OverlayDismissalCoordinator();
        var hideCount = 0;
        var stopwatch = Stopwatch.StartNew();
        var requests = new List<Task>();

        for (var i = 0; i < 50; i++)
        {
            requests.Add(coordinator.RunAsync(
                TimeSpan.FromMilliseconds(100),
                () =>
                {
                    Interlocked.Increment(ref hideCount);
                    return Task.CompletedTask;
                }));
        }

        await Task.WhenAll(requests);

        Assert.Equal(1, hideCount);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }
}
