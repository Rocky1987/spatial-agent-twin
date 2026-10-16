using System.Text.Json;

namespace SpatialTwinGuard;

// 讀前端的 space-map.json（正本在 frontend/src/data/）；Docker 版用設定 SpaceMapPath 指到複製進映像的那份
public class SpaceMap
{
    public List<StoreyDto> Storeys { get; }
    public List<SpaceDto> Spaces { get; }

    public SpaceMap(IConfiguration cfg, IWebHostEnvironment env)
    {
        var path = cfg["SpaceMapPath"] ?? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "frontend", "src", "data", "space-map.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        string[] Arr(JsonElement e, string p) => e.TryGetProperty(p, out var a) ? a.EnumerateArray().Select(x => x.GetString()!).ToArray() : [];
        Storeys = root.GetProperty("storeys").EnumerateArray().Select(s => new StoreyDto(
            s.GetProperty("key").GetString()!, s.GetProperty("name").GetString()!, s.GetProperty("zh").GetString()!,
            s.GetProperty("elevation").GetDouble(), Arr(s, "aliases"))).ToList();
        Spaces = root.GetProperty("spaces").EnumerateArray().Select(s => new SpaceDto(
            s.GetProperty("key").GetString()!, s.GetProperty("name").GetString()!, s.GetProperty("longName").GetString()!,
            s.GetProperty("zh").GetString()!, s.GetProperty("storey").GetString()!, Arr(s, "aliases"))).ToList();
    }
}
