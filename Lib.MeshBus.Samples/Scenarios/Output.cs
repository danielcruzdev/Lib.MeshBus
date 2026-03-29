namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Shared helpers for consistent console output across all scenarios.
/// </summary>
internal static class Output
{
    public static void Header(string title, params (string Key, string Value)[] details)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ── {title} ──");
        Console.ResetColor();
        foreach (var (key, value) in details)
            Console.WriteLine($"     {key + ":",-12} {value}");
        Console.WriteLine();
    }

    public static void Line(string message, ConsoleColor color = ConsoleColor.Gray)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }

    public static void Sent(string message) =>
        Line($"  [→ SENT]  {message}", ConsoleColor.Green);

    public static void Received(string message) =>
        Line($"  [← RECV]  {message}", ConsoleColor.Cyan);

    public static void Summary(int received, int sent, string? label = null)
    {
        Console.WriteLine();
        var tag = label is not null ? $"[{label}] " : "";
        var ok = received >= sent;
        Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($"  {(ok ? "✓" : "⚠")} {tag}Received {received}/{sent} messages");
        Console.ResetColor();
    }

    public static void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠  {message}");
        Console.ResetColor();
    }

    public static void Info(string message) =>
        Line(message, ConsoleColor.DarkGray);
}
