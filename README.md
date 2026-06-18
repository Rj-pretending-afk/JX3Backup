# 剑网3备份器

剑网3备份器是一个 Windows 便携式桌面工具，用来备份、恢复和切换剑网3的键位、界面与角色配置。

## 下载即用

普通用户请到 GitHub Releases 下载：

```text
剑网3备份器-win-x64-self-contained.zip
```

解压后双击：

```text
剑3备份器.exe
```

自包含版本已经带 .NET 运行时，下载后通常可以直接使用。

## 主要功能

- 自动扫描所有盘符下的 `X:\JX3`，并自动定位 `userdata`。
- 支持 `userdata\账号\大区\服务器\角色` 结构。
- 每个用户 Profile 独立管理 99 个保存档。
- 恢复前自动创建快照。
- 支持通用档和角色专用档。
- 通用档默认不恢复宏、技能/动作按钮、完整 dump，尽量避免覆盖角色技能栏。
- 支持 OneDrive、Google Drive、坚果云等网盘的本地同步文件夹。
- 所有运行数据保存在 exe 同目录的 `data/`，便于整体搬家。

## 如何使用

1. 打开 `剑3备份器.exe`。
2. 软件会自动扫描类似 `G:\JX3` 的游戏目录。
3. 如果没有找到，手动选择剑网3根目录，例如 `G:\JX3`。
4. 点击“扫描”，确认角色列表中出现角色。
5. 选择用户 Profile、保存档 Slot、来源角色和模块。
6. 点击“创建备份”。
7. 恢复时选择备份版本、目标角色和模块，再点击“恢复到角色”。

恢复前软件会自动给目标角色创建快照。

## 网盘备份

本软件不直接登录网盘账号，而是使用网盘客户端的本地同步目录。

### OneDrive

1. 安装并登录 OneDrive。
2. 建一个同步目录，例如 `C:\Users\你的用户名\OneDrive\JX3Backups`。
3. 在软件里把“同步文件夹”设置为该目录。
4. 选择备份版本，点击“同步所选备份”。

### Google Drive

1. 安装 Google Drive for desktop。
2. 选择或新建同步目录，例如 `G:\My Drive\JX3Backups`。
3. 在软件里设置为同步文件夹。
4. 点击“同步所选备份”。

### 换电脑

1. 新电脑安装同一个网盘客户端并完成同步。
2. 下载剑网3备份器。
3. 设置同一个同步文件夹。
4. 选择备份恢复到目标角色。

## 安全说明

通用档默认只勾选：

- 界面布局
- 快捷键/键位
- 显示/聊天/插件

通用档默认不勾选：

- 宏
- 技能/动作按钮
- 完整 `userpreferences/*.dump`

如果你手动勾选高风险模块，恢复时可能覆盖技能栏摆放、动作按钮或宏。

## 开发

需要 Windows 和 .NET 8 SDK。

```powershell
dotnet restore
dotnet build .\JX3ConfigSwitcher.sln
dotnet test .\JX3ConfigSwitcher.sln --no-build
dotnet run --project .\JX3ConfigSwitcher\JX3ConfigSwitcher.csproj
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
