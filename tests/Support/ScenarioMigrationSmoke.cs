using UmamusumeResponseAnalyzer.TerminalGui;

static class ScenarioMigrationSmoke
{
    public static void Run(WorkspaceSmokeSession ui)
    {
        using var directory = new TempCurrentDirectory("RamenScenarioAnalyzer-unused");
        using var context = new RuntimePluginContext(ui.Application);
        ui.Bootstrap.SwitchTo();
        var unused = new RamenScenarioAnalyzer.RamenScenarioAnalyzer();
        try
        {
            unused.Initialize(context);
            ui.Flush();
            if (!ReferenceEquals(Workspace.Current, ui.Bootstrap))
                throw new InvalidOperationException("Initialize changed Workspace.Current.");
        }
        finally { unused.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        ui.Flush();
        if (!ReferenceEquals(Workspace.Current, ui.Bootstrap))
            throw new InvalidOperationException("Unused Dispose changed Workspace.Current.");
        Console.WriteLine("PASS RamenScenarioAnalyzer: unused workspace lifecycle");
    }
}
