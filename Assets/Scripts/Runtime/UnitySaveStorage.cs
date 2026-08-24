// UnitySaveStorage.cs — ISaveStorage 的 Unity 实现：Application.persistentDataPath + 原子写。
using System;
using System.IO;
using System.Linq;
using Weiguang.Core;

namespace Weiguang.Runtime
{
    public class UnitySaveStorage : ISaveStorage
    {
        public void WriteAllText(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }
        public string ReadAllText(string path) => File.ReadAllText(path);
        public bool Exists(string path) => File.Exists(path);
        public void Move(string from, string to)
        {
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
        }
        public void Delete(string path) { if (File.Exists(path)) File.Delete(path); }
        public string[] ListFiles(string dir, string pattern)
        {
            if (!Directory.Exists(dir)) return new string[0];
            return Directory.GetFiles(dir, pattern).OrderBy(p => p).ToArray();
        }
    }

    // 轻量 CSV 解析（表头→字典行）。MVP 数据量小，不做引号/转义全量支持。
    public static class SimpleCsv
    {
        public static System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> Parse(string text)
        {
            var rows = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return rows;
            var header = lines[0].Split(',');
            for (int i = 1; i < lines.Length; i++)
            {
                var cells = lines[i].Split(',');
                var row = new System.Collections.Generic.Dictionary<string, string>();
                for (int j = 0; j < header.Length && j < cells.Length; j++) row[header[j].Trim()] = cells[j].Trim();
                rows.Add(row);
            }
            return rows;
        }
    }
}
