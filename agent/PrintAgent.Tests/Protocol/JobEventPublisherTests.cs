using FluentAssertions;
using PrintAgent.Printing;
using PrintAgent.Protocol.Events;
using Xunit;

namespace PrintAgent.Tests.Protocol;

public class JobEventPublisherTests
{
    [Fact]
    public async Task Publish_WithNoSubscribers_DoesNothing()
    {
        var pub = new JobEventPublisher();

        await pub.PublishAsync(Guid.NewGuid(), new JobEvent(Guid.NewGuid(), JobStatus.Submitted), default);
    }

    [Fact]
    public async Task Publish_DeliversOnlyToMatchingConnectionSubscriber()
    {
        var pub = new JobEventPublisher();
        var connA = Guid.NewGuid();
        var connB = Guid.NewGuid();
        var receivedByA = new List<JobEvent>();
        var receivedByB = new List<JobEvent>();

        using var subA = pub.Subscribe(connA, (ev, _) => { receivedByA.Add(ev); return Task.CompletedTask; });
        using var subB = pub.Subscribe(connB, (ev, _) => { receivedByB.Add(ev); return Task.CompletedTask; });

        var jobId = Guid.NewGuid();
        await pub.PublishAsync(connA, new JobEvent(jobId, JobStatus.Printing), default);

        receivedByA.Should().HaveCount(1);
        receivedByA[0].JobId.Should().Be(jobId);
        receivedByB.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispose_RemovesSubscriberSoNoFurtherEventsArrive()
    {
        var pub = new JobEventPublisher();
        var conn = Guid.NewGuid();
        var received = new List<JobEvent>();
        var sub = pub.Subscribe(conn, (ev, _) => { received.Add(ev); return Task.CompletedTask; });

        sub.Dispose();
        await pub.PublishAsync(conn, new JobEvent(Guid.NewGuid(), JobStatus.Completed), default);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Subscribe_TwiceForSameConnection_OverwritesPreviousSubscriber()
    {
        var pub = new JobEventPublisher();
        var conn = Guid.NewGuid();
        var first = new List<JobEvent>();
        var second = new List<JobEvent>();

        var s1 = pub.Subscribe(conn, (ev, _) => { first.Add(ev); return Task.CompletedTask; });
        var s2 = pub.Subscribe(conn, (ev, _) => { second.Add(ev); return Task.CompletedTask; });

        await pub.PublishAsync(conn, new JobEvent(Guid.NewGuid(), JobStatus.Printing), default);

        first.Should().BeEmpty();
        second.Should().HaveCount(1);

        s1.Dispose();
        s2.Dispose();
    }

    [Fact]
    public async Task ConcurrentSubscribeUnsubscribe_DoesNotCorruptState()
    {
        var pub = new JobEventPublisher();
        var connIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(connIds.Select(id => Task.Run(() =>
        {
            using var sub = pub.Subscribe(id, (_, _) => Task.CompletedTask);
            return pub.PublishAsync(id, new JobEvent(Guid.NewGuid(), JobStatus.Submitted), default);
        })));
    }
}
