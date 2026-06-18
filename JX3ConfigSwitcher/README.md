# 剑网3备份器

剑网3备份器是一个 Windows 便携式桌面工具，用来备份、恢复和切换剑网3的键位、界面与角色配置。

它的目标是解决两个常见问题：

- 换电脑、换账号、换角色时，不想手动重新调界面和键位。
- 不想整包覆盖 `userdata`，因为那样可能把不同角色的技能栏、动作按钮和宏一起覆盖掉。

## 功能

- 自动扫描所有盘符下的 `X:\JX3`，并自动定位真实的 `userdata`。
- 识别常见剑网3结构：`userdata\账号\大区\服务器\角色`。
- 每个用户 Profile 独立管理 99 个保存档。
- 支持通用档和角色专用档。
- 恢复前自动创建快照，方便回滚。
- 模块化备份/恢复，通用档默认不勾选高风险模块。
- 支持 OneDrive、Google Drive、坚果云等网盘的本地同步文件夹。
- 所有程序数据保存在 exe 同目录下的 `data/`，便于整体搬走。

## 下载即用

推荐普通用户下载 GitHub Release 里的自包含压缩包：

```text
剑网3备份器-win-x64-self-contained.zip
```

解压后双击：

```text
剑3备份器.exe
```

这个包自带 .NET 运行时，通常不需要额外安装环境。

如果你已经安装了 .NET 8 Desktop Runtime，也可以使用较小的 framework-dependent 版本。

## 使用方法

1. 打开 `剑3备份器.exe`。
2. 软件会自动扫描类似 `G:\JX3` 的游戏目录。
3. 如果没有自动找到，可以手动选择剑网3根目录，例如：

```text
G:\JX3
```

4. 点击“扫描”，确认角色列表中出现角色。
5. 选择用户 Profile、保存档 Slot、来源角色和模块。
6. 点击“创建备份”。
7. 恢复时选择备份版本、目标角色和模块，然后点击“恢复到角色”。

恢复危险操作前，软件会自动创建当前目标角色的快照。

## 如何避免覆盖技能栏和动作按钮

通用档默认只选择：

- 界面布局
- 快捷键/键位
- 显示/聊天/插件

通用档默认不会选择：

- 宏
- 技能/动作按钮
- 完整 `userpreferences/*.dump`

如果你手动勾选这些高风险模块，恢复时可能覆盖技能栏摆放、动作按钮或宏。

目前软件不会尝试修改未知 `.dump` 文件内部结构；这是为了避免错误解析导致配置损坏。

## 用网盘备份

这个软件不直接登录你的网盘账号。推荐方式是使用网盘客户端的本地同步目录。

### OneDrive

1. 安装并登录 OneDrive。
2. 新建一个同步文件夹，例如：

```text
C:\Users\你的用户名\OneDrive\JX3Backups
```

3. 在剑网3备份器里把“同步文件夹”设置为这个目录。
4. 选择一个备份版本，点击“同步所选备份”。

### Google Drive

1. 安装 Google Drive for desktop。
2. 新建或选择一个同步目录，例如：

```text
G:\My Drive\JX3Backups
```

3. 在软件里设置为同步文件夹。
4. 同步备份包即可。

### 换电脑使用

1. 在新电脑安装同一个网盘客户端并完成同步。
2. 下载或复制剑网3备份器文件夹。
3. 打开软件，把同步文件夹指向同一个网盘目录。
4. 选择备份包恢复到目标角色。

## 便携数据目录

程序运行后会在 exe 同目录创建：

```text
data/
  appsettings.json
  app.db
  backups/
  snapshots/
  logs/
```

这些文件可能包含你的本机路径、角色名、备份记录和配置包，不应该提交到 Git。

## 从源码运行

需要：

- Windows
- .NET 8 SDK

```powershell
dotnet restore
dotnet run --project .\JX3ConfigSwitcher\JX3ConfigSwitcher.csproj
```

## 测试

```powershell
dotnet build .\JX3ConfigSwitcher.sln
dotnet test .\JX3ConfigSwitcher.sln --no-build
```

## 发布

普通依赖运行时版本：

```powershell
dotnet publish .\JX3ConfigSwitcher\JX3ConfigSwitcher.csproj -c Release -r win-x64 --self-contained false -o .\publish-framework-dependent
```

自包含版本：

```powershell
dotnet publish .\JX3ConfigSwitcher\JX3ConfigSwitcher.csproj -c Release -r win-x64 --self-contained true -o .\publish-self-contained
```

## 开源协议

MIT License
