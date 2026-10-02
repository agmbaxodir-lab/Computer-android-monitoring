/** Hodisa turlarining foydalanuvchiga tushunarli nomi va yo'nalishi. */
export const EVENT_LABELS: Record<string, string> = {
  FILE_SENT: "Yuborilgan",
  DOWNLOADED: "Qabul qilingan",
  UPLOADED: "Yuklangan (upload)",
  SHARED: "Ulashilgan",
  CREATED: "Yaratilgan",
  MODIFIED: "O'zgartirilgan",
  RENAMED: "Nomi o'zgargan",
  DELETED: "O'chirilgan",
  COPIED: "Nusxalangan",
  MOVED: "Ko'chirilgan",
  OPENED: "Ochilgan",
};

export function eventLabel(type: string): string {
  return EVENT_LABELS[type] ?? type;
}

export function formatSize(bytes: number | null | undefined): string {
  if (bytes == null) return "-";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}
