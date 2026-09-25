// -----------------------------------------------------------------------------
// TestHarnessTests.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, for the SecretPrinter project, 2026-09-25. Reviewed by a human
// before merge.
//
// Purpose:
//   Holds the test harness to one thing: a test that fails is counted as
//   failed, whatever kind of method it is.
//
//   Until 2026-09-25 that was not so. TestHarness called each test and never
//   looked at what it returned, and an async test returns a Task that carries
//   its failure. So every async test in this repository was counted as passed
//   however it ended, fourteen of them from 2026-09-20, and one was in fact
//   failing. See
//   docs/findings/2026-09-25-the-test-harness-never-waited-for-an-async-test.md.
//
//   These tests are ordinary synchronous methods, so they would have been able
//   to fail under the old harness too. Each hands TestHarness.Execute a probe:
//   a small method below with no [TestCase] attribute, so the harness never
//   runs it as a test of its own. The probes fail or skip on purpose; the
//   tests check what Execute makes of that.
//
//   Registered first in Program.cs, so that a harness unable to fail an async
//   test is caught before any async test after it is believed.
// -----------------------------------------------------------------------------

using System.Reflection;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class TestHarnessTests
{
    private static MethodInfo Probe(string name) =>
        typeof(TestHarnessTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"No probe named {name}.");

    // ---- Probes: never run as tests of their own ------------------------------

    private static async Task FailsAfterAwaiting()
    {
        await Task.Delay(10).ConfigureAwait(false);
        Assert.True(false, "probe failing after it awaited");
    }

    private static async Task FailsBeforeAwaiting()
    {
        Assert.True(false, "probe failing before it awaited");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async Task SkipsAfterAwaiting()
    {
        await Task.Delay(10).ConfigureAwait(false);
        Assert.Skip("probe skipping after it awaited");
    }

    private static ValueTask ReturnsSomethingElse() => ValueTask.CompletedTask;

    // ---- Tests ----------------------------------------------------------------

    [TestCase("An async test that fails after it awaits is counted as failed")]
    [Requirement("REQ-DIST-009")]
    public static void Async_failure_after_an_await_is_a_failure()
    {
        TestResult result = TestHarness.Execute(Probe(nameof(FailsAfterAwaiting)));

        Assert.Equal("FAIL", result.Outcome,
            "a failure inside the Task an async test returns must count; it was counted as a pass until 2026-09-25");
        Assert.True(result.Message?.Contains("probe failing after it awaited", StringComparison.Ordinal) == true,
            "the failure reported must be the test's own");
    }

    [TestCase("An async test that fails before it awaits is counted as failed")]
    [Requirement("REQ-DIST-009")]
    public static void Async_failure_before_an_await_is_a_failure()
    {
        TestResult result = TestHarness.Execute(Probe(nameof(FailsBeforeAwaiting)));

        Assert.Equal("FAIL", result.Outcome,
            "an async method stores even an immediate failure in its Task rather than throwing it");
        Assert.True(result.Message?.Contains("probe failing before it awaited", StringComparison.Ordinal) == true,
            "the failure reported must be the test's own");
    }

    [TestCase("An async test that skips is counted as skipped, not passed")]
    [Requirement("REQ-DIST-009")]
    public static void Async_skip_is_a_skip()
    {
        TestResult result = TestHarness.Execute(Probe(nameof(SkipsAfterAwaiting)));

        Assert.Equal("SKIP", result.Outcome, "a skip is not a pass, however the test reports it");
    }

    [TestCase("A test the harness cannot wait for is counted as failed, not passed")]
    [Requirement("REQ-DIST-009")]
    public static void Unwaitable_test_is_a_failure()
    {
        TestResult result = TestHarness.Execute(Probe(nameof(ReturnsSomethingElse)));

        Assert.Equal("FAIL", result.Outcome,
            "a test whose ending the harness cannot see must not be reported as having passed");
    }
}
