using System.Text.Json;

namespace SpatialTwinGuard;

public record ChatMessage(string Role, string Content);
public record Alert(string SpaceKey, double Temp, string? Time);
public record ChatRequest(List<ChatMessage> Messages, List<Alert>? Alerts);
public record ToolCall(string Name, JsonElement Args);
public record ChatResponse(string Reply, List<ToolCall> ToolCalls);

public class ChatException(string message, int status = 502) : Exception(message)
{
    public int Status { get; } = status;
}

public record StoreyDto(string Key, string Name, string Zh, double Elevation, string[] Aliases);
public record SpaceDto(string Key, string Name, string LongName, string Zh, string Storey, string[] Aliases);
