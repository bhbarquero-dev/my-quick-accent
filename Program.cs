using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MyQuickAccent;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "MyQuickAccent.SingleInstance", out bool first);
        if (!first) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new AccentApp());
    }
}

internal sealed class AccentApp : ApplicationContext
{
    // ── Opciones por tecla. El orden define el número: la primera es el 1. ──
    // Mismo orden que macOS con teclado en inglés (EE. UU.).
    // Para cambiar o agregar, edite esta tabla (la clave es la tecla, en mayúscula).
    private static readonly Dictionary<char, string> Accents = new()
    {
        ['A'] = "àáâäæãåā",
        ['E'] = "èéêëēėę",
        ['I'] = "îïíīįì",
        ['O'] = "ôöòóœøōõ",
        ['U'] = "ûüùúū",
        ['N'] = "ñń",
        ['C'] = "çćč",
        ['S'] = "ßśš",
        ['Y'] = "ÿ",
        ['L'] = "ł",
        ['Z'] = "žźż",
    };

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MyQuickAccent";

    private readonly PopupForm _popup = new();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _watchdog = new() { Interval = 100 };
    private readonly Native.HookProc _hookProc; // referencia viva para que el GC no la recoja
    private readonly IntPtr _hook;

    private bool _paused;
    private bool _active;        // el popup está abierto (o por abrirse)
    private uint _lastDownVk;    // última tecla de la tabla que se presionó y sigue abajo
    private bool _lastDownUpper;
    private uint _heldVk;        // tecla que abrió el popup; se ignoran sus repeticiones hasta soltarla
    private string[] _options = [];
    private IntPtr _target;

    public AccentApp()
    {
        _ = _popup.Handle; // crea la ventana ya, para poder usar BeginInvoke
        _popup.ItemClicked += Choose;
        _watchdog.Tick += (_, _) => Watch();

        var pause = new ToolStripMenuItem("Pausar") { CheckOnClick = true };
        pause.CheckedChanged += (_, _) => { _paused = pause.Checked; Close(); };

        var startup = new ToolStripMenuItem("Iniciar con Windows") { CheckOnClick = true, Checked = IsInStartup() };
        startup.CheckedChanged += (_, _) => SetStartup(startup.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(pause);
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "My Quick Accent — mantenga presionada una letra",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _hookProc = HookCallback;
        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _hookProc, Native.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            MessageBox.Show("No se pudo instalar el hook de teclado.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected override void ExitThreadCore()
    {
        Native.UnhookWindowsHookEx(_hook);
        _tray.Visible = false;
        _tray.Dispose();
        base.ExitThreadCore();
    }

    // ── Teclado ──────────────────────────────────────────────────────────

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_paused)
        {
            var k = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            if ((k.flags & Native.LLKHF_INJECTED) == 0) // ignora lo que esta misma app escribe
            {
                int msg = (int)wParam;
                bool down = msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
                bool swallow = down ? OnKeyDown(k.vkCode) : OnKeyUp(k.vkCode);
                if (swallow) return 1;
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool OnKeyUp(uint vk)
    {
        if (vk == _heldVk) _heldVk = 0;
        if (vk == _lastDownVk) _lastDownVk = 0;
        return false;
    }

    /// <returns>true si la tecla se debe tragar (no llega a la app).</returns>
    private bool OnKeyDown(uint vk)
    {
        if (vk == _heldVk) return true; // la letra sigue presionada: no repetir
        if (IsModifier(vk)) return false;

        if (_active)
        {
            int digit = vk is >= 0x31 and <= 0x39 ? (int)vk - 0x31
                      : vk is >= 0x61 and <= 0x69 ? (int)vk - 0x61 // teclado numérico
                      : -1;
            if (digit >= 0 && digit < _options.Length) { Choose(digit); return true; }

            switch (vk)
            {
                case Native.VK_LEFT: Move(-1); return true;
                case Native.VK_RIGHT: Move(+1); return true;
                case Native.VK_ESCAPE: Close(); return true;
                case Native.VK_RETURN or Native.VK_SPACE when _popup.Selected >= 0:
                    Choose(_popup.Selected);
                    return true;
            }
            Close(); // cualquier otra tecla cierra el popup y se escribe normal
        }

        bool shortcut = Native.IsDown(Native.VK_CONTROL) || Native.IsDown(Native.VK_MENU)
                     || Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN);

        if (!shortcut && vk < 128 && Accents.ContainsKey((char)vk))
        {
            if (vk == _lastDownVk) // segunda señal sin soltar = se dejó presionada
            {
                Open(vk);
                return true;
            }
            _lastDownVk = vk;
            bool caps = (Native.GetKeyState((int)Native.VK_CAPITAL) & 1) != 0;
            _lastDownUpper = Native.IsDown(Native.VK_SHIFT) ^ caps;
            return false; // la primera vez la letra se escribe normal
        }

        _lastDownVk = 0;
        return false;
    }

    private static bool IsModifier(uint vk) =>
        vk is Native.VK_SHIFT or Native.VK_CONTROL or Native.VK_MENU or Native.VK_CAPITAL
            or Native.VK_LWIN or Native.VK_RWIN or (>= 0xA0 and <= 0xA5);

    // ── Popup ────────────────────────────────────────────────────────────

    private void Open(uint vk)
    {
        string chars = Accents[(char)vk];
        if (_lastDownUpper) chars = chars.ToUpperInvariant();

        _options = chars.Select(c => c.ToString()).ToArray();
        _heldVk = vk;
        _lastDownVk = 0;
        _active = true;
        _target = Native.GetForegroundWindow();

        // Fuera del hook: ubicar el cursor puede tardar y el hook debe responder rápido
        _popup.BeginInvoke(ShowPopup);
    }

    private async void ShowPopup()
    {
        IntPtr target = _target;
        string[] options = _options;
        Rectangle? caret = null;
        try
        {
            caret = await Task.Run(() => CaretLocator.Find(target)).WaitAsync(TimeSpan.FromMilliseconds(400));
        }
        catch { /* tardó demasiado o falló: se usa la posición de respaldo */ }

        if (!_active || options != _options) return; // ya se eligió o se canceló mientras tanto
        _popup.ShowAt(options, caret, target);
        _watchdog.Start();
    }

    private void Move(int delta)
    {
        int next = _popup.Selected < 0 ? (delta > 0 ? 0 : _options.Length - 1) : _popup.Selected + delta;
        _popup.Selected = Math.Clamp(next, 0, _options.Length - 1);
    }

    private void Choose(int index)
    {
        if (!_active || index < 0 || index >= _options.Length) return;
        string text = _options[index];
        Close();
        _popup.BeginInvoke(() => Native.ReplaceLastChar(text));
    }

    private void Close()
    {
        _active = false;
        _watchdog.Stop();
        if (_popup.Visible) _popup.Hide();
    }

    /// <summary>Cierra el popup si se cambia de ventana o se hace clic fuera de él.</summary>
    private void Watch()
    {
        if (!_active) { _watchdog.Stop(); return; }
        bool clicking = Native.IsDown(Native.VK_LBUTTON) || Native.IsDown(Native.VK_RBUTTON);
        if (Native.GetForegroundWindow() != _target || (clicking && !_popup.Bounds.Contains(Cursor.Position)))
            Close();
    }

    // ── Bandeja del sistema ──────────────────────────────────────────────

    private static bool IsInStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) != null;
    }

    private static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using var back = new SolidBrush(Color.FromArgb(10, 132, 255));
            g.FillEllipse(back, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 21, FontStyle.Bold, GraphicsUnit.Pixel);
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("á", font, Brushes.White, new RectangleF(0, -2, 32, 32), fmt);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
