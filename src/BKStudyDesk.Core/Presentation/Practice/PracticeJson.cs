using System.Text.Json.Serialization;

namespace SoHocTap.Presentation.Practice;

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
