using System.Collections.Concurrent;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Time;
using UmamusumeResponseAnalyzer.TerminalGui;

sealed class WorkspaceSmokeSession : IDisposable
{
    readonly BlockingCollection<RunRequest> runs = [];
    readonly CancellationTokenSource lifetime = new();
    readonly TaskCompletionSource<Startup> ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly Thread uiThread;
    readonly int width;
    readonly int height;
    readonly UiHost host;
    readonly Task run;
    IApplication? ownedApplication;
    BootstrapWorkspace? ownedBootstrap;
    int disposed;

    public WorkspaceSmokeSession(int width = 120, int height = 40)
    {
        InitializeConfig();
        this.width = width;
        this.height = height;
        uiThread = new Thread(RunUiThread)
        {
            IsBackground = true,
            Name = "URA workspace smoke"
        };
        uiThread.Start();

        var startup = ready.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Application = startup.Application;
        host = startup.Host;
        Bootstrap = startup.Bootstrap;
        run = StartAsync(host.RunAsync).GetAwaiter().GetResult();
        if (run.IsCompleted)
        {
            run.GetAwaiter().GetResult();
            throw new InvalidOperationException("UiHost stopped during workspace smoke startup.");
        }
        Flush();
    }

    static void InitializeConfig()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var configDirectory = Path.Combine(
            Path.GetTempPath(),
            "ura-workspace-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDirectory);
        try
        {
            Directory.SetCurrentDirectory(configDirectory);
            UmamusumeResponseAnalyzer.Config.Initialize();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(configDirectory, recursive: true);
        }
    }

    public IApplication Application { get; }
    public Workspace Bootstrap { get; }

    public void Flush()
        => host.FlushAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

    public void SendKey(Key key)
        => InvokeOnOwnerAsync(() =>
        {
            Application.Keyboard.RaiseKeyDownEvent(key);
            Application.LayoutAndDraw(forceRedraw: true);
        }).GetAwaiter().GetResult();

    public string CaptureScreen()
        => CaptureScreen(width, height);

    public string CaptureScreen(
        int screenWidth,
        int screenHeight,
        bool restore = true,
        bool flushHost = true)
    {
        if (flushHost)
            Flush();
        return InvokeOnOwner(() =>
        {
            var driver = Application.Driver
                ?? throw new InvalidOperationException("Terminal.Gui driver was not initialized.");
            var originalSize = driver.Screen.Size;
            var requestedSize = new System.Drawing.Size(screenWidth, screenHeight);
            var resized = originalSize != requestedSize;
            if (resized)
                driver.SetScreenSize(screenWidth, screenHeight);
            Application.LayoutAndDraw(forceRedraw: true);
            var screen = driver.ToString();
            if (restore && resized)
            {
                driver.SetScreenSize(originalSize.Width, originalSize.Height);
                Application.LayoutAndDraw(forceRedraw: true);
            }
            return screen;
        });
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        try
        {
            lifetime.Cancel();
            run.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
        finally
        {
            runs.CompleteAdding();
            if (!uiThread.Join(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Workspace smoke UI thread did not stop.");
            runs.Dispose();
            lifetime.Dispose();
        }
    }

    async Task<Task> StartAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runs.Add(new(action, completion));
        await WaitForAsync(() => Application.TopRunnableView is not null || completion.Task.IsCompleted);
        if (completion.Task.IsCompleted)
            return completion.Task;

        await WaitForAsync(() => Application.Driver?.SixelSupport is not null);
        await InvokeOnOwnerAsync(() =>
        {
            var driver = Application.Driver
                ?? throw new InvalidOperationException("Terminal.Gui driver was not initialized.");
            _ = driver.SixelSupport
                ?? throw new InvalidOperationException("Sixel detection did not complete.");
            driver.SetSixelSupport(new()
            {
                IsSupported = true,
                Resolution = new(10, 20)
            });
            driver.SetScreenSize(width, height);
            Application.LayoutAndDraw(forceRedraw: true);
        });
        return completion.Task;
    }

    async Task WaitForAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
            await Task.Delay(10, cancellation.Token);
    }

    public T InvokeOnOwner<T>(Func<T> action)
    {
        T result = default!;
        InvokeOnOwnerAsync(() => result = action()).GetAwaiter().GetResult();
        return result;
    }

    async Task InvokeOnOwnerAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Application.Invoke(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    void RunUiThread()
    {
        var previousContext = SynchronizationContext.Current;
        var previousDisableRealDriverIo = Environment.GetEnvironmentVariable("DisableRealDriverIO");
        try
        {
            Environment.SetEnvironmentVariable("DisableRealDriverIO", "1");
            var application = Terminal.Gui.App.Application.Create(new VirtualTimeProvider())
                .Init(DriverRegistry.Names.ANSI);
            ownedApplication = application;
            application.Driver!.SetScreenSize(width, height);
            var context = new OwnerSynchronizationContext(this);
            SynchronizationContext.SetSynchronizationContext(context);
            application.Iteration += ApplicationIteration;
            var createdHost = new UiHost(application, context, lifetime.Token);
            TerminalUi.Initialize(createdHost);
            ownedBootstrap = createdHost.Bootstrap;
            ready.SetResult(new(application, createdHost, ownedBootstrap.Workspace));
            foreach (var request in runs.GetConsumingEnumerable())
                request.Execute();
        }
        catch (Exception ex)
        {
            ready.TrySetException(ex);
            runs.CompleteAdding();
            while (runs.TryTake(out var request))
                request.Fail(ex);
        }
        finally
        {
            try
            {
                ownedBootstrap?.Dispose();
                ownedBootstrap = null;
                if (ownedApplication is { } application)
                    application.Iteration -= ApplicationIteration;
                ownedApplication?.Dispose();
                ownedApplication = null;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
                Environment.SetEnvironmentVariable("DisableRealDriverIO", previousDisableRealDriverIo);
            }
        }
    }

    Task DispatchToOwner(SendOrPostCallback callback, object? state)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new RunRequest(
            () =>
            {
                callback(state);
                return Task.CompletedTask;
            },
            completion);
        try
        {
            runs.Add(request);
            var application = Volatile.Read(ref ownedApplication)
                ?? throw new ObjectDisposedException(nameof(WorkspaceSmokeSession));
            application.Invoke(static () => { });
        }
        catch (Exception ex)
        {
            request.Fail(ex);
            throw;
        }
        return completion.Task;
    }

    void ApplicationIteration(
        object? sender,
        Terminal.Gui.App.EventArgs<IApplication?> e)
    {
        while (runs.TryTake(out var request))
            request.Execute();
    }

    sealed record Startup(IApplication Application, UiHost Host, Workspace Bootstrap);

    sealed class OwnerSynchronizationContext(WorkspaceSmokeSession owner) : SynchronizationContext
    {
        readonly int ownerThreadId = Environment.CurrentManagedThreadId;

        public override void Post(SendOrPostCallback callback, object? state)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _ = owner.DispatchToOwner(callback, state);
        }

        public override void Send(SendOrPostCallback callback, object? state)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (Environment.CurrentManagedThreadId == ownerThreadId)
            {
                callback(state);
                return;
            }

            owner.DispatchToOwner(callback, state).GetAwaiter().GetResult();
        }

        public override SynchronizationContext CreateCopy() => this;
    }

    sealed class RunRequest(Func<Task> action, TaskCompletionSource completion)
    {
        int started;

        public TaskCompletionSource Completion { get; } = completion;

        public void Fail(Exception exception)
        {
            if (Interlocked.Exchange(ref started, 1) == 0)
                Completion.TrySetException(exception);
        }

        public void Execute()
        {
            if (Interlocked.Exchange(ref started, 1) != 0)
                return;

            try
            {
                var task = action();
                if (task.IsCompleted)
                {
                    Complete(task);
                    return;
                }

                _ = task.ContinueWith(
                    static (completed, state) => ((RunRequest)state!).Complete(completed),
                    this,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Completion.TrySetException(ex);
            }
        }

        void Complete(Task task)
        {
            try
            {
                task.GetAwaiter().GetResult();
                Completion.TrySetResult();
            }
            catch (Exception ex)
            {
                Completion.TrySetException(ex);
            }
        }
    }
}
sealed class TempCurrentDirectory : IDisposable
{
    readonly string originalCurrentDirectory = Directory.GetCurrentDirectory();

    public TempCurrentDirectory(string pluginId)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ura-plugin-runtime-smoke",
            $"{pluginId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
        Directory.SetCurrentDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(originalCurrentDirectory);
        Directory.Delete(Path, recursive: true);
    }
}
