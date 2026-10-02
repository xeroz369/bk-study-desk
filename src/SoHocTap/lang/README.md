# Gói ngôn ngữ (language pack)

**Tiếng Việt** · [English](#english)

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

1. Sao chép `vi.json` thành `<mã>.json`, ví dụ `fr.json`.
2. Sửa `_meta.name` (ví dụ `"Français"`) và `_meta.culture` (ví dụ `"fr-FR"`).
3. Dịch các value. Giữ nguyên key và các chỗ trống `{0}`, `{1}`…
4. Mở lại app.
5. Vào **Cài đặt** → **Ngôn ngữ**, chọn ngôn ngữ mới, rồi chọn **Khởi động lại ngay**.

Lựa chọn được lưu trong `data\config.json`, ở key `app.language`. Rất hoan nghênh PR thêm ngôn ngữ mới.

**Khi thêm hay sửa chữ trên giao diện:** sửa `vi.json` trước, rồi mới thêm bản dịch vào các gói khác. Người đọc là sinh viên, nên viết tiếng Việt dễ hiểu, ít thuật ngữ.

---

## English

<details>
<summary><b>Language packs</b> (English version)</summary>

All UI text comes from the language packs in this folder.

- Each language is one JSON file named after its language code:
  - `vi.json`: Vietnamese, the **source** and default language;
  - `en.json`: English.
- This folder is copied next to the exe, so you can edit or add packs without rebuilding the app.

## Format

Each pack is a flat JSON object of `"key": "text"` pairs, saved as UTF-8 with 2-space indentation:

```json
{
  "_meta.name": "English",
  "_meta.culture": "en-US",

  "nav.today": "Today",
  "download.summary": "{0} sections with documents · {1} selected, {2} files ({3})"
}
```

- **Keys** look like `group.name`, for example `nav.today`, `settings.save`. Translate only the values; **never change the keys**.
  - Groups: `app`, `nav`, `status`, `info`, `login`, `common`, `col` (table column headers), `home`, `calendar`, `subjects`, `download`, `grades`, `curriculum`, `services`, `settings`, `tray`, `notify`, `timeline`, `format`, `kind`, `files`, `about`, `data`, `link`, `setup`, `sync`, `update`, `web`.
- **`_meta.name`**: the language name shown in Settings.
- **`_meta.culture`**: the .NET culture used to format numbers and dates, for example `vi-VN`, `en-US`, `fr-FR`.
- **Placeholders** `{0}`, `{1}`…: the app fills in numbers, names or dates here (see [`string.Format`](https://learn.microsoft.com/dotnet/standard/base-types/composite-formatting)).
  - Keep every placeholder from the source; you can change their order.
  - Some placeholders carry a format:
    - `{0:0}`: whole number;
    - `{1:dd/MM}`: day/month;
    - `{0:N0}`: with thousands separators.
  - To show a brace, write `{{` or `}}`.
- `_` marks the Alt access key, for example `"_Filter"` is Alt+F.
- `\n` is a line break, `\"` is a double quote.

## Missing keys

- If the selected pack lacks a key, the app uses the Vietnamese text from `vi.json`.
- If `vi.json` lacks it too, the app shows the key itself, for example `settings.save`, so missing text is easy to spot.
- A broken pack (invalid JSON) is skipped and the app uses Vietnamese; the reason is written to `data\app.log`.

## Add a language

1. Copy `vi.json` (or `en.json` if that's easier to translate from) to `<code>.json`, for example `fr.json`.
2. Set `_meta.name` (for example `"Français"`) and `_meta.culture` (for example `"fr-FR"`).
3. Translate the values. Keep the keys and the `{0}`, `{1}`… placeholders.
4. Reopen the app.
5. Go to **Settings** > **Language**, select the new language, then select **Restart now**.

The choice is saved in `data\config.json` under the key `app.language`. Pull requests that add languages are very welcome.

**When you add or change UI text:** edit `vi.json` first, then add the translation to the other packs. English text follows [docs/english-style.md](../../../docs/english-style.md).

</details>
