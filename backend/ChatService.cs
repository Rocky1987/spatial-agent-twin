using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpatialTwinGuard;

public class ChatService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly string _system;
    private readonly JsonArray _tools;

    public ChatService(HttpClient http, SpaceMap map, IConfiguration cfg)
    {
        _http = http;
        _apiKey = cfg["Anthropic:ApiKey"];
        _model = cfg["Anthropic:Model"] ?? "claude-haiku-5-5";
        _system = BuildSystemPrompt(map);
        _tools = BuildTools(map);
    }

    private const string Rules = @"你是「空間智慧數位雙生戰情室」的操作助理。使用者用繁體中文下指令，你透過工具操作畫面：左邊是台灣的 3D 地圖，右邊可展開一棟建築的 BIM 模型。

規則：
- 要操作畫面就呼叫工具，不要只用文字描述。一句複合指令（例如「飛到大樓並剖開二樓」）要在同一次回覆裡依序呼叫多個工具，順序就是執行順序。
- 工具參數只能用下方對照表的 key，不可自創。找不到對應的樓層或空間，不要呼叫工具，用一兩句話告訴使用者有哪些可選。
- 問題與畫面操作無關，或只是詢問資訊（例如哪裡有告警），直接用文字回答，不呼叫工具。
- 回覆保持簡短，一到兩句，說明你做了什麼。
- BIM 工具（sectionStorey、isolateSpaces、highlightSpaces、resetView）會自動展開 BIM 窗格並載入模型，不必先呼叫 flyToLocation；只有使用者明確要飛過去或回地圖時才用 flyToLocation。
- 「上一步」、「回到剛才」、「復原」、「撤銷」→ undo（回到上一步之前的畫面，可連續使用，一路退回最初）。「下一步」、「再往前」→ redo（取消剛才的 undo）。
- 「還原」、「恢復」、「清除標記」→ resetView。
- 「標紅」、「標示」、「標記」某空間 → highlightSpaces；「只看」、「隔離」→ isolateSpaces；「剖開」、「切開」、「看某一層」→ sectionStorey。

樓層換算：模型是德國建築，樓層名稱是德文。台灣說的 1 樓 = 德國的 Erdgeschoss（EG）；台灣 2 樓 = 1. Obergeschoss（OG1）；依此類推。地下一樓 = Keller（KG）。使用者說「二樓」就是台灣的 2 樓，對應 OG1。
";

    // 角色與規則 + 樓層換算 + 對照表全文
    private static string BuildSystemPrompt(SpaceMap m)
    {
        var sb = new StringBuilder(Rules).AppendLine();
        sb.AppendLine("樓層對照表（key | 中文 | 德文 | 別名）：");
        foreach (var s in m.Storeys) sb.AppendLine($"{s.Key} | {s.Zh} | {s.Name} | {string.Join("、", s.Aliases)}");
        sb.AppendLine();
        sb.AppendLine("空間對照表（key | 中文 | 德文 | 所在樓層 | 別名）：");
        foreach (var s in m.Spaces) sb.AppendLine($"{s.Key} | {s.Zh} | {s.LongName} | {s.Storey} | {string.Join("、", s.Aliases)}");
        return sb.ToString();
    }

    private static JsonArray Strs(IEnumerable<string> xs) => new(xs.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());

    // 工具的 key 參數列成 enum，模型只能從清單選
    private static JsonArray BuildTools(SpaceMap m)
    {
        JsonObject Schema(JsonObject props, params string[] required) => new()
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = Strs(required),
        };
        JsonObject Tool(string name, string desc, JsonObject schema) => new() { ["name"] = name, ["description"] = desc, ["input_schema"] = schema };
        JsonObject SpaceKeys() => new()
        {
            ["keys"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string", ["enum"] = Strs(m.Spaces.Select(s => s.Key)) },
                ["description"] = "空間 key 清單",
            },
        };

        return new JsonArray
        {
            Tool("flyToLocation", "切換視窗位置。institute：地圖飛到目標大樓上空並展開 BIM 窗格；taiwan：收合 BIM 窗格，地圖回到台灣全景。",
                Schema(new JsonObject { ["place"] = new JsonObject { ["type"] = "string", ["enum"] = Strs(["institute", "taiwan"]) } }, "place")),
            Tool("sectionStorey", "剖切指定樓層：隱藏該層以上的樓層，露出該層平面。",
                Schema(new JsonObject { ["key"] = new JsonObject { ["type"] = "string", ["enum"] = Strs(m.Storeys.Select(s => s.Key)) } }, "key")),
            Tool("isolateSpaces", "隔離顯示指定的空間，其他構件隱藏。", Schema(SpaceKeys(), "keys")),
            Tool("highlightSpaces", "把指定的空間標成紅色（每次呼叫會先清掉舊標記）。", Schema(SpaceKeys(), "keys")),
            Tool("undo", "回到上一步：把畫面（位置、剖切、隔離、標記）還原到使用者上一句指令執行之前的狀態。", Schema(new JsonObject())),
            Tool("redo", "再往前一步：取消剛才的 undo，回到被退回來的畫面。", Schema(new JsonObject())),
            Tool("resetView", "還原 BIM 視圖：取消剖切、隔離與標記。", Schema(new JsonObject())),
        };
    }

    public async Task<ChatResponse> AskAsync(ChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new ChatException("後端沒有設定 Anthropic:ApiKey（用 dotnet user-secrets 或環境變數 Anthropic__ApiKey）", 500);

        var system = new JsonArray
        {
            new JsonObject { ["type"] = "text", ["text"] = _system, ["cache_control"] = new JsonObject { ["type"] = "ephemeral" } },
        };
        // 告警清單每次不同，放在快取區塊之後
        if (req.Alerts is { Count: > 0 })
        {
            var lines = req.Alerts.Select(a => $"{a.SpaceKey}：{a.Temp:0.#}°C" + (a.Time is null ? "" : $"（{a.Time}）"));
            system.Add(new JsonObject { ["type"] = "text", ["text"] = "目前的告警清單：\n" + string.Join("\n", lines) });
        }
        else
        {
            system.Add(new JsonObject { ["type"] = "text", ["text"] = "目前沒有任何告警。" });
        }

        var body = new JsonObject
        {
            ["model"] = _model,
            ["max_tokens"] = 1024,
            ["system"] = system,
            ["tools"] = JsonNode.Parse(_tools.ToJsonString()),
            ["messages"] = new JsonArray(req.Messages.Select(m => (JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = m.Content }).ToArray()),
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        msg.Headers.Add("x-api-key", _apiKey);
        msg.Headers.Add("anthropic-version", "2023-06-01");

        using var res = await _http.SendAsync(msg, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            var detail = "";
            try { detail = JsonNode.Parse(text)?["error"]?["message"]?.GetValue<string>() ?? ""; } catch { }
            throw new ChatException($"Anthropic API {(int)res.StatusCode}：{detail}");
        }

        var reply = new StringBuilder();
        var calls = new List<ToolCall>();
        foreach (var block in JsonNode.Parse(text)!["content"]!.AsArray())
        {
            switch (block!["type"]!.GetValue<string>())
            {
                case "text": reply.Append(block["text"]!.GetValue<string>()); break;
                case "tool_use":
                    calls.Add(new ToolCall(block["name"]!.GetValue<string>(), JsonSerializer.SerializeToElement(block["input"])));
                    break;
            }
        }
        return new ChatResponse(reply.ToString().Trim(), calls);
    }
}
