using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace DSA.Tests.Infrastructure;

public sealed record LoadMetricResult(
    string ScenarioName,
    string FrameworkDescription,
    string TargetFramework,
    double ColdStartMs,
    double WarmMeanMs,
    double WarmStdDevMs,
    double WarmMedianMs,
    double WarmIqrMs,
    double WarmP95Ms,
    double WarmMinMs,
    double WarmMaxMs,
    long AllocatedBytesPerOp,
    int WarmIterations,
    int RunIndex = 1);

public static class LoadBenchmarkRunner
{
    private static readonly Lock StaInitLock = new();
    private static Dispatcher? _staDispatcher;

    public static string CurrentTfm =>
#if NET11_0_OR_GREATER
        "net11.0";
#else
        "net10.0";
#endif

    public static int CurrentRunIndex =>
        int.TryParse(Environment.GetEnvironmentVariable("DSA_BENCH_RUN_INDEX"), out int idx) ? idx : 1;

    /// <summary>
    /// Gets a persistent STA thread <see cref="Dispatcher"/> with an active message pump
    /// so <c>Application.Current.Dispatcher</c> never deadlocks across async tests.
    /// </summary>
    public static Dispatcher StaDispatcher
    {
        get
        {
            if (_staDispatcher != null) return _staDispatcher;
            lock (StaInitLock)
            {
                if (_staDispatcher != null) return _staDispatcher;

                using var ready = new ManualResetEventSlim(false);
                var thread = new Thread(() =>
                {
                    _staDispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                })
                {
                    IsBackground = true,
                    Name = "DSA.Tests.PersistentStaThread"
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.Wait();
                return _staDispatcher!;
            }
        }
    }

    public static LoadMetricResult Measure(
        string scenarioName,
        Action action,
        int warmIterations = 80,
        ITestOutputHelper? output = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Clean GC before cold measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 1. Cold start measurement
        long coldStartTicks = Stopwatch.GetTimestamp();
        action();
        double coldMs = Stopwatch.GetElapsedTime(coldStartTicks).TotalMilliseconds;

        // 2. Warmup pass (8 iterations to fully stabilize Tiered JIT / Dynamic PGO)
        for (int i = 0; i < 8; i++)
        {
            action();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 3. Timed warm iterations + allocation tracking
        var samples = new double[warmIterations];
        long allocBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < warmIterations; i++)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        long allocAfter = GC.GetAllocatedBytesForCurrentThread();
        long bytesPerOp = Math.Max(0, (allocAfter - allocBefore) / Math.Max(1, warmIterations));

        return BuildAndRecordResult(scenarioName, coldMs, samples, bytesPerOp, warmIterations, output);
    }

    public static async Task<LoadMetricResult> MeasureAsync(
        string scenarioName,
        Func<Task> asyncAction,
        int warmIterations = 60,
        ITestOutputHelper? output = null)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 1. Cold start measurement
        long coldStartTicks = Stopwatch.GetTimestamp();
        await asyncAction().ConfigureAwait(false);
        double coldMs = Stopwatch.GetElapsedTime(coldStartTicks).TotalMilliseconds;

        // 2. Warmup pass (8 iterations to fully stabilize Tiered JIT / Dynamic PGO)
        for (int i = 0; i < 8; i++)
        {
            await asyncAction().ConfigureAwait(false);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 3. Timed warm iterations
        var samples = new double[warmIterations];
        long totalAllocated = 0;

        for (int i = 0; i < warmIterations; i++)
        {
            long allocBefore = GC.GetTotalAllocatedBytes(precise: false);
            long start = Stopwatch.GetTimestamp();
            await asyncAction().ConfigureAwait(false);
            samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            long allocAfter = GC.GetTotalAllocatedBytes(precise: false);
            totalAllocated += Math.Max(0, allocAfter - allocBefore);
        }

        long bytesPerOp = totalAllocated / Math.Max(1, warmIterations);

        return BuildAndRecordResult(scenarioName, coldMs, samples, bytesPerOp, warmIterations, output);
    }

    private static LoadMetricResult BuildAndRecordResult(
        string scenarioName,
        double coldMs,
        double[] samples,
        long bytesPerOp,
        int warmIterations,
        ITestOutputHelper? output)
    {
        Array.Sort(samples);
        double mean = samples.Average();
        double stdDev = ComputeStdDev(samples, mean);
        double median = Percentile(samples, 0.50);
        double p25 = Percentile(samples, 0.25);
        double p75 = Percentile(samples, 0.75);
        double iqr = p75 - p25;
        double p95 = Percentile(samples, 0.95);
        double min = samples[0];
        double max = samples[^1];

        var result = new LoadMetricResult(
            ScenarioName: scenarioName,
            FrameworkDescription: RuntimeInformation.FrameworkDescription,
            TargetFramework: CurrentTfm,
            ColdStartMs: Math.Round(coldMs, 4),
            WarmMeanMs: Math.Round(mean, 4),
            WarmStdDevMs: Math.Round(stdDev, 4),
            WarmMedianMs: Math.Round(median, 4),
            WarmIqrMs: Math.Round(iqr, 4),
            WarmP95Ms: Math.Round(p95, 4),
            WarmMinMs: Math.Round(min, 4),
            WarmMaxMs: Math.Round(max, 4),
            AllocatedBytesPerOp: bytesPerOp,
            WarmIterations: warmIterations,
            RunIndex: CurrentRunIndex);

        LogResult(result, output);
        SaveResultToDisk(result);
        return result;
    }

    public static void RunOnStaThread(Action action)
    {
        if (StaDispatcher.CheckAccess())
        {
            action();
            return;
        }

        StaDispatcher.Invoke(action);
    }

    public static T RunOnStaThread<T>(Func<T> func)
    {
        if (StaDispatcher.CheckAccess())
        {
            return func();
        }

        return StaDispatcher.Invoke(func);
    }

    private static double ComputeStdDev(double[] samples, double mean)
    {
        if (samples.Length <= 1) return 0;
        double sumSq = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double diff = samples[i] - mean;
            sumSq += diff * diff;
        }
        return Math.Sqrt(sumSq / (samples.Length - 1));
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0) return 0;
        if (sorted.Length == 1) return sorted[0];

        double position = (sorted.Length - 1) * percentile;
        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex) return sorted[lowerIndex];

        double weight = position - lowerIndex;
        return sorted[lowerIndex] * (1 - weight) + sorted[upperIndex] * weight;
    }

    private static void LogResult(LoadMetricResult r, ITestOutputHelper? output)
    {
        string line =
            $"[{r.TargetFramework} | Run #{r.RunIndex}] {r.ScenarioName}: " +
            $"Cold={r.ColdStartMs:F3} ms | Mean={r.WarmMeanMs:F3}±{r.WarmStdDevMs:F3} ms | " +
            $"Median={r.WarmMedianMs:F3} ms (IQR={r.WarmIqrMs:F3}) | P95={r.WarmP95Ms:F3} ms | " +
            $"Alloc={r.AllocatedBytesPerOp / 1024.0:F2} KB/op (N={r.WarmIterations})";

        output?.WriteLine(line);
        Console.WriteLine(line);
    }

    private static readonly Lock FileGate = new();

    private static void SaveResultToDisk(LoadMetricResult result)
    {
        try
        {
            lock (FileGate)
            {
                string outDir = Path.Combine(AppContext.BaseDirectory, "BenchmarkResults");
                Directory.CreateDirectory(outDir);

                // Latest single-pass file
                string filePath = Path.Combine(outDir, $"load_metrics_{CurrentTfm}.json");
                List<LoadMetricResult> existing = [];
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    existing = JsonConvert.DeserializeObject<List<LoadMetricResult>>(json) ?? [];
                }
                existing.RemoveAll(x => string.Equals(x.ScenarioName, result.ScenarioName, StringComparison.OrdinalIgnoreCase));
                existing.Add(result);
                File.WriteAllText(filePath, JsonConvert.SerializeObject(existing, Formatting.Indented));

                // Multi-run history file (retains all process runs for statistical aggregation)
                string historyPath = Path.Combine(outDir, $"load_metrics_history_{CurrentTfm}.json");
                List<LoadMetricResult> history = [];
                if (File.Exists(historyPath))
                {
                    var hJson = File.ReadAllText(historyPath);
                    history = JsonConvert.DeserializeObject<List<LoadMetricResult>>(hJson) ?? [];
                }
                history.RemoveAll(x =>
                    x.RunIndex == result.RunIndex &&
                    string.Equals(x.ScenarioName, result.ScenarioName, StringComparison.OrdinalIgnoreCase));
                history.Add(result);
                File.WriteAllText(historyPath, JsonConvert.SerializeObject(history, Formatting.Indented));
            }
        }
        catch
        {
            // Non-critical diagnostic output
        }
    }
}
