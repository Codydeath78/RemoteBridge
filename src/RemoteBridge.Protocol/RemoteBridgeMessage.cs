namespace RemoteBridge.Protocol;

public sealed class RemoteBridgeMessage
{
    public string Type { get; set; } = string.Empty;

    public string? SessionCode { get; set; }

    public string? DeviceName { get; set; }

    public string? Message { get; set; }

    public string? SenderRole { get; set; }

    public string? Data { get; set; }

    public double? X { get; set; }

    public double? Y { get; set; }

    public RemoteKey? Key { get; set; }

    public int? WheelDelta { get; set; }
}