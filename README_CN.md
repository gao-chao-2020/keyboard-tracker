# 键盘鼠标追踪器

Windows 键盘鼠标活动记录统计软件，实时热力图可视化。

追踪你的键盘鼠标使用习惯——哪些键按得最多、鼠标移动了多远、什么时段最活跃。

## 功能

- **全局输入捕获** — 通过底层 Windows 钩子捕获系统级键盘鼠标操作
- **键盘热力图** — 可视化键盘布局，按频次着色（浅蓝 → 深蓝）
- **鼠标统计** — 左右键点击次数、移动距离（米）
- **活动时间线** — 折线图展示按键+鼠标活动，可在小时/天/月之间切换
- **系统托盘** — 后台静默运行，通过托盘图标唤出窗口
- **持久化存储** — SQLite 存储，重启后数据保留

## 环境要求

- Windows 10 或更高版本
- .NET 9 SDK（用于源码编译）

## 快速开始

```bash
# 克隆
git clone https://github.com/YOUR_USER/keyboard-tracker.git
cd keyboard-tracker

# 运行
dotnet run

# 发布单文件 exe
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## 技术栈

| 技术 | 用途 |
|---|---|
| C# .NET 9 + WPF | GUI 框架 |
| SQLite (Microsoft.Data.Sqlite) | 持久化存储 |
| LiveCharts2 | 活动图表 |
| Win32 WH_KEYBOARD_LL / WH_MOUSE_LL | 全局输入钩子 |
| Windows Forms NotifyIcon | 系统托盘 |

## 工作原理

1. **KeyboardHookService** / **MouseHookService** 启动独立线程，通过 `SetWindowsHookEx` 安装全局钩子
2. 事件通过 `Channel<T>` 流入 **StatsProcessor**
3. StatsProcessor 在内存中按小时/分钟聚合，每 5 秒刷入 SQLite
4. WPF 仪表盘查询 SQLite，通过数据绑定渲染
5. **LiveCharts2** 渲染活动时间线
6. **NotifyIcon** 提供系统托盘和右键菜单

## 许可证

MIT
