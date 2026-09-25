// -----------------------------------------------------------------------------
// TestHarness.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// A test that returns a Task waited for, and its outcome taken from that Task,
// by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-25. Until then no async test could fail. See "Tests that return a
// Task" below, and
// docs/findings/2026-09-25-the-test-harness-never-waited-for-an-async-test.md.
// Reviewed by a human before merge.
//
// Purpose:
//   A very small test runner, so that this repository has automated tests
//   without acquiring a third-party dependency. Shared by every test project.
//
// Why not xUnit or NUnit:
//   REQ-SEC-009 requires every dependency to be the base class library or a
//   project in this repository, and a central claim of the project is that
//   there is no third-party code to audit. A test framework would be the first
//   exception, and exceptions to that rule are worth resisting while the cost
//   of doing without is this low. Roughly a hundred lines buys assertions, test
//   discovery by attribute, and a process exit code - which is all CI needs.
//
//   The cost is real and is not hidden: no `dotnet test` integration, no IDE
//   test explorer, no parallel execution, no fixtures. If those become worth
//   more than the zero-dependency claim, switching is a contained change - the
//   tests themselves are ordinary methods.
//
// Tests are found by reflection over [TestCase] methods and run in declaration
// order. A test fails by throwing; anything else is a pass.
//
// Tests that return a Task:
//   The sentence above was true only of tests that return nothing. An async
//   test returns a Task, and whatever it throws goes into that Task rather
//   than out of the call. Until 2026-09-25 the harness called each test and
//   never looked at what it returned, so an async test was counted as passed
//   however it ended. From 2026-09-20, when the first async tests were added,
//   fourteen tests were reported as passing that could not have failed, and
//   one of them was in fact failing.
//
//   Now a test that returns a Task is waited for, and it fails, or is skipped,
//   exactly as it would by throwing. A test that returns anything else - a
//   ValueTask, for one - is failed rather than passed, because the harness
//   cannot wait for it and will not guess. Execute holds this rule, and
//   TestHarnessTests in SecretPrinter.Service.Tests holds Execute to it.
//
// Results file:
//   The harness can write a plain-text record of which requirement identifiers
//   had a test that actually PASSED. SecretPrinter.SpecCheck reads it.
//
//   This exists because a [Requirement] marker on a test is compiled-in
//   metadata: it is present whether or not the test ever executed. Without this
//   file, a permanently SKIPPED test counts as full coverage in the matrix,
//   which is exactly the kind of quiet overstatement this project must not
//   contain. The format is tab-separated text rather than JSON so that a reader
//   can check it with their eyes.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Reflection;
using SecretPrinter.Spec;

namespace SecretPrinter.TestKit;

/// <summary>Marks a method as a test. Must be public, static, parameterless.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestCaseAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}

/// <summary>
/// Marks a test as requiring a working local network stack, so it can be
/// skipped in environments that forbid it rather than reported as a failure.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresNetworkAttribute : Attribute;

/// <summary>Thrown by a failing assertion.</summary>
public sealed class AssertionException(string message) : Exception(message);

/// <summary>Thrown by a test that cannot run in the current environment.</summary>
public sealed class SkipException(string reason) : Exception(reason);

public static class Assert
{
    public static void True(bool condition, string because)
    {
        if (!condition)
        {
            throw new AssertionException($"Expected true: {because}");
        }
    }

    public static void False(bool condition, string because)
    {
        if (condition)
        {
            throw new AssertionException($"Expected false: {because}");
        }
    }

    public static void Equal<T>(T expected, T actual, string because)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionException($"Expected {expected}, got {actual}: {because}");
        }
    }

    public static void NotNull(object? value, string because)
    {
        if (value is null)
        {
            throw new AssertionException($"Expected non-null: {because}");
        }
    }

    public static void Null(object? value, string because)
    {
        if (value is not null)
        {
            throw new AssertionException($"Expected null, got {value}: {because}");
        }
    }

    /// <summary>Asserts that an action throws a specific exception type, and returns it.</summary>
    public static TException Throws<TException>(Action action, string because)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException expected)
        {
            return expected;
        }
        catch (Exception other)
        {
            throw new AssertionException(
                $"Expected {typeof(TException).Name} but got {other.GetType().Name} ({other.Message}): {because}");
        }

        throw new AssertionException($"Expected {typeof(TException).Name}, but nothing was thrown: {because}");
    }

    public static void Skip(string reason) => throw new SkipException(reason);
}

/// <summary>One test outcome, as written to the results file.</summary>
/// <param name="Outcome">PASS, FAIL or SKIP.</param>
/// <param name="Requirement">Requirement identifier, or "-" when the test carries none.</param>
/// <param name="Test">Fully qualified type and method name.</param>
public sealed record TestOutcome(string Outcome, string Requirement, string Test);

/// <summary>What running one test came to.</summary>
/// <param name="Outcome">PASS, FAIL or SKIP.</param>
/// <param name="Message">Why the test failed or was skipped; null when it passed.</param>
public sealed record TestResult(string Outcome, string? Message);

public static class TestHarness
{
    /// <summary>Runs every [TestCase] in the given types. Returns a process exit code.</summary>
    /// <param name="resultsPath">
    /// Optional path for the results file consumed by SpecCheck. When omitted,
    /// no record is written and SpecCheck cannot tell an executed test from a
    /// skipped one.
    /// </param>
    /// <param name="suites">Types containing [TestCase] methods.</param>
    public static int Run(string? resultsPath, params Type[] suites)
    {
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        var failures = new List<string>();
        var outcomes = new List<TestOutcome>();

        foreach (Type suite in suites)
        {
            Console.WriteLine();
            Console.WriteLine($"-- {suite.Name} {new string('-', Math.Max(0, 66 - suite.Name.Length))}");

            MethodInfo[] tests = [.. suite
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<TestCaseAttribute>() is not null)];

            foreach (MethodInfo test in tests)
            {
                var attribute = test.GetCustomAttribute<TestCaseAttribute>()!;
                string label = attribute.Description;
                string qualified = $"{suite.FullName}.{test.Name}";

                string[] requirements = [.. test
                    .GetCustomAttributes<RequirementAttribute>()
                    .Select(r => r.Id)];

                if (requirements.Length == 0)
                {
                    requirements = ["-"];
                }

                TestResult result = Execute(test);
                string outcome = result.Outcome;

                switch (outcome)
                {
                    case "PASS":
                        passed++;
                        Console.WriteLine($"  PASS  {label}");
                        break;

                    case "SKIP":
                        skipped++;
                        Console.WriteLine($"  SKIP  {label}");
                        Console.WriteLine($"        {result.Message}");
                        break;

                    default:
                        failed++;
                        Console.WriteLine($"  FAIL  {label}");
                        Console.WriteLine($"        {result.Message}");
                        failures.Add($"{suite.Name}.{test.Name}: {result.Message}");
                        break;
                }

                foreach (string requirement in requirements)
                {
                    outcomes.Add(new TestOutcome(outcome, requirement, qualified));
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 70));
        Console.WriteLine($"passed {passed}   failed {failed}   skipped {skipped}");

        if (failures.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Failures:");
            foreach (string failure in failures)
            {
                Console.WriteLine($"  {failure}");
            }
        }

        if (skipped > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Skipped tests did not run and prove nothing. They are not passes.");
        }

        if (resultsPath is not null)
        {
            WriteResults(resultsPath, outcomes);
            Console.WriteLine();
            Console.WriteLine($"Results written to {resultsPath}");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("No results file written. SpecCheck will not be able to tell an executed");
            Console.WriteLine("test from a skipped one. Pass --results <path> to record outcomes.");
        }

        Console.WriteLine(new string('=', 70));
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// Runs one test and says what it came to. A test that returns a Task is
    /// waited for, and the Task's ending is the test's: see "Tests that return
    /// a Task" at the top of this file.
    /// </summary>
    /// <param name="test">A public or private static method with no parameters.</param>
    public static TestResult Execute(MethodInfo test)
    {
        ArgumentNullException.ThrowIfNull(test);

        try
        {
            object? returned = test.Invoke(null, null);

            switch (returned)
            {
                case null:
                    break;

                case Task task:
                    // Throws what the test threw, unwrapped.
                    task.GetAwaiter().GetResult();
                    break;

                default:
                    return new TestResult(
                        "FAIL",
                        $"The test returned {returned.GetType().Name}, which the harness cannot wait for. "
                        + "A test must return nothing or a Task; it is failed rather than counted as passed.");
            }

            return new TestResult("PASS", null);
        }
        catch (Exception ex)
        {
            // Whatever the test threw, or its Task ended with, is its outcome.
            // Invoke wraps what a test throws; waiting on a Task does not.
            Exception cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

            return cause is SkipException skip
                ? new TestResult("SKIP", skip.Message)
                : new TestResult("FAIL", cause.Message);
        }
    }

    /// <summary>
    /// Writes the results file. Tab-separated so it stays readable, with a
    /// header explaining the format to anyone who opens it without context.
    /// </summary>
    /// <summary>
    /// Module version ids of the SecretPrinter assemblies this run exercised.
    /// </summary>
    /// <remarks>
    /// Only assemblies actually loaded are recorded. That is the right set: an
    /// assembly no test touched cannot have been verified by this run, so
    /// claiming a fingerprint for it would overstate what the file describes.
    /// </remarks>
    private static SortedDictionary<string, Guid> LoadedAssemblyIds()
    {
        var ids = new SortedDictionary<string, Guid>(StringComparer.Ordinal);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string? name = assembly.GetName().Name;

            if (name is null || !name.StartsWith("SecretPrinter", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                ids[name] = assembly.ManifestModule.ModuleVersionId;
            }
            catch (NotSupportedException)
            {
                // Dynamic assemblies have no module version id. None of ours is
                // dynamic, but skipping is better than failing a test run over
                // a fingerprint.
            }
        }

        return ids;
    }

    private static void WriteResults(string path, IReadOnlyList<TestOutcome> outcomes)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lines = new List<string>
        {
            "# SecretPrinter test results",
            "#",
            "# Written by tests/SecretPrinter.Mdns.Tests (TestHarness.cs).",
            "# Read by tools/SecretPrinter.SpecCheck via --test-results.",
            "#",
            "# A [Requirement] marker on a test is compiled-in metadata and exists whether",
            "# or not the test ran. This file records what actually EXECUTED, so that a",
            "# skipped test cannot be counted as coverage.",
            "#",
            "# columns: outcome <TAB> requirement <TAB> test",
            "# outcome is PASS, FAIL or SKIP. '-' in the requirement column means the",
            "# test carries no requirement marker.",
            "#",
            "# ASSEMBLY lines record the module version id of each SecretPrinter assembly",
            "# loaded during the run. Builds are deterministic, so the id changes exactly",
            "# when the code does. SpecCheck compares these against the assemblies it",
            "# scans and refuses results produced against different code.",
            "#",
            $"# generated: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture)}",
        };

        // Fingerprints first, so a reader sees what this file describes before
        // seeing what it claims. Enumerated after the tests have run, by which
        // point every assembly they exercise has been loaded.
        foreach ((string name, Guid id) in LoadedAssemblyIds())
        {
            lines.Add($"ASSEMBLY\t{name}\t{id:D}");
        }

        foreach (TestOutcome outcome in outcomes)
        {
            lines.Add($"{outcome.Outcome}\t{outcome.Requirement}\t{outcome.Test}");
        }

        File.WriteAllLines(path, lines);
    }
}
