using Gallop.Endpoints;
using MessagePack;
using UmamusumeResponseAnalyzer;
using UmamusumeResponseAnalyzer.Plugin;
using UmamusumeResponseAnalyzer.TerminalGui;

static class SourceDispatchSmoke
{
    public static void Run(WorkspaceSmokeSession ui)
    {
        var plugin = new RamenScenarioAnalyzer.RamenScenarioAnalyzer();
        using var context = new RuntimePluginContext(ui.Application);
        ui.Bootstrap.SwitchTo();
        var before = ui.CaptureScreen();
        var calls = 0;
        try
        {
            plugin.Initialize(context);
            Require(context.AnalyzerRegistry.Registrations.Count == 4, "Registration count changed.");
            Require(ReferenceEquals(Workspace.Current, ui.Bootstrap) && before == ui.CaptureScreen(), "Initialize changed the visible workspace or framebuffer.");
            foreach (var registration in PluginManager.CreateAttributeRegistrations(plugin))
            {
                var endpoint = GameEndpointCatalog.ByEndpointType[registration.EndpointType];
                registration.Handler(CreateContext(endpoint, registration.Kind)).GetAwaiter().GetResult();
                calls++;
            }
            foreach (var registration in context.AnalyzerRegistry.Registrations.OrderBy(value => value.Priority))
            foreach (var endpoint in PluginManager.ExpandEndpointPatterns(registration.Patterns))
            {
                registration.Invoke(CreateContext(endpoint, registration.Kind)).GetAwaiter().GetResult();
                calls++;
            }
            Require(Workspace.Current?.Title == "RamenScenarioAnalyzer", "Analyzers did not focus their workspace.");
            Require(ui.CaptureScreen(200, 80).Contains("总属性: 2500, Pt: 1200", StringComparison.Ordinal), "Registered analyzer output was not visible.");
        }
        finally { plugin.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        var workspace = Workspace.Create("RamenScenarioAnalyzer");
        workspace.SwitchTo();
        Require(ReferenceEquals(Workspace.Create("RamenScenarioAnalyzer".ToUpperInvariant()), workspace), "Dispose removed the shared workspace generation.");
        Require(!ui.CaptureScreen(200, 80).Contains("总属性: 2500, Pt: 1200", StringComparison.Ordinal), "Dispose retained the plugin panel.");
        ui.Bootstrap.SwitchTo();
        Console.WriteLine($"PASS source analyzer dispatch: {calls} endpoint invocations");
    }

    static AnalyzerDispatchContext CreateContext(GameEndpointDescriptor endpoint, AnalyzerKind kind)
    {
        var type = kind == AnalyzerKind.Request ? endpoint.RequestType : endpoint.ResponseType;
        var dto = SourceDtoFactory.Create(type, $"programmatic:{kind}:{endpoint.Path}");
        return new(endpoint, MessagePackSerializer.Serialize(type, dto), new("sid-1", "1.2.3", "2026070301", "123456789", "android", "phone"));
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
