using System;

namespace AutoKassa.Services
{
    public enum ToastType { Delete, Success, Error, Info }

    public class ToastItem
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Message { get; set; } = "";
        public ToastType Type { get; set; } = ToastType.Info;
        public Action? UndoAction { get; set; }
        public bool HasUndo => UndoAction != null;
    }
}
