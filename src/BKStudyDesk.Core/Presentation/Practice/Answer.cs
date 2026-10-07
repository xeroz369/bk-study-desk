using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoHocTap.Presentation.Practice;

/// <summary>
/// Câu trả lời như trong <c>ket-qua.json</c> và bài thi: số (chỉ số phương án, single), mảng số (multi) hoặc chuỗi gõ vào (numeric, short).
/// JSON giữ đúng ba dạng đó để 1.x và bản native đọc chung.
/// </summary>
[JsonConverter(typeof(AnswerConverter))]
public readonly record struct Answer
{
    public double? Number { get; init; }
    public int[]? Indices { get; init; }
    public string? Text { get; init; }

    public static Answer Index(int i) => new() { Number = i };
    public static Answer Of(double n) => new() { Number = n };
    public static Answer Of(string s) => new() { Text = s };
    public static Answer Many(params int[] i) => new() { Indices = i };

    /// <summary>Bỏ trống: null, mảng rỗng, chuỗi trắng. Số 0 không phải bỏ trống.</summary>
    public static bool IsBlankValue(Answer? a) =>
        a is not { } v
        || (v.Number is null && v.Indices is null && v.Text is null)
        || v.Indices is { Length: 0 }
        || (v.Text is { } t && SoHocTap.Presentation.Practice.Text.TrimJs(t).Length == 0);

    public bool Equals(Answer other) =>
        Number == other.Number && Text == other.Text
        && (Indices is null ? other.Indices is null : other.Indices is not null && Indices.AsSpan().SequenceEqual(other.Indices));

    public override int GetHashCode() => HashCode.Combine(Number, Text, Indices?.Length);
}

public sealed class AnswerConverter : JsonConverter<Answer>
{
    public override Answer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Null => default,   // bỏ trống
        JsonTokenType.Number => Answer.Of(reader.GetDouble()),
        JsonTokenType.String => Answer.Of(reader.GetString()!),
        JsonTokenType.StartArray => Answer.Many(JsonSerializer.Deserialize<double[]>(ref reader, options)!.Select(x => (int)x).ToArray()),
        _ => throw new JsonException($"câu trả lời không đọc được ({reader.TokenType})"),
    };

    public override void Write(Utf8JsonWriter writer, Answer value, JsonSerializerOptions options)
    {
        if (value.Indices is { } ix)
        {
            writer.WriteStartArray();
            foreach (var i in ix) writer.WriteNumberValue(i);
            writer.WriteEndArray();
        }
        else if (value.Text is { } t) writer.WriteStringValue(t);
        else if (value.Number is { } n)
        {
            if (n == Math.Floor(n) && Math.Abs(n) < 1e15) writer.WriteNumberValue((long)n);
            else writer.WriteNumberValue(n);
        }
        else writer.WriteNullValue();
    }
}
