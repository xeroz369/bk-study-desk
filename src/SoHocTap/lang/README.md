# Gói ngôn ngữ (language pack)

Mọi chữ trên giao diện đều lấy từ các gói ngôn ngữ trong thư mục này.

- Mỗi ngôn ngữ là một file JSON, đặt tên theo mã ngôn ngữ:
  - `vi.json`: tiếng Việt, ngôn ngữ **gốc** và mặc định;
  - `en.json`: tiếng Anh.
- Thư mục này được chép ra cạnh file exe, nên sửa hay thêm gói không cần build lại app.

## Định dạng

Mỗi gói là một JSON object phẳng gồm các cặp `"key": "chữ"`. File lưu UTF-8, thụt lề 2 dấu cách:

```json
{
  "_meta.name": "Tiếng Việt",
  "_meta.culture": "vi-VN",

  "nav.today": "Hôm nay",
  "download.summary": "{0} mục có tài liệu · đã chọn {1}, {2} file ({3})"
}
```

- **Key** có dạng `nhóm.tên`, ví dụ `nav.today`, `settings.save`. Chỉ dịch phần value, **không đổi key**.
  - Các nhóm: `app`, `nav`, `status`, `info`, `login`, `common`, `col` (tiêu đề cột bảng), `home`, `calendar`, `subjects`, `download`, `grades`, `curriculum`, `services`, `settings`, `tray`, `notify`, `timeline`, `format`, `kind`, `files`, `about`, `data`, `link`, `setup`, `sync`, `update`, `web`.
- **`_meta.name`**: tên ngôn ngữ hiện trong Cài đặt.
- **`_meta.culture`**: culture .NET dùng để format số và ngày, ví dụ `vi-VN`, `en-US`, `fr-FR`.
- **Chỗ trống** `{0}`, `{1}`… (placeholder): app tự điền số, tên hay ngày vào đây (theo [`string.Format`](https://learn.microsoft.com/dotnet/standard/base-types/composite-formatting)).
  - Giữ đủ các chỗ trống của bản gốc; đổi thứ tự thì được.
  - Một số chỗ trống có kèm định dạng:
    - `{0:0}`: số nguyên;
    - `{1:dd/MM}`: ngày/tháng;
    - `{0:N0}`: có dấu phân cách hàng nghìn.
  - Muốn hiện dấu ngoặc nhọn thì viết `{{` hoặc `}}`.
- Dấu `_` đánh dấu phím Alt, ví dụ `"_Lọc"` là Alt+L.
- `\n` là xuống dòng, `\"` là dấu nháy kép.

## Khi thiếu key

- Gói đang chọn thiếu key nào thì app dùng chữ tiếng Việt trong `vi.json`.
- Thiếu luôn trong `vi.json` thì app hiện chính cái key, ví dụ `settings.save`. Nhờ vậy dễ thấy chỗ còn thiếu.
- Gói lỗi (JSON sai cú pháp) bị bỏ qua và app dùng tiếng Việt; lý do ghi trong `data\app.log`.

## Thêm ngôn ngữ

1. Copy `vi.json` thành `<mã>.json`, ví dụ `fr.json`.
2. Sửa `_meta.name` (ví dụ `"Français"`) và `_meta.culture` (ví dụ `"fr-FR"`).
3. Dịch các value. Giữ nguyên key và các chỗ trống `{0}`, `{1}`…
4. Mở lại app.
5. Vào **Cài đặt → Ngôn ngữ**, chọn ngôn ngữ mới, rồi bấm **Khởi động lại ngay**.

Lựa chọn được lưu trong `data\config.json`, ở key `app.language`. Rất hoan nghênh PR thêm ngôn ngữ mới.

**Khi thêm hay sửa chữ trên giao diện:** sửa `vi.json` trước, rồi mới thêm bản dịch vào các gói khác. Người đọc là sinh viên, nên viết tiếng Việt dễ hiểu, ít thuật ngữ.

---

## English

All UI text comes from flat JSON language packs, one per language (`vi.json` is the base, `en.json` is English).
- **Keys:** never change keys, only translate values.
- **Placeholders:** keep every `{0}`, `{1}`…
- **Missing keys** fall back to Vietnamese.
- **To add a language:** copy `vi.json` to `<code>.json`, set `_meta.name` and `_meta.culture`, translate the values, then pick the new language in *Cài đặt → Ngôn ngữ*.
