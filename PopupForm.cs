using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;

namespace MyQuickAccent;

/// <summary>La burbuja con las opciones. Nunca toma el foco, para no interrumpir lo que se está escribiendo.</summary>
internal sealed class PopupForm : Form
{
    private string[] _items = [];
    private int _selected = -1;
    private float _scale = 1f;
    private bool _dark;

    public event Action<int>? ItemClicked;

    public PopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        TopMost = true;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Esquinas redondeadas + sombra en Windows 11
        int round = 2;
        Native.DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    private int Pad => (int)(6 * _scale);
    private int CellW => (int)(40 * _scale);
    private int CellH => (int)(56 * _scale);

    public void ShowAt(string[] items, Rectangle? caret, IntPtr target)
    {
        _items = items;
        _selected = -1;
        _dark = IsDarkTheme();

        uint dpi = Native.GetDpiForWindow(target);
        _scale = dpi > 0 ? dpi / 96f : 1f;

        var size = new Size(Pad * 2 + CellW * items.Length, Pad * 2 + CellH);
        int gap = (int)(8 * _scale);
        Point location;

        if (caret is { } c)
        {
            var area = Screen.FromPoint(c.Location).WorkingArea;
            int x = c.Left - size.Width / 2;
            int y = c.Top - size.Height - gap;          // arriba del texto, como en Mac
            if (y < area.Top) y = c.Bottom + gap;       // si no cabe, abajo
            x = Math.Max(area.Left + 4, Math.Min(x, area.Right - size.Width - 4));
            location = new Point(x, y);
        }
        else
        {
            // No se pudo ubicar el cursor: centrado en la ventana activa
            Native.GetWindowRect(target, out var r);
            location = new Point((r.Left + r.Right - size.Width) / 2, (r.Top + r.Bottom - size.Height) / 2);
        }

        Bounds = new Rectangle(location, size);
        if (!Visible) Show();
        Bounds = new Rectangle(location, size); // de nuevo, por si cambió de monitor/DPI al mostrarse
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int i = (e.X - Pad) / Math.Max(1, CellW);
        if (e.X >= Pad && i >= 0 && i < _items.Length) ItemClicked?.Invoke(i);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        Color back = _dark ? Color.FromArgb(44, 44, 46) : Color.FromArgb(242, 242, 247);
        Color text = _dark ? Color.White : Color.Black;
        Color dim = _dark ? Color.FromArgb(152, 152, 157) : Color.FromArgb(124, 124, 128);
        Color accent = Color.FromArgb(10, 132, 255);
        g.Clear(back);

        using var charFont = new Font("Segoe UI", 24 * _scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var numFont = new Font("Segoe UI", 12 * _scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        for (int i = 0; i < _items.Length; i++)
        {
            var cell = new RectangleF(Pad + i * CellW, Pad, CellW, CellH);
            bool sel = i == _selected;
            if (sel)
            {
                using var path = Rounded(RectangleF.Inflate(cell, -1, 0), 7 * _scale);
                using var fill = new SolidBrush(accent);
                g.FillPath(fill, path);
            }

            using var charBrush = new SolidBrush(sel ? Color.White : text);
            using var numBrush = new SolidBrush(sel ? Color.FromArgb(220, 255, 255, 255) : dim);
            g.DrawString(_items[i], charFont, charBrush, new RectangleF(cell.X, cell.Y, cell.Width, cell.Height * 0.64f), center);
            g.DrawString((i + 1).ToString(), numFont, numBrush, new RectangleF(cell.X, cell.Y + cell.Height * 0.62f, cell.Width, cell.Height * 0.36f), center);
        }
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static bool IsDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }
}
