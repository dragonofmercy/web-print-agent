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
    public async Task Publish_SubscriberThrows_DoesNotPropagate()
    {
        var pub = new JobEventPublisher();
        var conn = Guid.NewGuid();
        using var sub = pub.Subscribe(conn, (_, _) => throw new InvalidOperationException("subscriber boom"));

        var act = () => pub.PublishAsync(conn, new JobEvent(Guid.NewGuid(), JobStatus.Submitted), default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Publish_SubscriberCancelsViaToken_PropagatesOperationCanceled()
    {
        var pub = new JobEventPublisher();
        var conn = Guid.NewGuid();
        using var sub = pub.Subscribe(conn, (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => pub.PublishAsync(conn, new JobEvent(Guid.NewGuid(), JobStatus.Submitted), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConcurrentSubscribers_AllReceiveTheirOwnEventsExactlyOnce()
    {
        var pub = new JobEventPublisher();
        const int N = 100;
        var connIds = Enumerable.Range(0, N).Select(_ => Guid.NewGuid()).ToArray();
        var counts = new int[N];
        var subs = new IDisposable[N];

        await Task.WhenAll(connIds.Select((id, i) => Task.Run(() =>
        {
            subs[i] = pub.Subscribe(id, (_, _) =>
            {
                Interlocked.Increment(ref counts[i]);
                return Task.CompletedTask;
            });
        })));

        await Task.WhenAll(connIds.Select(id =>
            pub.PublishAsync(id, new JobEvent(Guid.NewGuid(), JobStatus.Submitted), default)));

        counts.Should().AllSatisfy(c => c.Should().Be(1));
        foreach (var s in subs) s.Dispose();
    }

    [Fact]
    public async Task DisposeOfStaleSubscription_DoesNotEvictNewerOneOnSameConnection()
    {
        var pub = new JobEventPublisher();
        var conn = Guid.NewGuid();
        var newReceived = 0;

        var stale = pub.Subscribe(conn, (_, _) => Task.CompletedTask);
        using var fresh = pub.Subscribe(conn, (_, _) =>
        {
            Interlocked.Increment(ref newReceived);
            return Task.CompletedTask;
        });

        stale.Dispose();

        await pub.PublishAsync(conn, new JobEvent(Guid.NewGuid(), JobStatus.Submitted), default);
        newReceived.Should().Be(1);
    }
}
