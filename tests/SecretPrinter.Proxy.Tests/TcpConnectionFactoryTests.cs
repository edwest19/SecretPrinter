// -----------------------------------------------------------------------------
// TcpConnectionFactoryTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-21, for the SecretPrinter project. Reviewed by a human before
// merge.
//
// Purpose:
//   Verifies that a printer which never accepts a TCP connection produces a
//   reported failure, not a job that ends with nothing logged
//   (docs/findings/2026-09-17-connect-timeout-is-not-reported.md).
//
//   TcpConnectionFactory bounds its connect with a deadline. When that deadline
//   runs out, the connect throws a cancellation; IppRelay reports failures and
//   deliberately does not report cancellations. So the factory must turn its OWN
//   deadline into a TimeoutException - and must leave a cancellation the CALLER
//   asked for alone, because that is the service stopping or the printer being
//   withdrawn, not a failed job.
//
// How these tests reach the deadline without a network:
//   They use the factory's internal constructor, which replaces only the socket
//   connect with one that waits on its token and never completes. The deadline,
//   the linked token source, the translation and the disposal are the code the
//   service runs. No packet is sent, and nothing depends on how any network,
//   router or CI runner treats an unanswered SYN.
//
// What these tests cannot do, said plainly:
//   They do not show that a real unanswered SYN reaches this code as a
//   cancellation. That rests on Microsoft's documentation for
//   TcpClient.ConnectAsync(IPEndPoint, CancellationToken), and on the log shape
//   seen on FIOS-STB-01 on 2026-09-21, which the finding describes.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Proxy.Tests;

internal static class TcpConnectionFactoryTests
{
    private static readonly IPEndPoint PrinterEndpoint =
        new(IPAddress.Parse("192.168.12.180"), 631);

    private static readonly IPEndPoint ClientEndpoint =
        new(IPAddress.Parse("192.168.1.41"), 49152);

    /// <summary>
    /// Short enough to keep the suite fast; the tests never depend on it being
    /// exact, only on it running out long before <see cref="Patience"/>.
    /// </summary>
    private static readonly TimeSpan ShortDeadline = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A connect that never completes on its own: a printer whose network drops
    /// the SYN. It ends only when its token is cancelled, and then throws the
    /// same kind of exception the real connect does.
    /// </summary>
    private static readonly Func<TcpClient, IPEndPoint, CancellationToken, ValueTask> NeverAnswers =
        static (_, _, token) => new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, token));

    // ---- The factory ----------------------------------------------------------

    [TestCase("A connect that runs out of time fails as a timeout, not a cancellation")]
    [Requirement("REQ-PXY-007")]
    public static void Deadline_fails_as_a_timeout()
    {
        var factory = new TcpConnectionFactory(NeverAnswers);

        Task<IDuplexConnection> connecting =
            factory.ConnectAsync(PrinterEndpoint, ShortDeadline, CancellationToken.None);

        Exception failure = Failure(connecting);

        Assert.True(failure is TimeoutException,
            "a connect that runs out of time must fail as a TimeoutException, not a cancellation: IppRelay "
            + $"does not report cancellations as failed jobs. Got {failure.GetType().Name}: {failure.Message}");

        Assert.True(failure.InnerException is OperationCanceledException,
            "the deadline that fired is kept as the inner exception, so nothing about the failure is lost");

        Assert.True(failure.Message.Contains(PrinterEndpoint.ToString(), StringComparison.Ordinal),
            $"the reason must name where the connection was going. Got: {failure.Message}");
    }

    [TestCase("A cancellation the caller asked for stays a cancellation")]
    public static void Caller_cancellation_is_not_a_timeout()
    {
        // The other half of the distinction. When the service stops, or the
        // printer is withdrawn, the caller cancels - and reporting every
        // in-flight connect as a printer that failed to answer would put a
        // false claim about the printer into the log.
        var factory = new TcpConnectionFactory(NeverAnswers);
        using var caller = new CancellationTokenSource();

        Task<IDuplexConnection> connecting =
            factory.ConnectAsync(PrinterEndpoint, TimeSpan.FromMinutes(5), caller.Token);

        caller.Cancel();

        Exception failure = Failure(connecting);

        Assert.True(failure is OperationCanceledException,
            "a cancellation the caller asked for must stay a cancellation, not become a claim that the printer "
            + $"did not answer. Got {failure.GetType().Name}: {failure.Message}");
    }

    // ---- Through the relay ----------------------------------------------------

    [TestCase("A printer that never accepts the connection produces a reported, failed job")]
    [Requirement("REQ-PXY-007")]
    public static void Unanswered_connect_is_reported_by_the_relay()
    {
        // The case seen on FIOS-STB-01 on 2026-09-21, end to end with the real
        // factory: accepted, located, and then - before the fix - nothing at
        // all, because the cancellation left RelayOneAsync unreported.
        var observer = new RecordingObserver();
        var client = new FakeConnection(StreamPair.Create().Left, ClientEndpoint);

        var relay = new IppRelay(
            new NeverAcceptsListener(),
            new TcpConnectionFactory(NeverAnswers),
            _ => Task.FromResult(PrinterEndpoint),
            new RelayOptions { ConnectTimeout = ShortDeadline },
            observer);

        Task<RelayOutcome> relaying = relay.RelayOneAsync(client, CancellationToken.None);

        RelayOutcome outcome;
        try
        {
            if (!relaying.Wait(Patience))
            {
                throw new AssertionException(
                    "RelayOneAsync neither returned nor failed within the test's patience; it is hung.");
            }

            outcome = relaying.Result;
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            throw new AssertionException(
                "RelayOneAsync ended in a cancellation nobody asked for, so the job ended with nothing "
                + "reported. This is the defect in docs/findings/2026-09-17-connect-timeout-is-not-reported.md. "
                + "Events: " + string.Join(" | ", observer.Events));
        }

        Assert.False(outcome.Succeeded, "a printer that never accepted the connection printed nothing");

        Assert.Equal(1, observer.Events.Count(e => e.StartsWith("failed ", StringComparison.Ordinal)),
            "exactly one failure must be reported for the job. Events: " + string.Join(" | ", observer.Events));

        Assert.True(observer.Events.Any(e => e.Contains("was established within", StringComparison.Ordinal)),
            "the reported reason must say the connection was not established in time. Events: "
            + string.Join(" | ", observer.Events));

        Assert.False(observer.Events.Any(e => e.StartsWith("started ", StringComparison.Ordinal)),
            "nothing was relayed, so no relay may be reported as started");
    }

    // ---- Helpers --------------------------------------------------------------

    private static Exception Failure(Task<IDuplexConnection> connecting)
    {
        try
        {
            if (!connecting.Wait(Patience))
            {
                throw new AssertionException(
                    "ConnectAsync neither returned nor failed within the test's patience; it is hung.");
            }
        }
        catch (AggregateException ex) when (ex.InnerException is not null)
        {
            return ex.InnerException;
        }

        throw new AssertionException(
            "ConnectAsync returned a connection from a connect that never completed.");
    }
}
