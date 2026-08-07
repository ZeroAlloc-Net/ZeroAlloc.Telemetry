namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="TagName">Tag key written to the span.</param>
/// <param name="Literal">
/// The value as a C# literal, ready to emit — <c>"chat"</c>, <c>true</c>, <c>42</c>, or a
/// qualified enum member. Produced by Roslyn from the attribute argument rather than formatted
/// here, so quoting and escaping match the language exactly.
/// </param>
internal sealed record ConstantTagModel(string TagName, string Literal);
