# 应用栏

一个轻量的 Windows 11 置顶应用快捷面板。它由一个小圆形入口按钮和一个 5 列 × 2 行的浅色面板组成，可随时打开常用桌面应用，而不需要先退回桌面。

## 使用

1. 双击 `dist\AppBar\AppBar.exe` 启动。
2. 点击左上角圆形按钮展开面板。
3. 点击面板右上角的加号，或直接把 `.lnk` 快捷方式和 `.exe` 程序拖入面板。
4. 点击应用图标启动；拖动图标可调整顺序。
5. 鼠标离开面板和按钮约 300 毫秒后，面板自动收起。
6. 拖动圆形按钮可移动到任意显示器的工作区。
7. 右键圆形按钮可添加应用、切换开机启动或退出。右键图标可从应用栏移除，原始快捷方式不会被删除。

## 系统要求

- Windows 10 或 Windows 11。
- .NET Framework 4.8 运行时。目标系统已安装该运行时，不需要联网。
- 首版只支持传统桌面 `.lnk` 和 `.exe`，不支持 Microsoft Store/UWP 应用、脚本或普通文档。

## 数据位置

- 配置：`%LocalAppData%\AppBar\state.json`
- 图标缓存：`%LocalAppData%\AppBar\icons`
- 写入配置时使用临时文件和替换操作；损坏的旧配置会被保留为 `state.json.corrupt-*.json`。

## 构建与测试

在项目根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

脚本使用系统自带的 .NET Framework MSBuild，编译 WPF 应用、运行无第三方依赖的自动测试，并生成便携目录 `dist\AppBar`。

进程级冒烟测试：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1
```

## 技术结构

- `src/AppBar`：WPF 窗口、浅色圆角面板、DPI/多屏定位、拖放与动画。
- `src/AppBar.Core`：快捷方式解析、状态保存、启动、开机启动和布局计算。
- `tests/AppBar.Tests`：核心服务与纯布局逻辑测试。

## 已知限制

- 独占全屏 DirectX 应用可能覆盖普通置顶窗口。
- 目标程序和快捷方式都被移动或删除后，应用无法启动，面板会显示明确提示。
- 便携目录移动后，需要再运行一次应用或重新切换开机启动，以更新启动项路径。
