using System.Buffers.Binary;

namespace Aerger;

public sealed class Archive
{
    private readonly byte[] data;
    private readonly byte[] index;
    public int Count => index.Length / 8;
    public Archive(string directory, string name)
    {
        data = File.ReadAllBytes(Path.Combine(directory, name + ".DAT"));
        index = File.ReadAllBytes(Path.Combine(directory, name + ".IDX"));
        if (index.Length == 0 || index.Length % 8 != 0) throw new InvalidDataException("Ungültiger Index: " + name);
        for (int i = 0; i < Count; i++) _ = Read(i);
    }
    public byte[] Read(int entry)
    {
        if (entry < 0 || entry >= Count) throw new ArgumentOutOfRangeException(nameof(entry));
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(entry * 8));
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(entry * 8 + 4));
        if ((ulong)offset + length > (ulong)data.Length) throw new InvalidDataException("Archiv-Eintrag außerhalb der Datei.");
        return data.AsSpan(checked((int)offset), checked((int)length)).ToArray();
    }
    public Bitmap Bitmap(int entry)
    {
        using var stream = new MemoryStream(Read(entry));
        using var bitmap = new Bitmap(stream);
        return new Bitmap(bitmap);
    }
}
