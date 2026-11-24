using System.IO.Ports;

namespace RemapEcu;

public sealed class SerialLink : IDisposable
{
    private SerialPort? _port;
    private readonly FrameParser _parser = new();

    public event EventHandler<(byte Cmd, byte[] Payload)>? FrameReceived;
    public event EventHandler<string>? Log;

    public bool IsOpen => _port is { IsOpen: true };

    public string[] GetPorts() => SerialPort.GetPortNames();

    public void Open(string portName, int baud)
    {
        Close();
        _port = new SerialPort(portName, baud, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 500,
        };
        _port.DataReceived += OnData;
        _port.Open();
        Log?.Invoke(this, $"Terhubung: {portName} @ {baud}");
    }

    public void Close()
    {
        if (_port != null)
        {
            _port.DataReceived -= OnData;
            if (_port.IsOpen)
            {
                _port.Close();
            }
            _port.Dispose();
            _port = null;
        }
        Log?.Invoke(this, "Terputus");
    }

    public bool Send(byte cmd, byte[]? payload = null)
    {
        if (_port is not { IsOpen: true })
        {
            return false;
        }
        payload ??= Array.Empty<byte>();
        byte[] frame = Protocol.BuildFrame(cmd, payload);
        _port.Write(frame, 0, frame.Length);
        Log?.Invoke(this, $"TX 0x{cmd:X2}: {Protocol.ToHex(payload)}");
        return true;
    }

    private void OnData(object? sender, SerialDataReceivedEventArgs e)
    {
        var port = _port;
        if (port == null)
        {
            return;
        }
        try
        {
            while (port.BytesToRead > 0)
            {
                int b = port.ReadByte();
                if (b < 0)
                {
                    break;
                }
                if (_parser.Push((byte)b))
                {
                    FrameReceived?.Invoke(this, (_parser.Cmd, _parser.Payload));
                }
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke(this, $"Serial error: {ex.Message}");
        }
    }

    public void Dispose() => Close();
}
