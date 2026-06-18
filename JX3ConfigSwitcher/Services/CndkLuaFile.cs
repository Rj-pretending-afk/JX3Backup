using System;
using System.IO;
using System.Linq;
using System.Text;

namespace JX3ConfigSwitcher.Services;

public sealed class CndkLuaFile
{
    private static readonly byte[] Magic = { 0x43, 0x4E, 0x44, 0x4B };
    private static readonly Encoding PayloadEncoding = Encoding.Latin1;

    public string ReadPayloadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 16 || !bytes.Take(4).SequenceEqual(Magic))
        {
            throw new InvalidDataException($"不是有效的 CNDK Lua 配置文件：{path}");
        }

        return PayloadEncoding.GetString(bytes, 16, bytes.Length - 16);
    }

    public void WritePayloadText(string path, string text)
    {
        var payload = PayloadEncoding.GetBytes(text);
        var crc = ComputeCrc32(payload);
        using var stream = File.Create(path);
        stream.Write(Magic);
        WriteUInt32(stream, crc);
        WriteUInt32(stream, (uint)payload.Length);
        WriteUInt32(stream, (uint)payload.Length);
        stream.Write(payload);
    }

    public void ValidateHeader(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 16 || !bytes.Take(4).SequenceEqual(Magic))
        {
            throw new InvalidDataException($"不是有效的 CNDK Lua 配置文件：{path}");
        }

        var storedCrc = BitConverter.ToUInt32(bytes, 4);
        var length1 = BitConverter.ToUInt32(bytes, 8);
        var length2 = BitConverter.ToUInt32(bytes, 12);
        var payload = bytes.Skip(16).ToArray();
        var actualCrc = ComputeCrc32(payload);

        if (length1 != payload.Length || length2 != payload.Length || storedCrc != actualCrc)
        {
            throw new InvalidDataException($"CNDK 头校验失败：{path}");
        }
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static uint ComputeCrc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
