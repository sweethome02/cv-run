# cv-run

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

> 轻量级 WPF 粘贴板工具，支持文本/图片自动捕获、边缘吸附、快捷键呼出。

## ✨ 特性

| 功能 | 说明 |
|------|------|
| 📋 自动捕获 | 监听剪贴板变更，自动保存文本和图片 |
| ⌨️ 快捷键呼出 | `Ctrl+`` (反引号) 快速弹出/隐藏面板 |
| 📌 固定记录 | 右键固定重要内容，不被自动清理 |
| 🔍 全文搜索 | 实时过滤文本剪贴板记录 |
| 🖱 边缘吸附 | 面板可吸附到屏幕左/右/上三边 |
| 🎯 悬浮指示器 | 面板隐藏后显示半透明指示器，点击/拖拽操作 |
| 💾 本地持久化 | 使用 SQLite 数据库存储历史记录 |
| 🖼 图片支持 | 自动捕获剪贴板图片，PNG 编码存储 |
| 🔄 开机自启 | 支持开机自动启动 |
| 📝 日志系统 | 按日记录操作日志，自动清理 7 天前文件 |
| 🎨 深色 UI | 深色主题，缩放动画过渡 |

## 🏗 架构

```
cv-run/
├── App.xaml(.cs)          # 应用入口，系统托盘、剪贴板监听、全局热键
├── MainWindow.xaml(.cs)   # 主面板窗口，列表展示、搜索、吸附逻辑
├── IndicatorWindow.xaml(.cs)  # 悬浮指示器，点击/拖拽交互
├── Models.cs              # 数据模型 (ClipboardItem)
├── Storage.cs             # SQLite 持久化层
├── Logger.cs              # 日志工具 (按日记录 + 自动清理)
├── NativeMethods.cs       # Windows API 封装 (热键/剪贴板监听/窗口)
└── .gitignore
```

### 核心流程

```
[剪贴板变更] → WM_CLIPBOARDUPDATE → App.ReadClipboard()
    ├── 文本 → 保存文本内容
    └── 图片 → PNG 编码 → Base64 存储

[用户呼出] → Ctrl+` → App.TogglePanel()
    ├── 悬浮态 → 弹出主面板 + 缩回动画
    └── 吸附态 → 弹出面板 → 隐藏指示器

[面板关闭] → 鼠标离开 / 拖离边缘
    ├── 吸附 → 缩回指示器 (左/右/上三边)
    └── 悬浮 → 面板保持
```

## 🛠 技术栈

| 技术 | 用途 |
|------|------|
| **.NET 8.0** | 运行时框架 |
| **WPF** | UI 框架 |
| **SQLite** | 本地数据存储 |
| **Windows API** | 剪贴板监听、全局热键、窗口管理 |

## 📦 依赖

- Microsoft.Data.Sqlite (10.0.8)

## 🚀 快速开始

### 环境要求

- Windows 10/11
- .NET 8.0 SDK 或运行时

### 构建

```bash
cd ClipNestWpf
dotnet restore
dotnet build -c Release
```

### 运行

```bash
dotnet run
```

或发布后直接运行：

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
.\publish\ClipNestWpf.exe
```

## 📖 使用方法

### 基本操作

1. **自动捕获** — 应用启动后自动监听剪贴板，复制文本或截图后自动保存
2. **呼出面板** — 按 `Ctrl+`` 或点击屏幕边缘指示器
3. **粘贴内容** — 双击列表项，自动粘贴到当前焦点
4. **搜索** — 在搜索框输入文字，实时过滤文本记录
5. **固定记录** — 右键某条记录 → 固定，不被自动清理
6. **删除** — 右键 → 删除，或点击列表项右侧 ✕ 按钮
7. **清空** — 点击右上角"清空"按钮，清除所有未固定记录

### 吸附模式

- **拖拽面板到屏幕边缘** → 自动吸附（左/右/上三边可选）
- **拖拽指示器** → 沿边缘移动吸附位置
- **拖拽指示器远离边缘** → 切换到悬浮模式

## 📁 数据目录

应用运行目录下的 `data/` 文件夹存储所有数据：

```
data/
├── clipnest.db          # SQLite 数据库
├── clipnest-2025-01-15.log   # 当日操作日志
├── clipnest-error-2025-01-15.log  # 错误日志
```

> 日志文件自动清理 7 天前的记录。

## 🔧 开发

### 窗口行为

| 属性 | 默认值 | 说明 |
|------|--------|------|
| 面板宽度 | 400px | 最大 800px |
| 面板高度 | 540px | 最大 900px |
| 剪贴板缓存上限 | 500 条 | 超出自动删除旧记录 |
| 吸附距离 | 40px | 窗口中心距边缘此距离内自动吸附 |
| 指示器尺寸 | 32×64px | 悬浮时 0.8 缩放，hover 时 1.0 缩放 |

### 热键

| 热键 | 功能 |
|------|------|
| `Ctrl+`` | 切换面板显示/隐藏 |

## 📄 License

MIT
