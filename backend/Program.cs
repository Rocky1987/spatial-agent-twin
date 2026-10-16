using SpatialTwinGuard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SpaceMap>();
builder.Services.AddHttpClient<ChatService>();

var app = builder.Build();

// 唯一的 API：對話紀錄進來，{ reply, toolCalls } 出去。不串流、單輪
app.MapPost("/api/chat", async (ChatRequest req, ChatService chat, CancellationToken ct) =>
{
    if (req.Messages is not { Count: > 0 } || req.Messages[0].Role != "user")
        return Results.BadRequest(new { error = "messages 必須以 user 開頭" });
    try
    {
        return Results.Ok(await chat.AskAsync(req, ct));
    }
    catch (ChatException e)
    {
        return Results.Json(new { error = e.Message }, statusCode: e.Status);
    }
});

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));

app.Run();
