using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace MyQuickAccent;

/// <summary>
/// Busca dónde está el cursor de texto, en coordenadas de pantalla.
/// Ninguna API funciona en todas las apps, así que se prueban tres en orden.
/// </summary>
internal static class CaretLocator
{
    public static Rectangle? Find(IntPtr foreground)
    {
        IntPtr focus = foreground;

        // 1. Cursor clásico de Win32 (Bloc de notas viejo, Word, la mayoría de apps de escritorio)
        try
        {
            uint thread = Native.GetWindowThreadProcessId(foreground, out _);
            var info = new Native.GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.GUITHREADINFO>() };
            if (Native.GetGUIThreadInfo(thread, ref info))
            {
                if (info.hwndFocus != IntPtr.Zero) focus = info.hwndFocus;
                int h = info.rcCaret.Bottom - info.rcCaret.Top;
                if (info.hwndCaret != IntPtr.Zero && h > 0)
                {
                    var pt = new Native.POINT { X = info.rcCaret.Left, Y = info.rcCaret.Top };
                    Native.ClientToScreen(info.hwndCaret, ref pt);
                    return new Rectangle(pt.X, pt.Y, Math.Max(1, info.rcCaret.Right - info.rcCaret.Left), h);
                }
            }
        }
        catch { }

        // 2. MSAA (Chrome, Edge, apps Electron como VS Code, Slack, etc.)
        try
        {
            const uint OBJID_CARET = 0xFFFFFFF8;
            var iid = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
            if (Native.AccessibleObjectFromWindow(focus, OBJID_CARET, ref iid, out object? obj) == 0
                && obj is Accessibility.IAccessible acc)
            {
                acc.accLocation(out int l, out int t, out int w, out int h, 0);
                if (h > 0 && (l != 0 || t != 0))
                    return new Rectangle(l, t, Math.Max(1, w), h);
            }
        }
        catch { }

        // 3. UI Automation (apps modernas: Bloc de notas nuevo, WinUI, WPF)
        try
        {
            var el = AutomationElement.FocusedElement;
            if (el != null && el.TryGetCurrentPattern(TextPattern.Pattern, out object p))
            {
                var selection = ((TextPattern)p).GetSelection();
                if (selection.Length > 0)
                {
                    var range = selection[0];
                    var rects = range.GetBoundingRectangles();
                    if (rects.Length == 0)
                    {
                        // Un rango vacío (solo el cursor) no tiene rectángulo: se expande a un carácter
                        range = range.Clone();
                        range.ExpandToEnclosingUnit(TextUnit.Character);
                        rects = range.GetBoundingRectangles();
                    }
                    if (rects.Length > 0 && rects[0].Height > 0)
                        return new Rectangle((int)rects[0].Left, (int)rects[0].Top, 1, (int)rects[0].Height);
                }
            }
        }
        catch { }

        return null;
    }
}
