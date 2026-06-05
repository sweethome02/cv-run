# cv-run

[English](#english-version) | 简体中文

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
├── App.xaml(.cs)              # 应用入口，系统托盘、剪贴板监听、全局热键
├── MainWindow.xaml(.cs)       # 主面板窗口，列表展示、搜索、吸附逻辑
├── IndicatorWindow.xaml(.cs)  # 悬浮指示器，点击/拖拽交互
├── Models.cs                  # 数据模型 (ClipboardItem)
├── Storage.cs                 # SQLite 持久化层
├── Logger.cs                  # 日志工具 (按日记录 + 自动清理)
├── NativeMethods.cs           # Windows API 封装 (热键/剪贴板监听/窗口)
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
├── clipnest.db              # SQLite 数据库
├── clipnest-2025-01-15.log      # 当日操作日志
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

---

## English Version

A lightweight WPF clipboard manager with auto capture, edge snapping, and keyboard shortcut support.

## ✨ Features

| Feature | Description |
|---------|-------------|
| 📋 Auto Capture | Monitors clipboard changes and automatically saves text and images |
| ⌨️ Keyboard Shortcut | `Ctrl+`` (backtick) to quickly toggle the panel |
| 📌 Pin Records | Right-click to pin important items, immune to auto-cleanup |
| 🔍 Full-text Search | Real-time filtering of text clipboard records |
| 🖱 Edge Snapping | Panel can dock to the left, right, or top screen edge |
| 🎯 Floating Indicator | Semi-transparent indicator shown when panel is hidden, click/drag to interact |
| 💾 Local Persistence | SQLite database for history storage |
| 🖼 Image Support | Auto-captures clipboard images, stored as PNG-encoded Base64 |
| 🔄 Auto-start | Supports startup on Windows login |
| 📝 Logging System | Daily operation logs with 7-day auto-cleanup |
| 🎨 Dark UI | Dark theme with scale-animation transitions |

## 🏗 Architecture

```
cv-run/
├── App.xaml(.cs)              # App entry, system tray, clipboard monitoring, global hotkey
├── MainWindow.xaml(.cs)       # Main panel window, list display, search, snapping logic
├── IndicatorWindow.xaml(.cs)  # Floating indicator, click/drag interaction
├── Models.cs                  # Data model (ClipboardItem)
├── Storage.cs                 # SQLite persistence layer
├── Logger.cs                  # Logging utility (daily logs + auto-cleanup)
├── NativeMethods.cs           # Windows API wrappers (hotkey/clipboard/window)
└── .gitignore
```

### Core Flow

```
[Clipboard Change] → WM_CLIPBOARDUPDATE → App.ReadClipboard()
    ├── Text → Save text content
    └── Image → PNG encode → Base64 storage

[User Trigger] → Ctrl+` → App.TogglePanel()
    ├── Floating mode → Show panel + scale-in animation
    └── Docked mode → Show panel → Hide indicator

[Panel Close] → Mouse leave / Drag away from edge
    ├── Docked → Shrink back to indicator (left/right/top)
    └── Floating → Panel stays visible
```

## 🛠 Tech Stack

| Technology | Purpose |
|------------|---------|
| **.NET 8.0** | Runtime framework |
| **WPF** | UI framework |
| **SQLite** | Local data storage |
| **Windows API** | Clipboard monitoring, global hotkeys, window management |

## 📦 Dependencies

- Microsoft.Data.Sqlite (10.0.8)

## 🚀 Quick Start

### Requirements

- Windows 10/11
- .NET 8.0 SDK or Runtime

### Build

```bash
cd ClipNestWpf
dotnet restore
dotnet build -c Release
```

### Run

```bash
dotnet run
```

Or publish and run directly:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
.\publish\ClipNestWpf.exe
```

## 📖 Usage

### Basic Operations

1. **Auto Capture** — Automatically monitors clipboard after startup, saves copied text or screenshots
2. **Toggle Panel** — Press `Ctrl+`` or click the edge indicator
3. **Paste Content** — Double-click an item to paste to current focus
4. **Search** — Type text in the search box to filter records in real-time
5. **Pin Records** — Right-click an item → Pin, immune to auto-cleanup
6. **Delete** — Right-click → Delete, or click the ✕ button on the right
7. **Clear All** — Click "Clear All" button to remove all unpinned records

### Docking Mode

- **Drag panel to screen edge** → Auto-snaps (left/right/top edges)
- **Drag the indicator** → Moves docking position along the edge
- **Drag indicator away from edge** → Switches to floating mode

## 📁 Data Directory

The `data/` folder in the application directory stores all data:

```
data/
├── clipnest.db                  # SQLite database
├── clipnest-2025-01-15.log      # Daily operation log
├── clipnest-error-2025-01-15.log  # Error log
```

> Log files are auto-cleaned after 7 days.

## 🔧 Development

### Window Behavior

| Property | Default | Description |
|----------|---------|-------------|
| Panel Width | 400px | Max 800px |
| Panel Height | 540px | Max 900px |
| Clipboard Cache Limit | 500 items | Oldest auto-deleted when exceeded |
| Snap Distance | 40px | Auto-snaps when window center is within this distance from edge |
| Indicator Size | 32×64px | Scales to 0.8x floating, 1.0x on hover |

### Hotkeys

| Hotkey | Function |
|--------|----------|
| `Ctrl+`` | Toggle panel visibility |

## 📄 License

MIT
