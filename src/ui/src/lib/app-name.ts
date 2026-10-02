// Tên app chủ, do WPF truyền qua ?app= (Shell/WebUi.cs): "Sổ học tập" (bản riêng) hoặc "BK Study Desk" (bản public).
export const APP_NAME = new URLSearchParams(location.search).get('app') || 'BK Study Desk';
