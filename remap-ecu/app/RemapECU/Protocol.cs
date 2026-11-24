namespace RemapEcu;

public static class Protocol
{
    public const byte Stx = 0xAA;
    public const int MaxPayload = 128;

    public const byte CmdPing = 0x01;
    public const byte CmdLoopback = 0x02;
    public const byte CmdKlineInit = 0x10;
    public const byte CmdKlineSend = 0x11;
    public const byte CmdEcuId = 0x12;
    public const byte CmdSetMode = 0x20;
    public const byte CmdReadFlash = 0x21;
    public const byte CmdWriteFlash = 0x22;
    public const byte CmdChecksum = 0x23;
    public const byte CmdPowerCycle = 0x30;
    public const byte CmdModuleInfo = 0x40;
    public const byte CmdError = 0xFF;

    public const byte StOk = 0x00;
    public const byte StBadFrame = 0x01;
    public const byte StUnknown = 0x02;
    public const byte StNoEcu = 0x03;
    public const byte StTimeout = 0x04;
    public const byte StNotImpl = 0x05;
    public const byte StBusy = 0x06;

    public static byte Crc8Update(byte crc, byte b)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++)
        {
            crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
        }
        return crc;
    }

    public static byte Crc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0;
        foreach (byte b in data)
        {
            crc = Crc8Update(crc, b);
        }
        return crc;
    }

    public static byte[] BuildFrame(byte cmd, ReadOnlySpan<byte> payload)
    {
        int len = payload.Length;
        var frame = new byte[3 + len + 1];
        frame[0] = Stx;
        frame[1] = (byte)len;
        frame[2] = cmd;
        payload.CopyTo(frame.AsSpan(3));
        frame[^1] = Crc8(frame.AsSpan(1, len + 2));
        return frame;
    }

    public static string ToHex(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return "(kosong)";
        }
        return Convert.ToHexString(data);
    }
}

public sealed class FrameParser
{
    private readonly List<byte> _buf = new();
    public byte Cmd { get; private set; }
    public byte[] Payload { get; private set; } = Array.Empty<byte>();

    public bool Push(byte b)
    {
        if (_buf.Count == 0)
        {
            if (b == Protocol.Stx)
            {
                _buf.Add(b);
            }
            return false;
        }
        if (_buf.Count == 1)
        {
            if (b == 0 || b > Protocol.MaxPayload)
            {
                _buf.Clear();
                return false;
            }
            _buf.Add(b);
            return false;
        }

        _buf.Add(b);
        int expected = 3 + _buf[1] + 1;
        if (_buf.Count == expected)
        {
            byte[] payload = _buf.GetRange(3, _buf[1]).ToArray();
            byte expectedCrc = Protocol.Crc8(_buf.GetRange(1, _buf[1] + 2).ToArray());
            byte actualCrc = _buf[expected - 1];
            byte cmd = _buf[2];
            _buf.Clear();
            if (expectedCrc == actualCrc)
            {
                Cmd = cmd;
                Payload = payload;
                return true;
            }
        }
        else if (_buf.Count > expected)
        {
            _buf.Clear();
        }
        return false;
    }

    public void Reset() => _buf.Clear();
}
