using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace PCL.Aurora.Application;

internal static class MinecraftServerListCodec
{
    private const int MaximumServers = 10_000;
    private const int MaximumCollectionLength = 1_000_000;
    private const int MaximumDepth = 32;

    public static IReadOnlyList<MinecraftServerEntry> Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var file = File.OpenRead(path);
        using var payload = OpenPayload(file);
        using var reader = new BinaryReader(payload, Encoding.UTF8, leaveOpen: false);
        if (reader.ReadByte() != 10)
        {
            throw new InvalidDataException("servers.dat 的根标签不是 NBT Compound。");
        }

        _ = ReadString(reader);
        var result = new List<MinecraftServerEntry>();
        while (true)
        {
            var type = reader.ReadByte();
            if (type == 0)
            {
                break;
            }

            var name = ReadString(reader);
            if (type == 9 && string.Equals(name, "servers", StringComparison.Ordinal))
            {
                ReadServerList(reader, result);
            }
            else
            {
                SkipPayload(reader, type, 0);
            }
        }

        return result;
    }

    public static void Write(string path, IReadOnlyList<MinecraftServerEntry> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        if (servers.Count > MaximumServers || servers.Any(server => !server.IsValid))
        {
            throw new InvalidDataException("服务器列表数量或字段无效。");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + $".{Guid.NewGuid():N}.partial";
        try
        {
            using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
            using (var writer = new BinaryWriter(gzip, Encoding.UTF8, leaveOpen: false))
            {
                writer.Write((byte)10);
                WriteString(writer, string.Empty);
                writer.Write((byte)9);
                WriteString(writer, "servers");
                writer.Write((byte)10);
                WriteInt32(writer, servers.Count);
                foreach (var server in servers)
                {
                    WriteStringTag(writer, "name", server.Name);
                    WriteStringTag(writer, "ip", server.Address);
                    if (!string.IsNullOrWhiteSpace(server.Icon))
                    {
                        WriteStringTag(writer, "icon", server.Icon);
                    }
                    if (server.AcceptTextures is { } acceptTextures)
                    {
                        WriteByteTag(writer, "acceptTextures", acceptTextures);
                    }
                    if (server.Hidden)
                    {
                        WriteByteTag(writer, "hidden", true);
                    }
                    writer.Write((byte)0);
                }
                writer.Write((byte)0);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            TryDeleteFile(temporaryPath);
            throw;
        }
    }

    private static Stream OpenPayload(FileStream file)
    {
        Span<byte> header = stackalloc byte[2];
        var count = file.Read(header);
        file.Position = 0;
        return count == 2 && header[0] == 0x1F && header[1] == 0x8B
            ? new GZipStream(file, CompressionMode.Decompress)
            : file;
    }

    private static void ReadServerList(BinaryReader reader, ICollection<MinecraftServerEntry> result)
    {
        var elementType = reader.ReadByte();
        var count = ReadInt32(reader);
        if (elementType != 10 || count is < 0 or > MaximumServers)
        {
            throw new InvalidDataException("servers.dat 的服务器列表无效。");
        }

        for (var index = 0; index < count; index++)
        {
            var name = string.Empty;
            var address = string.Empty;
            string? icon = null;
            bool? acceptTextures = null;
            var hidden = false;
            while (true)
            {
                var type = reader.ReadByte();
                if (type == 0)
                {
                    break;
                }

                var tagName = ReadString(reader);
                switch (type, tagName)
                {
                    case (8, "name"):
                        name = ReadString(reader);
                        break;
                    case (8, "ip"):
                        address = ReadString(reader);
                        break;
                    case (8, "icon"):
                        icon = ReadString(reader);
                        break;
                    case (1, "acceptTextures"):
                        acceptTextures = reader.ReadByte() != 0;
                        break;
                    case (1, "hidden"):
                        hidden = reader.ReadByte() != 0;
                        break;
                    default:
                        SkipPayload(reader, type, 1);
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(address))
            {
                result.Add(new(name, address, icon, acceptTextures, hidden));
            }
        }
    }

    private static void SkipPayload(BinaryReader reader, byte type, int depth)
    {
        if (depth > MaximumDepth)
        {
            throw new InvalidDataException("NBT 嵌套层级过深。");
        }

        switch (type)
        {
            case 1:
                _ = reader.ReadByte();
                break;
            case 2:
                ReadExactly(reader, 2);
                break;
            case 3:
            case 5:
                ReadExactly(reader, 4);
                break;
            case 4:
            case 6:
                ReadExactly(reader, 8);
                break;
            case 7:
                ReadExactly(reader, ReadBoundedLength(reader, 1));
                break;
            case 8:
                _ = ReadString(reader);
                break;
            case 9:
            {
                var elementType = reader.ReadByte();
                var count = ReadBoundedLength(reader, 1);
                for (var index = 0; index < count; index++)
                {
                    SkipPayload(reader, elementType, depth + 1);
                }
                break;
            }
            case 10:
                while (true)
                {
                    var childType = reader.ReadByte();
                    if (childType == 0)
                    {
                        break;
                    }
                    _ = ReadString(reader);
                    SkipPayload(reader, childType, depth + 1);
                }
                break;
            case 11:
                ReadExactly(reader, checked(ReadBoundedLength(reader, 4) * 4));
                break;
            case 12:
                ReadExactly(reader, checked(ReadBoundedLength(reader, 8) * 8));
                break;
            default:
                throw new InvalidDataException($"不支持的 NBT 标签类型：{type}。");
        }
    }

    private static int ReadBoundedLength(BinaryReader reader, int elementSize)
    {
        var count = ReadInt32(reader);
        if (count < 0 || count > MaximumCollectionLength || count > int.MaxValue / elementSize)
        {
            throw new InvalidDataException("NBT 集合长度无效。");
        }
        return count;
    }

    private static string ReadString(BinaryReader reader)
    {
        Span<byte> lengthBytes = stackalloc byte[2];
        ReadExactly(reader, lengthBytes);
        var length = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException();
        }
        return Encoding.UTF8.GetString(bytes);
    }

    private static int ReadInt32(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[4];
        ReadExactly(reader, bytes);
        return BinaryPrimitives.ReadInt32BigEndian(bytes);
    }

    private static void WriteStringTag(BinaryWriter writer, string name, string value)
    {
        writer.Write((byte)8);
        WriteString(writer, name);
        WriteString(writer, value);
    }

    private static void WriteByteTag(BinaryWriter writer, string name, bool value)
    {
        writer.Write((byte)1);
        WriteString(writer, name);
        writer.Write(value ? (byte)1 : (byte)0);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new InvalidDataException("NBT 字符串过长。");
        }
        Span<byte> lengthBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(lengthBytes, (ushort)bytes.Length);
        writer.Write(lengthBytes);
        writer.Write(bytes);
    }

    private static void WriteInt32(BinaryWriter writer, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        writer.Write(bytes);
    }

    private static void ReadExactly(BinaryReader reader, int length)
    {
        var buffer = new byte[Math.Min(length, 81920)];
        var remaining = length;
        while (remaining > 0)
        {
            var read = reader.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                throw new EndOfStreamException();
            }
            remaining -= read;
        }
    }

    private static void ReadExactly(BinaryReader reader, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = reader.Read(buffer[offset..]);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }
            offset += read;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
