using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace bobTail.Models;

public class TailService
{
    private readonly ConcurrentDictionary<string, long> _offsets = new();

    public IEnumerable<string> LoadLastLines(string path, int maxLines)
    {
        if (!File.Exists(path))
            yield break;

        const int chunkSize = 4096;
        var fileInfo = new FileInfo(path);
        var length = fileInfo.Length;
        var buffer = new byte[chunkSize];
        var lines = new List<string>();
        var sb = new StringBuilder();

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        long position = length;
        while (position > 0 && lines.Count < maxLines)
        {
            int toRead = (int)Math.Min(chunkSize, position);
            position -= toRead;
            fs.Seek(position, SeekOrigin.Begin);
            fs.Read(buffer, 0, toRead);

            for (int i = toRead - 1; i >= 0; i--)
            {
                char c = (char)buffer[i];
                if (c == '\n')
                {
                    if (sb.Length > 0)
                    {
                        var line = ReverseString(sb.ToString());
                        lines.Add(line);
                        sb.Clear();
                        if (lines.Count >= maxLines)
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        if (sb.Length > 0 && lines.Count < maxLines)
        {
            var line = ReverseString(sb.ToString());
            lines.Add(line);
        }

        lines.Reverse();
        foreach (var line in lines)
            yield return line;
    }

    private static string ReverseString(string s)
    {
        var arr = s.ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }

    public IEnumerable<string> ReadPreviousChunk(string path, ref long offset, int maxLines = 200)
    {
        if (!File.Exists(path) || offset <= 0)
            return Array.Empty<string>();

        const int chunkSize = 4096;
        var buffer = new byte[chunkSize];
        var lines = new List<string>();
        var sb = new StringBuilder();

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        long position = offset;
        while (position > 0 && lines.Count < maxLines)
        {
            int toRead = (int)Math.Min(chunkSize, position);
            position -= toRead;
            fs.Seek(position, SeekOrigin.Begin);
            fs.Read(buffer, 0, toRead);

            for (int i = toRead - 1; i >= 0; i--)
            {
                char c = (char)buffer[i];
                if (c == '\n')
                {
                    if (sb.Length > 0)
                    {
                        var line = ReverseString(sb.ToString());
                        lines.Add(line);
                        sb.Clear();
                        if (lines.Count >= maxLines)
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        if (sb.Length > 0 && lines.Count < maxLines)
        {
            var line = ReverseString(sb.ToString());
            lines.Add(line);
        }

        lines.Reverse();
        offset = position;
        return lines;
    }

    public async IAsyncEnumerable<string> TailFile(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            yield break;

        long offset = _offsets.GetOrAdd(path, _ =>
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : 0;
        });

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(fs, Encoding.UTF8);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line != null)
            {
                offset = fs.Position;
                _offsets[path] = offset;
                yield return line;
            }
            else
            {
                await Task.Delay(50, cancellationToken);
                fs.Seek(offset, SeekOrigin.Begin);
            }
        }
    }
}
