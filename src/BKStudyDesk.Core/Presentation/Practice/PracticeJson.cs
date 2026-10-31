using System.Text.Json.Serialization;

namespace SoHocTap.Presentation.Practice;

/// <summary>
/// Metadata JSON sinh sẵn cho các kiểu của Luyện tập (cùng cách dùng với <c>SoHocTap.Core.CoreJson</c>, tách riêng vì test Logic
/// chỉ link Core/Data/Library mà không link thư mục này).
/// </summary>
[JsonSerializable(typeof(StudyPack))]
[JsonSerializable(typeof(ProgressState))]
[JsonSerializable(typeof(QuestionRecord))]
[JsonSerializable(typeof(LessonVisit))]
[JsonSerializable(typeof(ExamAttempt))]
[JsonSerializable(typeof(SrsRecord))]
[JsonSerializable(typeof(NoteRecord))]
[JsonSerializable(typeof(List<SavedQuiz>))]
[JsonSerializable(typeof(ContentLibrary.ManifestFile))]
internal sealed partial class PracticeJson : JsonSerializerContext;
