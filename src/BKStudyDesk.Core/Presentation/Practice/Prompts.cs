using System.Reflection;

namespace SoHocTap.Presentation.Practice;

public enum PromptMode { Soan, TuongTu, Soat, GiaiThich }

/// <param name="Course">"MT1009 Phương pháp tính".</param>
/// <param name="Unit">Tên chương (giữ kết quả đúng chỗ).</param>
/// <param name="Units">Tên các chương đang có (khi soạn cho cả môn).</param>
/// <param name="Question">Một câu dạng Study Markdown (câu tương tự, giải thích).</param>
/// <param name="Pack">Cả gói dạng Study Markdown (soát).</param>
public sealed record PromptInput(string Course, int Count, string Extra, string? Unit = null, string? Lesson = null,
    IReadOnlyList<string>? Units = null, string? Question = null, string? Pack = null);

/// <summary>
/// Prompt cho AI bất kỳ (ChatGPT, Claude, Gemini...), port <c>prompts.ts</c>. Mọi prompt có cùng các mục cố định để model nhỏ
/// cũng làm theo. Đầu ra luôn là Study Markdown (studypack/SPEC.md). Đây là nội dung gửi cho AI theo định dạng gói tiếng Việt,
/// không phải chữ giao diện, nên không qua lang. Khung dài (định dạng, luật, tự kiểm, ví dụ) nằm ở Prompts/*.md.
/// </summary>
public static class Prompts
{
    private static string Resource(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Practice.Prompts.{name}.md")
            ?? throw new InvalidOperationException($"thiếu Prompts/{name}.md");
        using var r = new StreamReader(s);
        // Git trên Windows có thể đổi sang CRLF lúc checkout: prompt luôn dùng LF.
        return r.ReadToEnd().ReplaceLineEndings("\n");
    }

    public static readonly string Format = Resource("format");
    private static readonly string Rules = Resource("rules");
    private static readonly string Check = Resource("check");
    private static readonly string Example = Resource("example");

    /// <summary>Tên chế độ như trong route của bản Svelte (soan, tuong-tu, soat, giai-thich).</summary>
    public static PromptMode ParseMode(string s) => s switch
    {
        "tuong-tu" => PromptMode.TuongTu,
        "soat" => PromptMode.Soat,
        "giai-thich" => PromptMode.GiaiThich,
        _ => PromptMode.Soan,
    };

    private static string Where(PromptInput p)
    {
        if (!string.IsNullOrEmpty(p.Lesson)) return $"Chương \"{p.Unit ?? "undefined"}\", bài \"{p.Lesson}\". Giữ ĐÚNG hai tên này ở dòng # và ##.";
        if (!string.IsNullOrEmpty(p.Unit)) return $"Chương \"{p.Unit}\" (giữ đúng tên ở dòng #). Tự đặt tên bài (##) theo nội dung.";
        var units = string.Join(", ", (p.Units ?? []).Select(u => $"\"{u}\""));
        return $"Các chương đang có: {(units.Length > 0 ? units : "chưa có")}. Nội dung thuộc chương nào thì dùng đúng tên chương đó.";
    }

    public static string Build(PromptMode mode, PromptInput p)
    {
        var task = mode switch
        {
            PromptMode.Soan => $"Tạo khoảng {p.Count} câu luyện tập mới (kèm kiến thức ngắn nếu là bài mới) từ tài liệu tôi gửi kèm.",
            PromptMode.TuongTu => $"Tạo {p.Count} câu TƯƠNG TỰ câu mẫu bên dưới: cùng dạng bài và mức khó, đổi số liệu/ngữ cảnh, tính lại đáp án.",
            PromptMode.Soat => "Soát lại gói câu hỏi bên dưới: tính lại từng đáp án, sửa câu sai, lời giải thiếu bước. Trả về TOÀN BỘ gói đã sửa; cuối mỗi câu đã sửa thêm dòng \"> Đã sửa: <lý do>\".",
            _ => "Giải thích câu bên dưới cho sinh viên chưa hiểu: nhắc lại kiến thức cần dùng, giải từng bước, chỉ ra vì sao các phương án sai là sai, và một mẹo nhớ. Trả lời bằng chữ thường (Markdown), KHÔNG cần theo ĐỊNH DẠNG gói.",
        };
        var extra = Text.TrimJs(p.Extra);
        string[] lines =
        [
            $"Môn: {p.Course}.",
            Where(p),
            extra.Length > 0 ? $"Yêu cầu thêm: {extra}" : "",
            mode == PromptMode.Soan ? "Tài liệu: slide/đề tôi gửi kèm (PDF, ảnh)." : "",
            string.IsNullOrEmpty(p.Question) ? "" : $"Câu mẫu:\n```markdown\n{p.Question}\n```",
            string.IsNullOrEmpty(p.Pack) ? "" : $"Gói cần soát:\n```markdown\n{p.Pack}\n```",
        ];
        var input = string.Join("\n", lines.Where(x => x.Length > 0));
        if (mode == PromptMode.GiaiThich)
            return $"# VAI TRÒ\nBạn là trợ giảng đại học, giải thích rõ ràng, ngắn gọn bằng tiếng Việt.\n\n# NHIỆM VỤ\n{task}\n\n# ĐẦU VÀO\n{input}\n\n# LUẬT\nCông thức viết TeX \\( ... \\). Dùng đúng ký hiệu trong câu. Không dài quá 300 chữ.";
        return string.Join("\n",
            "# VAI TRÒ",
            "Bạn là trợ giảng đại học tạo câu luyện tập cho sinh viên.",
            "",
            "# NHIỆM VỤ",
            task,
            "",
            "# ĐẦU VÀO",
            input,
            "",
            "# ĐẦU RA",
            "Một khối ```markdown theo ĐỊNH DẠNG bên dưới (Study Markdown). Người dùng sẽ dán nguyên khối này vào app.",
            "",
            "# ĐỊNH DẠNG",
            Format,
            "",
            "# LUẬT",
            Rules,
            "",
            "# TỰ KIỂM (soát trước khi trả lời)",
            Check,
            "",
            "# VÍ DỤ (một câu đúng định dạng)",
            Example);
    }
}
