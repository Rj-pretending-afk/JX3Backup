# 剑网3备份器

剑网3备份器是一个 Windows 便携式桌面工具，用来备份、恢复和切换剑网3的键位、界面与角色配置。程序数据都放在 exe 同目录的 `data/` 里，复制整个文件夹就能带走。

## 下载即用

普通用户请到 GitHub Releases 下载：

```text
JX3BackupSwitcher-win-x64-self-contained.zip
```

解压后双击：

```text
剑3备份器.exe
```

自包含版本已经带 .NET 运行时，下载后通常可以直接使用。

## 主要功能

- 自动扫描所有盘符下的 `X:\JX3`，并自动定位 `userdata`。
- 支持 `userdata\账号\大区\服务器\角色` 这类真实剑网3目录结构。
- 每个用户 Profile 独立管理 99 个保存档 Slot。
- 支持通用档、角色专用档和自动快照。
- 恢复、覆盖、删除等危险操作前自动给目标角色创建快照。
- 通用档默认不包含宏、技能/动作按钮、完整 dump，尽量避免覆盖角色技能栏。
- 技能/动作按钮模块会解析 `userpreferences.jx3dat` 里的 `ActionBar...` 条目，保存为独立快照；恢复时只合并技能摆放，不整文件覆盖。
- 支持 OneDrive、Google Drive、坚果云等网盘的本地同步文件夹。

## 如何使用

1. 打开 `剑3备份器.exe`。
2. 软件会自动扫描类似 `G:\JX3` 的游戏目录。
3. 如果没有找到，手动选择剑网3根目录，例如 `G:\JX3`。
4. 点击“扫描”，确认角色列表中出现角色。
5. 选择用户 Profile、保存档 Slot、来源角色和模块。
6. 点击“创建备份”。
7. 恢复时选择备份版本、目标角色和模块，再点击“恢复到角色”。

恢复前软件会自动给目标角色创建快照。游戏运行时默认禁止恢复，只允许备份，避免游戏退出时把恢复结果写回覆盖。

## 技能摆放备份

实测剑网3的技能/动作按钮摆放位于角色目录的 `userpreferences.jx3dat` 中，文件格式是带 `CNDK` 头和 CRC32 校验的 Lua 表。动作栏位置主要表现为：

```lua
["ActionBar1_Page1/1"]={5,9005}
["ActionBar2_Page1/2"]={}
```

本工具不会为了技能摆放整份覆盖 `userpreferences.jx3dat`。选择“技能/动作按钮”模块时，程序会：

1. 从来源角色抽取 `ActionBar...` 条目。
2. 写入备份包的 `special/skill-placement.json`。
3. 恢复时读取目标角色现有 `userpreferences.jx3dat`。
4. 只替换/补充对应 `ActionBar...` 条目。
5. 重新写入 CNDK 头、长度和 CRC32。

建议只在同职业、同心法或高度相似的技能栏布局之间使用这个模块。目标角色需要至少登录过一次并生成 `userpreferences.jx3dat`。

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
2. 下载或复制剑网3备份器文件夹。
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
- 完整 dump / 完整 `userpreferences.jx3dat`

如果手动勾选高风险模块，恢复前请确认目标角色、职业和心法。完整 dump 和完整 `userpreferences.jx3dat` 仍然可能覆盖宏、动作栏和角色专用设置。

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
