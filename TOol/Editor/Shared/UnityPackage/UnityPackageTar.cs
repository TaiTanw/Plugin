using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

// =====================================================================================
// Shared / UnityPackage — gzip+tar 读写（Assets 外）。不调 AssetDatabase。
// =====================================================================================

/// <summary>一条 tar 项（相对包内路径）。</summary>
public sealed class UnityPackageTarEntry
{
    public string Name;
    public byte[] Data;
}

/// <summary>.unitypackage = gzip(ustar)。只服务预处理，不是⑥导出。</summary>
public static class UnityPackageTar
{
    const int Block = 512;

    /// <summary>解出包内全部文件项。失败抛异常。</summary>
    public static List<UnityPackageTarEntry> Read(string packagePath)
    {
        using (FileStream file = File.OpenRead(packagePath))
        using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
        using (MemoryStream tar = new MemoryStream())
        {
            gzip.CopyTo(tar);
            tar.Position = 0;
            return ReadTar(tar);
        }
    }

    /// <summary>把项写成 .unitypackage。测试与自检用。</summary>
    public static void Write(string packagePath, IList<UnityPackageTarEntry> entries)
    {
        string dir = Path.GetDirectoryName(packagePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (FileStream file = File.Create(packagePath))
        using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
        {
            WriteTar(gzip, entries);
        }
    }

    static List<UnityPackageTarEntry> ReadTar(Stream tar)
    {
        var list = new List<UnityPackageTarEntry>();
        byte[] header = new byte[Block];
        while (true)
        {
            int read = ReadExact(tar, header, 0, Block);
            if (read == 0)
            {
                break;
            }

            if (read < Block)
            {
                throw new InvalidDataException("unitypackage tar 头不完整");
            }

            if (IsZeroBlock(header))
            {
                break;
            }

            string name = ReadCString(header, 0, 100).Replace("\\", "/").TrimStart('/');
            long size = ReadOctal(header, 124, 12);
            byte type = header[156];
            if (size < 0 || size > int.MaxValue)
            {
                throw new InvalidDataException("unitypackage 条目过大: " + name);
            }

            byte[] data = new byte[(int)size];
            if (size > 0 && ReadExact(tar, data, 0, (int)size) < size)
            {
                throw new InvalidDataException("unitypackage 条目截断: " + name);
            }

            int pad = (int)((Block - (size % Block)) % Block);
            if (pad > 0)
            {
                tar.Seek(pad, SeekOrigin.Current);
            }

            if (type == (byte)'5')
            {
                continue;
            }

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            list.Add(new UnityPackageTarEntry { Name = name, Data = data });
        }

        return list;
    }

    static void WriteTar(Stream tar, IList<UnityPackageTarEntry> entries)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                UnityPackageTarEntry e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.Name))
                {
                    continue;
                }

                byte[] data = e.Data ?? Array.Empty<byte>();
                WriteHeader(tar, e.Name.Replace("\\", "/"), data.Length, (byte)'0');
                tar.Write(data, 0, data.Length);
                int pad = (Block - (data.Length % Block)) % Block;
                if (pad > 0)
                {
                    tar.Write(new byte[pad], 0, pad);
                }
            }
        }

        tar.Write(new byte[Block * 2], 0, Block * 2);
    }

    static void WriteHeader(Stream tar, string name, int size, byte typeflag)
    {
        byte[] header = new byte[Block];
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        int n = Math.Min(100, nameBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, header, 0, n);
        WriteOctal(header, 100, 8, 420);
        WriteOctal(header, 108, 8, 0);
        WriteOctal(header, 116, 8, 0);
        WriteOctal(header, 124, 12, size);
        WriteOctal(header, 136, 12, 0);
        for (int i = 148; i < 156; i++)
        {
            header[i] = (byte)' ';
        }

        header[156] = typeflag;
        byte[] magic = Encoding.ASCII.GetBytes("ustar");
        Buffer.BlockCopy(magic, 0, header, 257, magic.Length);
        header[262] = 0;
        header[263] = (byte)'0';
        header[264] = (byte)'0';

        int sum = 0;
        for (int i = 0; i < Block; i++)
        {
            sum += header[i];
        }

        string chk = Convert.ToString(sum, 8).PadLeft(6, '0');
        byte[] chkBytes = Encoding.ASCII.GetBytes(chk);
        Buffer.BlockCopy(chkBytes, 0, header, 148, Math.Min(6, chkBytes.Length));
        header[154] = 0;
        header[155] = (byte)' ';
        tar.Write(header, 0, Block);
    }

    static int ReadExact(Stream s, byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int n = s.Read(buffer, offset + total, count - total);
            if (n <= 0)
            {
                break;
            }

            total += n;
        }

        return total;
    }

    static bool IsZeroBlock(byte[] header)
    {
        for (int i = 0; i < header.Length; i++)
        {
            if (header[i] != 0)
            {
                return false;
            }
        }

        return true;
    }

    static string ReadCString(byte[] buf, int offset, int length)
    {
        int end = offset;
        int max = Math.Min(buf.Length, offset + length);
        while (end < max && buf[end] != 0)
        {
            end++;
        }

        return Encoding.UTF8.GetString(buf, offset, end - offset).Trim();
    }

    static long ReadOctal(byte[] buf, int offset, int length)
    {
        string text = ReadCString(buf, offset, length).Trim();
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        long value = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c < '0' || c > '7')
            {
                continue;
            }

            value = (value << 3) + (c - '0');
        }

        return value;
    }

    static void WriteOctal(byte[] buf, int offset, int length, long value)
    {
        string text = Convert.ToString(value, 8).PadLeft(length - 1, '0');
        if (text.Length > length - 1)
        {
            text = text.Substring(text.Length - (length - 1));
        }

        byte[] bytes = Encoding.ASCII.GetBytes(text);
        Buffer.BlockCopy(bytes, 0, buf, offset, bytes.Length);
        buf[offset + length - 1] = 0;
    }
}
