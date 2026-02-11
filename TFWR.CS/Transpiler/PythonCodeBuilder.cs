using System.Text;

namespace TFWR.CS.Transpiler;

/// <summary>
/// 辅助构建器，用于生成 Python 代码并管理缩进。
/// </summary>
public class PythonCodeBuilder
{
    private readonly StringBuilder _sb = new();
    private readonly List<string> _pendingImports = [];
    private readonly List<string> _helperFunctions = [];
    private int _indentLevel;
    private const string IndentUnit = "\t";
    private bool _lastWasBlank;

    public void IncreaseIndent() => _indentLevel++;

    public void DecreaseIndent()
    {
        if (_indentLevel > 0) _indentLevel--;
    }

    public void AppendLine(string line)
    {
        _lastWasBlank = false;
        _sb.Append(GetIndent());
        _sb.AppendLine(line);
    }

    public void AppendBlankLine()
    {
        if (!_lastWasBlank)
        {
            _sb.AppendLine();
            _lastWasBlank = true;
        }
    }

    /// <summary>追加一行不带缩进的原始文本（用于单独 import 行）</summary>
    public void AppendRawLine(string line)
    {
        _lastWasBlank = false;
        _sb.AppendLine(line);
    }

    /// <summary>在现有的 import 行之后插入新的 import 行（去重）</summary>
    public void InsertImportAfterExisting(string importLine)
    {
        if (!_pendingImports.Contains(importLine))
            _pendingImports.Add(importLine);
    }

    /// <summary>插入辅助函数（在 import 之后、其他代码之前）</summary>
    public void InsertHelperFunction(string functionCode)
    {
        if (!_helperFunctions.Contains(functionCode))
            _helperFunctions.Add(functionCode);
    }

    private string GetIndent()
    {
        return string.Concat(Enumerable.Repeat(IndentUnit, _indentLevel));
    }

    public override string ToString()
    {
        var content = _sb.ToString();
        var lines = content.Split('\n').ToList();

        // 找到最后一个 import/from 行的位置
        int lastImportIdx = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("import ") || trimmed.StartsWith("from "))
                lastImportIdx = i;
        }

        var insertIdx = lastImportIdx + 1; // 0 if no imports found

        // 插入 imports
        foreach (var imp in _pendingImports)
        {
            // 检查是否已存在
            if (!lines.Any(l => l.Trim() == imp))
            {
                lines.Insert(insertIdx, imp);
                insertIdx++;
            }
        }

        // 插入辅助函数（在 import 之后）
        if (_helperFunctions.Count > 0)
        {
            // 如果有 import，在 import 后插入空行
            if (_pendingImports.Count > 0)
            {
                lines.Insert(insertIdx, "");
                insertIdx++;
            }

            foreach (var helper in _helperFunctions)
            {
                var helperLines = helper.TrimEnd().Split('\n');
                foreach (var line in helperLines)
                {
                    lines.Insert(insertIdx, line);
                    insertIdx++;
                }
                lines.Insert(insertIdx, "");
                insertIdx++;
            }
        }

        return string.Join("\n", lines).TrimEnd() + "\n";
    }
}
