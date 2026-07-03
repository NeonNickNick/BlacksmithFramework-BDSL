# BlacksmithFramework-BDSL

[![VS Code](https://img.shields.io/badge/VS%20Code-%5E1.125.0-blue)](https://code.visualstudio.com/)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)

专为 **Blacksmith DSL**（`.bdsl`）设计的 VS Code 扩展，该语言是为 [Blacksmith-Core](https://github.com/NeonNickNick/BlacksmithFramework-Core.git) 游戏项目设计的领域特定语言。无需编写代码即可实现职业技能。

## 功能

- **语法高亮** — 对关键字、字符串、数字、运算符、注释和块结构进行完整的 Token 着色
- **保存时自动格式化** — C++/C# 风格的缩进，可配置缩进宽度
- **语法校验** — 通过内置的 C# 原生校验器（Windows x64）进行实时错误报告
- **注释切换** — 支持 `//` 单行注释和 `/* */` 多行注释

## 待支持功能

- **编译** — 将.bdsl文件编译为.cs文件

## 语言概览

BDSL 语法参考了 C++ 和 Python，简洁且无需分号。所有内容都可以写在同一行 —— 没有缩进约束。设计原则是**低自由度、写法唯一**。

```bdsl
[skillpackagedefinition]("职业名")->{

    [skill]("技能名")->{
        [check]()->{
            <require> Iron >= 3
        }
        [declare]()->{
            // 技能逻辑
        }
    }

    [skill]("auto")->{
        [declare]()->{
            // 被动技能逻辑
        }
    }
}
```

完整的语言规范请参见 [BDSLSyntax.md](BDSLSyntax.md)。

## 环境要求

- **VS Code** `>= 1.125.0`
- **Windows x64** — 内置的 `bdsl-validator.exe` 编译目标为 `win-x64`

## 扩展设置

对 `.bdsl` 文件应用的默认设置：

| 设置项 | 值 |
|---------|-------|
| `editor.formatOnSave` | `true` |
| `editor.insertSpaces` | `true` |
| `editor.tabSize` | `4` |

## 更新日志

详见 [CHANGELOG.md](CHANGELOG.md)。

### 0.0.1

初始版本，包含语法高亮、格式化和语法校验功能。
