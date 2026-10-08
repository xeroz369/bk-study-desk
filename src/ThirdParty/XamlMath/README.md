# XAML-Math (vendored)

Bộ dựng công thức TeX cho trang Luyện tập native. Mã lấy từ [ForNeVeR/xaml-math](https://github.com/ForNeVeR/xaml-math), commit `4f3c875` (16/09/2026), giấy phép MIT (`LICENSE.md`). Font Computer Modern trong `AvaloniaMath/Fonts` theo giấy phép ở `fonts/LICENSES.md`.

Từ đây repo tự bảo trì, không theo upstream. Lý do chọn và số liệu spike: `docs/plans/2026-10-07-practice-native-spec.md` mục 2.

- `XamlMath.Shared`: parser, atom, box. Không phụ thuộc UI.
- `AvaloniaMath`: font, renderer, `FormulaBlock`. Giữ tên assembly vì font nạp qua `avares://AvaloniaMath`.
- Test: `tests/BKStudyDesk.Math.Tests`.

## Thay đổi so với upstream

- Chỉ build `net10.0`, bỏ package `System.Reactive`, `Nullable`, `ChangelogAutomation.MSBuild`.
- Avalonia 12: `AvaloniaCharInfoEx` dùng `CharacterToGlyphMap.TryGetGlyph` và constructor `GlyphRun` mới.
- `FormulaBlock`: đổi theo `OnPropertyChanged` thay cho `Observable.Merge`.
- Lệnh thêm (`Parsers/VendorCommands.cs`, `Data/PredefinedTexFormulas.xml`): `\displaystyle`, `\textstyle`, `\scriptstyle`, `\dfrac`, `\tfrac`, `\big`, `\Big`, `\bigg`, `\Bigg` (và biến thể `l`, `r`, `m`), `\mathbb`, `\quad`, `\qquad`, `\ `, `\iff`, `\dots`, `\iint`, `\iiint`.
- `\mathbb`: không có font msbm nên ℂ ℍ ℕ ℙ ℚ ℝ ℤ vẽ bằng font hệ thống, chữ khác giữ dạng đứng.
- `AvaloniaSystemFont`: font chính thiếu ký tự thì lấy font dự phòng (upstream ném lỗi lúc Render).
- Môi trường thêm: `array` (cột `l c r`, vạch `|`, `\hline`), `cases`, `matrix`, `bmatrix`, `Bmatrix`, `vmatrix`, `Vmatrix`. `\\` ở cuối bảng không sinh hàng rỗng.
- Sửa lỗi vẽ: `pmatrix` dùng ngoặc tròn (upstream ngoặc vuông); `FormulaBlock` khử răng cưa grayscale để glyph không có viền màu khác màu chữ.
- `FormulaBlock` không bao giờ ném lỗi: bắt mọi lỗi lúc parse, dựng box và vẽ; thử lấy glyph ngay lúc dựng (`GlyphProbeRenderer`); báo qua `HasError`, `LastError`, sự kiện `Failed`. Thêm `MathStyle` (Text cho công thức trong dòng).
- Công thức rỗng: không vẽ gì, không báo lỗi.
- Chữ font hệ thống (`\text`, `\mathbb`) đo theo hình glyph (InkBounds), không theo chiều cao dòng: số mũ và đường chân đúng chỗ.
- `\lim`, `\sup`, `\inf`, `\limsup`, `\liminf`, `\max`, `\min`, `\det`, `\gcd`, `\Pr`: cận ở dưới khi Display, bên cạnh khi Text (`AddLimitOperator`).
- `array`: cột cách nhau 1 em như `\arraycolsep` của LaTeX.
- `FormulaBlock.Ascent`: khoảng từ đỉnh tới đường chân, để `MathView` báo `TextBlock.BaselineOffset` khi nằm trong dòng chữ.

## Kiểm tra

- `dotnet test tests/BKStudyDesk.Math.Tests`: lệnh, môi trường, lỗi vẽ, `MathView`, `ContentRenderer`, cả bộ công thức của `studypack/examples`.
- `powershell -File src\SoHocTap\tools-dev\math-corpus.ps1`: cả bộ công thức của `content/` (nội dung riêng), chạy trước mỗi release.
