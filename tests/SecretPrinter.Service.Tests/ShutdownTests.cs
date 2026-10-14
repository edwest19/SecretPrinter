// -----------------------------------------------------------------------------
// ShutdownTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-14, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks REQ-LIF-009: when Windows shuts down or restarts, the service stops
//   its work the way it does for a stop request, and does not ask Windows for
//   more time while doing so.
//
// Why these exist:
//   On 2026-10-14 Windows restarted FIOS-STB-01 while the service ran. The
//   service logged that it was stopping and nothing after it, and Windows
//   recorded an application error in SecretPrinter.Service.exe with the code
//   of an exception from the .NET runtime. Read in the source of
//   System.ServiceProcess.ServiceController (tags v10.0.0 and v10.0.12 of
//   dotnet/runtime, the same file): for a stop request ServiceBase marks the
//   service stop-pending before calling OnStop, but for a shutdown it calls
//   OnShutdown with the service still marked running; RequestAdditionalTime
//   throws InvalidOperationException unless the service is marked pending;
//   and ServiceBase throws what OnShutdown threw again on a thread-pool
//   thread, which ends the process. OnShutdown called OnStop, which asked for
//   more time before it stopped anything. See
//   docs/findings/2026-10-14-a-windows-restart-ended-the-service-before-it-stopped.md.
//
// How:
//   The first test builds the real WindowsService, starts it so that its work
//   waits at startup for a printer-side adapter no machine has, and calls
//   OnShutdown by reflection, as ServiceBase does: with the service not marked
//   stop-pending. A service built outside the control manager is in no state
//   at all, which RequestAdditionalTime refuses just as it refuses the running
//   state. On Linux ServiceBase cannot even be constructed, so that test runs
//   on Windows only and is skipped elsewhere.
//
//   The other two read the compiled service: every method reachable from
//   OnShutdown and from OnStop through calls into this assembly, and every
//   method outside it that those call. A call written against a base class's
//   virtual method is followed into every override of it in the assembly too,
//   because that is where it may land: `OnShutdown() => OnStop()` compiles to
//   a call naming ServiceBase.OnStop, not WindowsService.OnStop. They run on
//   any machine.
//
// What these tests do NOT prove:
//   That Windows waits long enough for the stop to finish at a real shutdown.
//   How long it waits is set on each machine by WaitToKillServiceTimeout
//   (5000 milliseconds on FIOS-STB-01, read 2026-10-14). Only a restart of a
//   machine running the service shows that.
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using SecretPrinter.Configuration;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class ShutdownTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>A printer-side adapter name that no machine has, so the work waits at startup.</summary>
    private const string AbsentPrinterAdapter = "SecretPrinter test adapter that no machine has";

    // ---- Under ServiceBase, on Windows -----------------------------------------

    [TestCase("A Windows shutdown stops the work, without asking Windows for more time")]
    [Requirement("REQ-LIF-009")]
    public static void A_Windows_shutdown_stops_the_work_without_asking_for_more_time()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("ServiceBase can be constructed only on Windows; the two tests after this one run anywhere.");
            return;
        }

        // The work waits rather than fails: an adapter name the machine does not
        // have is reported by MdnsInterfaceResolver.ResolveByName with an
        // MdnsInterfaceException, which StartupWait.Examine treats as a reason
        // to wait. Were the work to fail instead, the service's failure handler
        // would end this test process (WindowsService.EndTheProcess).
        var log = new CollectingServiceLog();
        using var service = new WindowsService(LoadTestConfiguration(), log);

        try
        {
            typeof(WindowsService).GetMethod("OnStart", Instance)!.Invoke(service, [Array.Empty<string>()]);

            Assert.True(
                WaitForEntry(log, "Waiting for the printer-side interface", TimeSpan.FromSeconds(10)),
                "the work must be under way, waiting at startup, before the shutdown arrives");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                typeof(WindowsService).GetMethod("OnShutdown", Instance)!.Invoke(service, null);
            }
            catch (TargetInvocationException thrown) when (thrown.InnerException is { } cause)
            {
                // What ServiceBase would throw again on a thread-pool thread,
                // ending the process.
                throw new AssertionException(
                    $"OnShutdown threw {cause.GetType().FullName}: {cause.Message} "
                    + "ServiceBase calls OnShutdown with the service not marked stop-pending, and throws "
                    + "what it throws again where nothing catches it, so the process ends before the work stops.");
            }

            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"OnShutdown took {stopwatch.Elapsed.TotalSeconds:0.##}s; Windows may wait no longer than "
                + "WaitToKillServiceTimeout, 5 s where this was measured");
            Assert.True(
                log.Entries.Any(e => e.Level == LogLevel.Information
                                     && e.Message.Contains("Windows is shutting down", StringComparison.Ordinal)),
                "the log must say that the stop came from a Windows shutdown, not from a stop request");
            Assert.True(
                log.Entries.Any(e => e.Message.StartsWith("Stopped while waiting at startup.", StringComparison.Ordinal)),
                "the work must have stopped, as it does for a stop request");
            Assert.False(
                log.Entries.Any(e => e.Message.StartsWith("Stopped without completing shutdown cleanly", StringComparison.Ordinal)),
                "the stop must have finished within ServiceLifecycle's own time");
            Assert.False(
                log.Entries.Any(e => e.Level == LogLevel.Error),
                "and nothing may have failed. (The startup wait's own line is a warning, so warnings in general are not "
                + "a sign of anything here.)");
        }
        finally
        {
            // If OnShutdown failed, the work is still waiting. Stop it here so
            // it does not run on beside the tests that follow; OnStop cannot be
            // used for that, because outside the control manager it refuses in
            // the same way.
            if (typeof(WindowsService).GetField("_lifecycle", Instance)?.GetValue(service) is ServiceLifecycle lifecycle)
            {
                lifecycle.Stop();
            }
        }
    }

    // ---- In the compiled service, on any machine --------------------------------

    [TestCase("OnShutdown never reaches RequestAdditionalTime, and does reach the lifecycle's stop")]
    [Requirement("REQ-LIF-009")]
    public static void OnShutdown_never_asks_for_more_time_and_does_stop_the_work()
    {
        Reach reach = ReachFrom("OnShutdown");

        Assert.False(reach.Calls("System.ServiceProcess.ServiceBase", "RequestAdditionalTime"),
            "OnShutdown must not reach ServiceBase.RequestAdditionalTime, directly or through any method of its "
            + "own: ServiceBase calls OnShutdown with the service not marked stop-pending, and the request "
            + "throws then. Reached: " + reach.Describe());
        Assert.True(reach.Defines("ServiceLifecycle", "Stop"),
            "and it must reach ServiceLifecycle.Stop, which cancels the work and waits for it. Reached: "
            + reach.Describe());
    }

    [TestCase("OnStop still asks for more time, where ServiceBase allows it")]
    [Requirement("REQ-LIF-009")]
    public static void OnStop_still_asks_for_more_time()
    {
        Reach reach = ReachFrom("OnStop");

        Assert.True(reach.Calls("System.ServiceProcess.ServiceBase", "RequestAdditionalTime"),
            "OnStop must still ask for more time: for a stop request ServiceBase marks the service "
            + "stop-pending before calling OnStop, so the request is allowed there. Reached: " + reach.Describe());
        Assert.True(reach.Defines("ServiceLifecycle", "Stop"),
            "and it must reach ServiceLifecycle.Stop. Reached: " + reach.Describe());
    }

    // ---- Helpers ---------------------------------------------------------------

    /// <summary>
    /// A configuration that loads: one client adapter and the absent
    /// printer-side adapter, both in a made-up inventory. The addresses are
    /// from RFC 5737; the fingerprint and the UUID belong to no device.
    /// </summary>
    private static ServiceConfiguration LoadTestConfiguration()
    {
        var inventory = new FixedInventory(
            new LocalAdapter("SecretPrinter test client", 7, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("192.0.2.10")]),
            new LocalAdapter(AbsentPrinterAdapter, 8, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("198.51.100.10")]));

        string json = ConfigurationLoader.ExampleJson
            .Replace("\"Ethernet 2\"", "\"SecretPrinter test client\"", StringComparison.Ordinal)
            .Replace("\"Wi-Fi\"", $"\"{AbsentPrinterAdapter}\"", StringComparison.Ordinal)
            .Replace("\"printerCertificateSha256\": \"\"",
                "\"printerCertificateSha256\": \"00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF\"",
                StringComparison.Ordinal)
            .Replace("\"uuid\": \"\"", "\"uuid\": \"b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94\"", StringComparison.Ordinal);

        return ConfigurationLoader.Load(json, inventory);
    }

    private static bool WaitForEntry(CollectingServiceLog log, string startOfMessage, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (log.Entries.Any(e => e.Message.StartsWith(startOfMessage, StringComparison.Ordinal)))
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return false;
    }

    private sealed class FixedInventory(params LocalAdapter[] adapters) : IInterfaceInventory
    {
        public IReadOnlyList<LocalAdapter> Adapters { get; } = adapters;
    }

    /// <summary>
    /// What a method of WindowsService reaches: the methods of the service's
    /// own assembly it calls, followed through every call, and the methods
    /// outside the assembly that any of them calls.
    /// </summary>
    private sealed class Reach(HashSet<string> defined, HashSet<string> called)
    {
        public bool Defines(string typeName, string methodName) => defined.Contains($"{typeName}.{methodName}");

        public bool Calls(string fullTypeName, string methodName) => called.Contains($"{fullTypeName}.{methodName}");

        public string Describe() =>
            string.Join(", ", defined.Order(StringComparer.Ordinal)) + " | "
            + string.Join(", ", called.Order(StringComparer.Ordinal));
    }

    /// <summary>The operand type of every IL opcode, by its value, from the runtime's own table.</summary>
    private static readonly Dictionary<short, OperandType> OperandTypes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value, code => code.OperandType);

    private static Reach ReachFrom(string methodName)
    {
        string directory = Path.GetDirectoryName(typeof(ShutdownTests).Assembly.Location)!;
        string path = Path.Combine(directory, "SecretPrinter.Service.dll");

        if (!File.Exists(path))
        {
            throw new AssertionException(
                "SecretPrinter.Service.dll is not beside the test assembly, so what the service's shutdown "
                + "reaches cannot be read. This test project must reference the service's project.");
        }

        using FileStream stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        MetadataReader metadata = reader.GetMetadataReader();

        MethodDefinitionHandle start = metadata.MethodDefinitions.FirstOrDefault(handle =>
        {
            MethodDefinition method = metadata.GetMethodDefinition(handle);
            TypeDefinition type = metadata.GetTypeDefinition(method.GetDeclaringType());
            return metadata.GetString(type.Name) == nameof(WindowsService)
                   && metadata.GetString(method.Name) == methodName;
        });

        if (start.IsNil)
        {
            throw new AssertionException($"WindowsService.{methodName} was not found in the compiled service.");
        }

        // Every override in the assembly, by name: a virtual call naming a
        // base class's method may land in any of them.
        var overrides = new Dictionary<string, List<MethodDefinitionHandle>>(StringComparer.Ordinal);
        foreach (MethodDefinitionHandle handle in metadata.MethodDefinitions)
        {
            MethodDefinition method = metadata.GetMethodDefinition(handle);
            if ((method.Attributes & MethodAttributes.Virtual) != 0
                && (method.Attributes & MethodAttributes.NewSlot) == 0)
            {
                string name = metadata.GetString(method.Name);
                if (!overrides.TryGetValue(name, out List<MethodDefinitionHandle>? list))
                {
                    overrides[name] = list = [];
                }

                list.Add(handle);
            }
        }

        var defined = new HashSet<string>(StringComparer.Ordinal);
        var called = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<MethodDefinitionHandle>();
        var pending = new Queue<MethodDefinitionHandle>();
        pending.Enqueue(start);

        while (pending.Count > 0)
        {
            MethodDefinitionHandle handle = pending.Dequeue();
            if (!seen.Add(handle))
            {
                continue;
            }

            MethodDefinition method = metadata.GetMethodDefinition(handle);
            TypeDefinition declaring = metadata.GetTypeDefinition(method.GetDeclaringType());
            defined.Add($"{metadata.GetString(declaring.Name)}.{metadata.GetString(method.Name)}");

            if (method.RelativeVirtualAddress == 0)
            {
                continue; // abstract, or an interface member: no body to read
            }

            byte[] il = reader.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            foreach (EntityHandle target in MethodOperands(il))
            {
                Follow(metadata, target, pending, called, overrides);
            }
        }

        return new Reach(defined, called);
    }

    private static void Follow(
        MetadataReader metadata,
        EntityHandle target,
        Queue<MethodDefinitionHandle> pending,
        HashSet<string> called,
        Dictionary<string, List<MethodDefinitionHandle>> overrides)
    {
        switch (target.Kind)
        {
            case HandleKind.MethodDefinition:
                pending.Enqueue((MethodDefinitionHandle)target);
                break;

            case HandleKind.MethodSpecification:
                Follow(metadata, metadata.GetMethodSpecification((MethodSpecificationHandle)target).Method,
                    pending, called, overrides);
                break;

            case HandleKind.MemberReference:
                MemberReference member = metadata.GetMemberReference((MemberReferenceHandle)target);
                if (member.Parent.Kind == HandleKind.TypeReference)
                {
                    TypeReference parent = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                    called.Add($"{metadata.GetString(parent.Namespace)}.{metadata.GetString(parent.Name)}."
                               + metadata.GetString(member.Name));
                }
                else
                {
                    called.Add("(generic or nested type)." + metadata.GetString(member.Name));
                }

                if (overrides.TryGetValue(metadata.GetString(member.Name), out List<MethodDefinitionHandle>? landing))
                {
                    foreach (MethodDefinitionHandle handle in landing)
                    {
                        pending.Enqueue(handle);
                    }
                }

                break;
        }
    }

    /// <summary>The method tokens in a method body: the operands of call, callvirt, newobj, ldftn and the like.</summary>
    private static IEnumerable<EntityHandle> MethodOperands(byte[] il)
    {
        int offset = 0;
        while (offset < il.Length)
        {
            short value = il[offset] == 0xFE
                ? unchecked((short)(0xFE00 | il[offset + 1]))
                : il[offset];
            offset += il[offset] == 0xFE ? 2 : 1;

            if (!OperandTypes.TryGetValue(value, out OperandType operand))
            {
                throw new AssertionException($"Unknown IL opcode 0x{value:X4} at offset {offset}.");
            }

            if (operand == OperandType.InlineMethod)
            {
                yield return MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
            }

            offset += operand switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, offset)),
                _ => 4,
            };
        }
    }
}
