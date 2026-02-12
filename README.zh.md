# TFWR.CS

面向《编程农场》（The Farmer Was Replaced）游戏的 C# → 类 Python 代码自动转译工具。

## 快速开始

### 使用方法

1. 从 [Releases](https://github.com/Ocrosoft/TFWR.CS/releases) 下载 `SampleProject.zip`
2. 在游戏中，点击右上角打开信息窗口，进入 **"外部编辑器"** 帮助页，点击 **"存档文件夹"** 按钮打开存档目录
3. 将 `SampleProject.zip` 解压到对应的存档目录（如 `Save0`）
4. 用 IDE 打开 `cs\SampleProject.sln`，即可获得完整的代码补全与类型提示
5. 双击 `TFWR.CS\TFWR.CS.exe` 运行转译器（需要 [.NET 9 运行时](https://dotnet.microsoft.com/download/dotnet/9.0)）
6. 转译器会自动监控 `cs\` 目录下的 `.cs` 文件变化，实时将 `.py` 文件生成到存档根目录

> 解压后的目录结构：
> ```
> Save0/
> ├── TFWR.CS/       # 转译器
> │   └── TFWR.CS.exe         # 双击运行
> ├── cs/        # C# 源码（在这里写代码）
> │   ├── Program.cs
> │   ├── SampleProject.csproj
> │   ├── SampleProject.sln
> │   ├── TFWR.CS.REF.dll     # 游戏 API 定义（提供代码补全）
> │   └── TFWR.CS.REF.xml     # API 文档注释
> └── *.py        # 转译器自动生成的脚本（游戏读取）
> ```

### 从源码构建

```bash
git clone https://github.com/Ocrosoft/TFWR.CS.git
cd TFWR.CS
dotnet build
```

## 集合表达式 `[]` 的类型推断

转译器没有完整的语义模型，对集合表达式 `[]` 的类型推断有限：

| 场景 | 是否支持 | 示例 |
|---|---|---|
| 变量声明初始化 | ✅ | `List<int> x = [];` |
| 简单标识符赋值 | ✅ | `x = [];`（x 在当前作用域已声明） |
| 方法返回值 | ✅ | `return [];`（从方法签名推断） |
| 方法参数 | ❌ | `Method([]);` → 请改用 `Method(new List<int>())` |
| 属性赋值 | ❌ | `obj.Prop = [];` → 请改用 `obj.Prop = new()` |
| 复杂表达式 | ❌ | `obj.Method().Prop = [];` |

无法推断类型时默认转换为列表 `[]` 并输出警告。

## ❌ 不支持的语法和特性

以下语法会被**忽略或产生错误**，请避免使用：

- **类型系统**：继承、接口、抽象类、sealed、枚举、泛型约束、record、struct（当作普通类处理，值语义不保留）
- **高级语法**：Lambda、LINQ、三元表达式 `? :`、`??`、`?.`、模式匹配（switch 表达式 / is 模式）、范围运算符 `..` / `^`
- **异常处理**：try-catch-finally、throw
- **异步**：async/await、Task
- **类成员**：Properties 的 get/set 逻辑（自动属性可转换）、索引器、运算符重载、扩展方法、委托、事件
- **参数修饰**：ref/out（仅语法转译）、in、params、命名参数
- **其他**：Attributes（忽略）、预处理指令、命名空间（忽略）、using 指令（忽略）、partial 类、yield return、dynamic、匿名类型

## 注意事项

- 本项目仍处于开发阶段，仅供学习与娱乐
- 不支持调试 C# 代码，请在游戏内对生成代码进行调试
- 不要忽略列表读取方法的返回值，否则游戏会报错
- 避免使用 `List` 的带参构造函数
- **元组返回值不支持通过命名字段访问**（如 `ret.x`），必须使用解构赋值：
  ```csharp
  // ❌ 不支持
  var ret = GetPosition();
  var px = ret.x;

  // ✅ 正确
  var (x, y) = GetPosition();
  ```

## 免责声明

本项目为个人开发，与《编程农场》官方无任何关联。请勿将本项目用于商业用途。
