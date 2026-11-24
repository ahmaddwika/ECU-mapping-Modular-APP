namespace RemapEcu;

public sealed class MainForm : Form
{
    private readonly SerialLink _link = new();
    private readonly ComboBox _cmbPort = new();
    private readonly ComboBox _cmbBaud = new();
    private readonly Button _btnConnect = new();
    private readonly TextBox _txtLog = new();
    private readonly TextBox _txtLoopback = new();
    private readonly TextBox _txtKline = new();
    private readonly TextBox _txtTimeout = new();
    private readonly Label _lblStatus = new();

    public MainForm()
    {
        Text = "RemapECU - Honda ECU Bridge";
        Width = 760;
        Height = 560;
        MinimumSize = new Size(600, 420);
        StartPosition = FormStartPosition.CenterScreen;

        _link.FrameReceived += OnFrameReceived;
        _link.Log += OnLinkLog;

        BuildUi();
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 2) };
        top.Controls.Add(new Label { Text = "Port:", AutoSize = true, Location = new Point(8, 14) });
        _cmbPort.Location = new Point(50, 10);
        _cmbPort.Width = 110;
        top.Controls.Add(_cmbPort);

        var btnRefresh = new Button { Text = "Muati ulang", Location = new Point(166, 9), Width = 90 };
        btnRefresh.Click += (_, _) => RefreshPorts();
        top.Controls.Add(btnRefresh);

        top.Controls.Add(new Label { Text = "Baud:", AutoSize = true, Location = new Point(268, 14) });
        _cmbBaud.Location = new Point(310, 10);
        _cmbBaud.Width = 90;
        _cmbBaud.Items.AddRange(new object[] { 9600, 10400, 115200, 230400 });
        _cmbBaud.SelectedItem = 115200;
        top.Controls.Add(_cmbBaud);

        _btnConnect.Location = new Point(412, 9);
        _btnConnect.Width = 110;
        _btnConnect.Text = "Hubungkan";
        _btnConnect.Click += (_, _) => ToggleConnect();
        top.Controls.Add(_btnConnect);

        _lblStatus.Text = "Belum terhubung";
        _lblStatus.Location = new Point(528, 14);
        _lblStatus.AutoSize = true;
        _lblStatus.ForeColor = Color.Gray;
        top.Controls.Add(_lblStatus);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 120, Padding = new Padding(8) };
        bottom.Controls.Add(new Label { Text = "Loopback hex:", AutoSize = true, Location = new Point(8, 8) });
        _txtLoopback.Location = new Point(96, 5);
        _txtLoopback.Width = 200;
        bottom.Controls.Add(_txtLoopback);

        var btnLoopback = new Button { Text = "Kirim Loopback", Location = new Point(304, 4), Width = 120 };
        btnLoopback.Click += (_, _) => _link.Send(Protocol.CmdLoopback, ParseHex(_txtLoopback.Text));
        bottom.Controls.Add(btnLoopback);

        var btnPing = new Button { Text = "Ping", Location = new Point(432, 4), Width = 70 };
        btnPing.Click += (_, _) => _link.Send(Protocol.CmdPing);
        bottom.Controls.Add(btnPing);

        var btnInfo = new Button { Text = "Info Modul", Location = new Point(510, 4), Width = 90 };
        btnInfo.Click += (_, _) => _link.Send(Protocol.CmdModuleInfo);
        bottom.Controls.Add(btnInfo);

        var btnKlineInit = new Button { Text = "K-Line Init (10400)", Location = new Point(8, 36), Width = 150 };
        btnKlineInit.Click += (_, _) => _link.Send(Protocol.CmdKlineInit, new byte[] { 0x28, 0x28 });
        bottom.Controls.Add(btnKlineInit);

        bottom.Controls.Add(new Label { Text = "Timeout ms:", AutoSize = true, Location = new Point(168, 40) });
        _txtTimeout.Location = new Point(248, 37);
        _txtTimeout.Width = 50;
        _txtTimeout.Text = "200";
        bottom.Controls.Add(_txtTimeout);

        bottom.Controls.Add(new Label { Text = "K-Line hex:", AutoSize = true, Location = new Point(312, 40) });
        _txtKline.Location = new Point(382, 37);
        _txtKline.Width = 200;
        bottom.Controls.Add(_txtKline);

        var btnKlineSend = new Button { Text = "Kirim K-Line", Location = new Point(590, 36), Width = 100 };
        btnKlineSend.Click += (_, _) => SendKline();
        bottom.Controls.Add(btnKlineSend);

        _txtLog.Dock = DockStyle.Fill;
        _txtLog.Multiline = true;
        _txtLog.ReadOnly = true;
        _txtLog.ScrollBars = ScrollBars.Vertical;
        _txtLog.Font = new Font("Consolas", 9.5f);

        Controls.Add(_txtLog);
        Controls.Add(bottom);
        Controls.Add(top);

        Load += (_, _) => RefreshPorts();
    }

    private void RefreshPorts()
    {
        _cmbPort.Items.Clear();
        foreach (string p in _link.GetPorts())
        {
            _cmbPort.Items.Add(p);
        }
        if (_cmbPort.Items.Count > 0)
        {
            _cmbPort.SelectedIndex = 0;
        }
    }

    private void ToggleConnect()
    {
        if (_link.IsOpen)
        {
            _link.Close();
            _btnConnect.Text = "Hubungkan";
            _lblStatus.Text = "Belum terhubung";
            _lblStatus.ForeColor = Color.Gray;
        }
        else
        {
            string port = _cmbPort.Text;
            int baud = int.Parse(_cmbBaud.Text);
            _link.Open(port, baud);
            _btnConnect.Text = "Putuskan";
            _lblStatus.Text = $"Terhubung ke {port}";
            _lblStatus.ForeColor = Color.Green;
        }
    }

    private void SendKline()
    {
        byte[] payload = ParseHex(_txtKline.Text);
        if (payload.Length == 0)
        {
            AppendLog("Hex K-Line kosong atau format tidak valid.");
            return;
        }
        if (!int.TryParse(_txtTimeout.Text, out int ms))
        {
            ms = 200;
        }
        ms = Math.Clamp(ms, 10, 255);
        var full = new byte[payload.Length + 1];
        full[0] = (byte)ms;
        Array.Copy(payload, 0, full, 1, payload.Length);
        _link.Send(Protocol.CmdKlineSend, full);
    }

    private void OnLinkLog(object? sender, string message)
    {
        RunOnUi(() =>
        {
            if (!string.IsNullOrEmpty(message))
            {
                AppendLog(message);
            }
        });
    }

    private void OnFrameReceived(object? sender, (byte Cmd, byte[] Payload) frame)
    {
        RunOnUi(() =>
        {
            string body = InterpretResponse(frame);
            AppendLog($"RX 0x{frame.Cmd:X2}: {body}");
        });
    }

    private static string InterpretResponse((byte Cmd, byte[] Payload) frame)
    {
        byte[] p = frame.Payload;
        if (p.Length == 0)
        {
            return "(tanpa payload)";
        }
        if (p[0] is >= 0x00 and <= 0x06)
        {
            string status = StatusName(p[0]);
            if (frame.Cmd == Protocol.CmdKlineSend && p[0] == Protocol.StOk)
            {
                return $"{status}, balasan: {Protocol.ToHex(p.AsSpan(1))}";
            }
            return status;
        }
        return System.Text.Encoding.ASCII.GetString(p);
    }

    private static string StatusName(byte st) => st switch
    {
        Protocol.StOk => "OK",
        Protocol.StBadFrame => "BAD_FRAME",
        Protocol.StUnknown => "UNKNOWN",
        Protocol.StNoEcu => "NO_ECU",
        Protocol.StTimeout => "TIMEOUT",
        Protocol.StNotImpl => "NOT_IMPLEMENTED",
        Protocol.StBusy => "BUSY",
        _ => $"0x{st:X2}",
    };

    private void AppendLog(string line)
    {
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {line}{Environment.NewLine}");
        if (_txtLog.TextLength > 60000)
        {
            _txtLog.Clear();
        }
    }

    private void RunOnUi(Action action)
    {
        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private static byte[] ParseHex(string text)
    {
        string cleaned = text
            .Replace("0x", "")
            .Replace("0X", "")
            .Replace(",", "")
            .Replace(" ", "")
            .Replace("\t", "")
            .Replace("\r", "")
            .Replace("\n", "");
        if (string.IsNullOrEmpty(cleaned) || cleaned.Length % 2 != 0)
        {
            return Array.Empty<byte>();
        }
        try
        {
            return Convert.FromHexString(cleaned);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _link.Dispose();
        base.OnFormClosed(e);
    }
}
