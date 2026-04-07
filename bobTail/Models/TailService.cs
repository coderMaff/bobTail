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
    private readonly ConcurrentDictionary<string, Encoding> _encodingCache = new();

    private Encoding DetectEncoding(string path)
    {
        if (_encodingCache.TryGetValue(path, out var cached))
            return cached;

        var encoding = Encoding.UTF8;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[4];
            if (fs.Read(buffer, 0, 4) >= 2)
            {
                // Check UTF-16 LE BOM
                if (buffer[0] == 0xFF && buffer[1] == 0xFE && (buffer[2] != 0 || buffer[3] != 0))
                    encoding = Encoding.Unicode; // UTF-16LE
                // Check UTF-16 BE BOM
                else if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    encoding = Encoding.BigEndianUnicode; // UTF-16BE
                // Check UTF-8 BOM
                else if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    encoding = Encoding.UTF8;
            }
        }
        catch
        {
            // Default to UTF8 if detection fails
        }

        _encodingCache[path] = encoding;
        return encoding;
    }

    private static string StripBOM(string text)
    {
        // Remove any leading BOM character (U+FEFF)
        while (text.Length > 0 && text[0] == '\ufeff')
            text = text.Substring(1);
        return text;
    }

    public IEnumerable<string> LoadLastLines(string path, int maxLines)
    {
        if (!File.Exists(path))
            yield break;

        var encoding = DetectEncoding(path);
        const int chunkSize = 4096;
        var fileInfo = new FileInfo(path);
        var length = fileInfo.Length;
        var buffer = new byte[chunkSize];
        var lines = new List<string>();
        var lineBuffer = new List<byte>();

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        long position = length;
        while (position > 0 && lines.Count < maxLines)
        {
            int toRead = (int)Math.Min(chunkSize, position);
            position -= toRead;
            fs.Seek(position, SeekOrigin.Begin);
            fs.ReadExactly(buffer, 0, toRead);

            for (int i = toRead - 1; i >= 0; i--)
            {
                lineBuffer.Insert(0, buffer[i]);
                
                // Look for newline character(s) based on encoding
                bool isNewline = false;
                if (encoding == Encoding.Unicode)
                {
                    // UTF-16LE: \n is 0x0A 0x00
                    if (lineBuffer.Count >= 2 && lineBuffer[0] == 0x0A && lineBuffer[1] == 0x00)
                        isNewline = true;
                }
                else
                {
                    // UTF-8 or others: \n is 0x0A
                    if (lineBuffer.Count >= 1 && lineBuffer[0] == 0x0A)
                        isNewline = true;
                }

                if (isNewline)
                {
                    if (lineBuffer.Count > (encoding == Encoding.Unicode ? 2 : 1))
                    {
                        // Remove newline bytes
                        if (encoding == Encoding.Unicode)
                            lineBuffer.RemoveRange(0, 2);
                        else
                            lineBuffer.RemoveAt(0);

                        var lineStr = encoding.GetString(lineBuffer.ToArray());
                        if (!string.IsNullOrEmpty(lineStr))
                        {
                            lines.Add(lineStr);
                            if (lines.Count >= maxLines)
                                break;
                        }
                    }
                    lineBuffer.Clear();
                }
            }
        }

        // Don't forget remaining bytes as last line
        if (lineBuffer.Count > 0 && lines.Count < maxLines)
        {
            var lineStr = StripBOM(encoding.GetString(lineBuffer.ToArray()));
            if (!string.IsNullOrEmpty(lineStr))
                lines.Add(lineStr);
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

        var encoding = DetectEncoding(path);
        const int chunkSize = 4096;
        var buffer = new byte[chunkSize];
        var lines = new List<string>();
        var lineBuffer = new List<byte>();

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        long position = offset;
        while (position > 0 && lines.Count < maxLines)
        {
            int toRead = (int)Math.Min(chunkSize, position);
            position -= toRead;
            fs.Seek(position, SeekOrigin.Begin);
            fs.ReadExactly(buffer, 0, toRead);

            for (int i = toRead - 1; i >= 0; i--)
            {
                lineBuffer.Insert(0, buffer[i]);
                
                // Look for newline character(s) based on encoding
                bool isNewline = false;
                if (encoding == Encoding.Unicode)
                {
                    // UTF-16LE: \n is 0x0A 0x00
                    if (lineBuffer.Count >= 2 && lineBuffer[0] == 0x0A && lineBuffer[1] == 0x00)
                        isNewline = true;
                }
                else
                {
                    // UTF-8 or others: \n is 0x0A
                    if (lineBuffer.Count >= 1 && lineBuffer[0] == 0x0A)
                        isNewline = true;
                }

                if (isNewline)
                {
                    if (lineBuffer.Count > (encoding == Encoding.Unicode ? 2 : 1))
                    {
                        // Remove newline bytes
                        if (encoding == Encoding.Unicode)
                            lineBuffer.RemoveRange(0, 2);
                        else
                            lineBuffer.RemoveAt(0);

                        var lineStr = encoding.GetString(lineBuffer.ToArray());
                        if (!string.IsNullOrEmpty(lineStr))
                        {
                            lines.Add(lineStr);
                            if (lines.Count >= maxLines)
                                break;
                        }
                    }
                    lineBuffer.Clear();
                }
            }
        }

        // Don't forget remaining bytes as last line
        if (lineBuffer.Count > 0 && lines.Count < maxLines)
        {
            var lineStr = encoding.GetString(lineBuffer.ToArray());
            if (!string.IsNullOrEmpty(lineStr))
                lines.Add(lineStr);
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

        var encoding = DetectEncoding(path);

        long offset = _offsets.GetOrAdd(path, _ =>
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : 0;
        });

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(fs, encoding);

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
