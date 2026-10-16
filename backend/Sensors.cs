using Microsoft.AspNetCore.SignalR;

namespace SpatialTwinGuard;

public record SensorReading(string SpaceKey, double Temp, DateTimeOffset? Since);

// 模擬約 10 個溫度感測器；每 2 秒透過 SignalR 把全部讀數推給所有連線的前端
public class SensorSimulator(IHubContext<SensorHub> hub) : BackgroundService
{
    public const double Threshold = 30;
    private const double Base = 24;
    private static readonly string[] Spaces =
    [
        "KG-Technikraum-I", "KG-Technikraum-II", "KG-Technikraum-III", "KG-Technikraum-IV",
        "KG-Labor-K1", "KG-Labor-K2", "KG-Labor-K3", "EG-Labor-I", "EG-Labor-II", "KG-Seminarraum",
    ];

    private class State { public required string Key; public double Temp; public int Heat; public DateTimeOffset? Since; }

    private readonly object _lock = new();
    private readonly Random _rnd = new();
    private List<State>? _states;

    private List<State> States => _states ??= Spaces.Select(k => new State { Key = k, Temp = Base + _rnd.NextDouble() * 2 }).ToList();

    public List<SensorReading> Snapshot()
    {
        lock (_lock) return States.Select(s => new SensorReading(s.Key, Math.Round(s.Temp, 1), s.Since)).ToList();
    }

    public List<SensorReading> Alerts() => Snapshot().Where(r => r.Temp >= Threshold).ToList();

    // 讓某個感測器開始升溫；不指定就隨機挑一個正常的（錄影與驗收用）
    public void Spike(string? key)
    {
        lock (_lock)
        {
            var cool = States.Where(s => s.Temp < Threshold).ToList();
            var s = key is null ? (cool.Count > 0 ? cool[_rnd.Next(cool.Count)] : null) : States.FirstOrDefault(x => x.Key == key);
            if (s != null) s.Heat = 6;
        }
    }

    private void Step()
    {
        lock (_lock)
        {
            foreach (var s in States)
            {
                if (s.Heat > 0) { s.Temp += 1.5 + _rnd.NextDouble(); s.Heat--; }
                else s.Temp += (Base - s.Temp) * 0.25 + (_rnd.NextDouble() - 0.5) * 0.6;
                if (s.Temp >= Threshold) s.Since ??= DateTimeOffset.UtcNow;
                else s.Since = null;
            }
            // 偶爾自己發生一次升溫，畫面不會一直平靜
            if (_rnd.NextDouble() < 0.06 && !States.Any(s => s.Heat > 0 || s.Temp >= Threshold))
            {
                var s = States[_rnd.Next(States.Count)];
                s.Heat = 6;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(ct))
        {
            Step();
            await hub.Clients.All.SendAsync("readings", Snapshot(), ct);
        }
    }
}

public class SensorHub(SensorSimulator sim) : Hub
{
    // 剛連上的前端先拿到目前讀數，不必等下一次推播
    public override Task OnConnectedAsync() => Clients.Caller.SendAsync("readings", sim.Snapshot());

    public void Spike(string? spaceKey = null) => sim.Spike(spaceKey);
}
