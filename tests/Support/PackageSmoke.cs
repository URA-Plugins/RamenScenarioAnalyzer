using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Gallop;
using Gallop.Endpoints;
using MessagePack;
using Newtonsoft.Json;
using Terminal.Gui.App;
using UmamusumeResponseAnalyzer;
using UmamusumeResponseAnalyzer.Entities;
using UmamusumeResponseAnalyzer.Plugin;
using UmamusumeResponseAnalyzer.TerminalGui;
using GameText = UmamusumeResponseAnalyzer.Localization.Game;

static class PackageSmoke
{
    const string InternalName = "RamenScenarioAnalyzer";

    public static void Run(WorkspaceSmokeSession ui)
    {
        var packageRoot = Environment.GetEnvironmentVariable("URA_PLUGIN_SMOKE_PACKAGE_ROOT");
        if (string.IsNullOrWhiteSpace(packageRoot) && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
            packageRoot = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")
                ?? throw new InvalidOperationException("GITHUB_WORKSPACE is required in GitHub Actions."), "artifacts");
        if (string.IsNullOrWhiteSpace(packageRoot) || !Path.IsPathFullyQualified(packageRoot))
            throw new InvalidOperationException("URA_PLUGIN_SMOKE_PACKAGE_ROOT must identify the absolute fresh package directory.");
        var matches = Directory.EnumerateFiles(packageRoot, "*.zip", SearchOption.AllDirectories)
            .Where(path =>
            {
                using var archive = ZipFile.OpenRead(path);
                using var stream = archive.GetEntry("manifest.json")?.Open()
                    ?? throw new InvalidDataException($"Package has no root manifest.json: {path}");
                using var manifest = JsonDocument.Parse(stream);
                return manifest.RootElement.GetProperty("InternalName").GetString() == InternalName;
            }).ToArray();
        Require(matches.Length == 1, $"Expected exactly one fresh {InternalName} ZIP, found {matches.Length}.");
        var packagePath = matches[0];
        using (var archive = ZipFile.OpenRead(packagePath))
        {
            Require(Path.GetFileNameWithoutExtension(packagePath) == InternalName, "ZIP filename/InternalName mismatch.");
            Require(archive.Entries.Count(entry => entry.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)) == 1
                && archive.Entries.Count(entry => entry.FullName.Equals(InternalName + ".dll", StringComparison.OrdinalIgnoreCase)) == 1
                && archive.GetEntry(InternalName + ".dll") is not null, "ZIP root manifest/main DLL mismatch.");
            using var manifestStream = archive.GetEntry("manifest.json")!.Open();
            using var manifest = JsonDocument.Parse(manifestStream);
            string[] expected = ["Author", "InternalName", "DisplayName", "Description", "Changelog", "Version", "Dependencies", "RepositoryUrl", "LastUpdate", "Category", "Homepage"];
            var properties = manifest.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
            Require(properties.Length == expected.Length && properties.ToHashSet(StringComparer.Ordinal).SetEquals(expected), "Manifest schema mismatch.");
            Require(manifest.RootElement.GetProperty("Dependencies").GetArrayLength() == 0, "Unexpected plugin Dependencies.");
        }
        using var directory = new TempCurrentDirectory(InternalName + "-zip");
        InitializeSmokeDatabase().GetAwaiter().GetResult();
        Directory.CreateDirectory("Plugins");
        File.Copy(packagePath, Path.Combine("Plugins", InternalName + ".zip"));

        SourceDispatchSmoke.Run(ui);
        try
        {
            PluginManager.Init();
            PluginManager.InitializeLoadedPlugins();
            PluginManager.TriggerStartedAsync().GetAwaiter().GetResult();
            Require(PluginManager.FailedPlugins.Count == 0 && PluginManager.LoadedPlugins.Count == 1, "ZIP plugin did not initialize.");
            var plugin = PluginManager.LoadedPlugins.Single();
            var assembly = plugin.GetType().Assembly;
            var context = AssemblyLoadContext.GetLoadContext(assembly)!;
            var metadata = PluginManager.Metadatas[InternalName];
            Require(metadata.PackagePath == Path.GetFullPath(Path.Combine("Plugins", InternalName + ".zip")), "ZIP metadata origin mismatch.");
            using var archive = ZipFile.OpenRead(packagePath);
            using var stream = archive.GetEntry(metadata.AssemblyEntry)!.Open();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            bytes.Position = 0;
            using var pe = new PEReader(bytes);
            var reader = pe.GetMetadataReader();
            var mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
            Require(!context.IsCollectible && context != AssemblyLoadContext.Default && assembly.ManifestModule.ModuleVersionId == mvid, "Loaded DLL did not originate from the fresh ZIP.");
            foreach (var shared in new[] { typeof(IPlugin).Assembly, typeof(SingleModeChara).Assembly, typeof(IApplication).Assembly })
                Require(ReferenceEquals(context.LoadFromAssemblyName(shared.GetName()), shared), "Shared Host ABI identity mismatch.");
            var response = PackageRamenFixture.CreateRamenLoadResponse();
            var chara = response.data.single_mode_load_common.chara_info;
            chara.state = 1;
            chara.scenario_id = 14;
            chara.card_id = 100100;
            chara.single_mode_chara_id = 904;
            response.data.ramen_data_set_load = new()
            {
                selected_region_id_array = [1, 2, 3],
                reduce_base_turn_info_array = [new() { feeling_id = 1, reduce_base_turn = 4 }],
                last_tasting_info = new() { region_id = 4 }, check_point_pt = 123, expected_check_point_pt = 456
            };
            Dispatch(typeof(GameApi.SingleModeRamen.Load), response);
            Panel(ui, "URAF", GameText.I18N_Speed, "100", "110");
            var state = assembly.GetType("RamenScenarioAnalyzer.RamenScenarioState", true)!.GetMethod("Snapshot")!.Invoke(null, null)!;
            Require(Equals(Member(state, "loaded"), true) && Equals(Member(state, "single_mode_chara_id"), 904)
                && Equals(Member(state, "check_point_pt"), 123) && ((int[])Member(state, "selected_region_id_array")).SequenceEqual([1, 2, 3]), "Ramen ZIP state mismatch.");
        }
        finally { PluginManager.ShutdownAsync().GetAwaiter().GetResult(); }
        Require(PluginManager.LoadedPlugins.Count == 0 && PluginManager.Contexts.Count == 0
            && PluginManager.RequestAnalyzerMethods.Count == 0 && PluginManager.ResponseAnalyzerMethods.Count == 0, "ZIP shutdown retained instances or registrations.");
        Workspace.Create(InternalName).SwitchTo();
        Require(!ui.CaptureScreen(200, 80).Contains("URAF", StringComparison.Ordinal), "ZIP shutdown retained its panel.");
        ui.Bootstrap.SwitchTo();
        Console.WriteLine($"PASS {InternalName}: fresh ZIP business and shutdown");
    }

    static void Dispatch(Type endpointType, object response)
    {
        var endpoint = GameEndpointCatalog.ByEndpointType[endpointType];
        var registrations = PluginManager.SnapshotAnalyzerRegistrations(AnalyzerKind.Response, endpointType);
        Require(registrations.Length != 0, "No ZIP analyzer matched " + endpoint.Path);
        var context = new AnalyzerDispatchContext(endpoint, MessagePackSerializer.Serialize(response.GetType(), response), new("zip-sid", "zip-app", "zip-res", "zip-viewer", null, null));
        foreach (var registration in registrations)
        {
            using var callback = PluginManager.TryEnterPluginCallback(registration.Plugin);
            Require(callback is not null && !registration.IsFaulted, "ZIP analyzer is unavailable.");
            using var owner = HotkeyManager.RegisterScope(registration.Plugin);
            registration.Handler(context).GetAwaiter().GetResult();
        }
    }

    static void Panel(WorkspaceSmokeSession ui, params string[] values)
    {
        Workspace.Create(InternalName).SwitchTo();
        var screen = ui.CaptureScreen(200, 80);
        foreach (var value in values) Require(screen.Contains(value, StringComparison.Ordinal), "ZIP framebuffer missing: " + value);
    }

    static object Member(object value, string name)
        => value.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(value)
            ?? value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(value)
            ?? throw new InvalidOperationException("Missing runtime member: " + name);

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
static async Task InitializeSmokeDatabase(
    IEnumerable<Story>? events = null,
    IEnumerable<UmamusumeResponseAnalyzer.Entities.SkillData>? skills = null)
{
    WriteBrotliJson(Database.EVENT_NAME_FILEPATH, (events ?? []).ToList());
    WriteBrotliJson(
        Database.NAMES_FILEPATH,
        new List<BaseName>
        {
            new SupportCardName(10001, "fixture support 1", "S1", 101, 1001),
            new SupportCardName(10002, "fixture support 2", "S2", 105, 1002),
            new SupportCardName(10003, "fixture support 3", "S3", 102, 1003),
            new SupportCardName(10004, "fixture support 4", "S4", 103, 1004),
            new SupportCardName(10005, "fixture support 5", "S5", 106, 1005),
            new SupportCardName(10006, "fixture support 6", "S6", 0, 1006),
        },
        new() { TypeNameHandling = TypeNameHandling.All });
    WriteBrotliJson(Database.SKILLS_FILEPATH, (skills ?? []).ToList());
    WriteBrotliJson(Database.SKILL_UPGRADE_SPECIALITY_FILEPATH, new List<SkillUpgradeSpeciality>());
    WriteBrotliJson(Database.TALENT_SKILLS_FILEPATH, new Dictionary<int, TalentSkillData[]>());
    WriteBrotliJson(Database.FACTOR_IDS_FILEPATH, new Dictionary<int, string>());
    WriteBrotliJson(Database.SADDLE_IDS_FILEPATH, Array.Empty<int>());
    WriteBrotliJson(Database.SUCCESSION_RELATION_FILEPATH, new SuccessionRelationTable());
    if (await Database.Initialize() != DatabaseAvailability.Ready)
        throw new InvalidOperationException("Smoke database fixture did not load atomically.");
}


static void WriteBrotliJson<T>(string path, T value, JsonSerializerSettings? settings = null)
{
    using var file = File.Create(path);
    using var brotli = new BrotliStream(file, CompressionLevel.SmallestSize);
    using var writer = new StreamWriter(brotli, Encoding.UTF8);
    using var json = new JsonTextWriter(writer);
    (settings is null
        ? Newtonsoft.Json.JsonSerializer.CreateDefault()
        : Newtonsoft.Json.JsonSerializer.CreateDefault(settings)).Serialize(json, value);
}


}
