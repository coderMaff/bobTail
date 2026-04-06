using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace bobTail.Models;

public class TailService
{
    private const int ChunkSize = 8192;

    public async IAsyncEnumerable<string> TailFile(string path)
    {
        using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        using var reader = new StreamReader(fs, Encoding.UTF8);

        fs.Seek(0, SeekOrigin.End);

        while (true)
        {
            string? line = await reader.ReadLineAsync();

            if (line != null)
                yield return line;
            else
                await Task.Delay(150);
        }
    }

    public List<string> LoadLastLines(string path, int maxLines)
    {
        var result = new List<string>();

        using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        long offset = fs.Length;
        var buffer = new List<string>();

        while (offset > 0 && buffer.Count < maxLines)
        {
            long readSize = Math.Min(ChunkSize, offset);
            long newOffset = offset - readSize;

            fs.Seek(newOffset, SeekOrigin.Begin);

            byte[] chunk = new byte[readSize];
            fs.ReadExactly(chunk, 0, (int)readSize);

            string text = Encoding.UTF8.GetString(chunk);
            var lines = text.Split('\n', StringSplitOptions.None);

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (buffer.Count >= maxLines)
                    break;

                if (!string.IsNullOrWhiteSpace(lines[i]))
                    buffer.Add(lines[i]);
            }

            offset = newOffset;
        }

        buffer.Reverse();
        return buffer;
    }

    public List<string> ReadPreviousChunk(string path, ref long offset)
    {
        var result = new List<string>();

        using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (offset <= 0)
            return result;

        long readSize = Math.Min(ChunkSize, offset);
        long newOffset = offset - readSize;

        fs.Seek(newOffset, SeekOrigin.Begin);

        byte[] buffer = new byte[readSize];
        fs.ReadExactly(buffer, 0, (int)readSize);

        offset = newOffset;

        string chunk = Encoding.UTF8.GetString(buffer);
        var lines = chunk.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        result.AddRange(lines);
        return result;
    }
}
