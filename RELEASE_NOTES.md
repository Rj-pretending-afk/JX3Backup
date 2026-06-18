# 剑网3备份器 v0.1.2

## 更新

- 新增独立“技能/动作按钮”备份：解析 `userpreferences.jx3dat` 中的 `ActionBar...` 条目，保存到 `special/skill-placement.json`。
- 恢复技能摆放时只合并 `ActionBar...` 条目，不整份覆盖目标角色的 `userpreferences.jx3dat`。
- `userpreferences.jx3dat` 和 `userpreferencesasync.jx3dat` 现在归类为完整高风险配置，通用档默认不会包含。
- 新增 CNDK 头、长度和 CRC32 写回校验，恢复后会重新生成合法配置文件头。
- 移除本地无关的 `挂机` 文件夹，并修正 `.gitignore`。
- README 增加技能摆放备份、网盘同步和安全说明。

## 下载

推荐下载：

```text
JX3BackupSwitcher-win-x64-self-contained.zip
```

解压后双击 `剑3备份器.exe` 即可使用。

## 注意

技能摆放模块建议只在同职业、同心法或技能栏结构相近的角色之间使用。目标角色需要至少登录过一次，让游戏生成 `userpreferences.jx3dat`。

---

# 剑网3备份器 v0.1.1

## 更新

- 重做主界面为更简洁的 dark mode 工作台布局。
- 右侧备份/恢复区域改为滚动栏，避免模块选择和底部提示重叠。
- 角色列表和保存档区域重新分配高度，避免内容压在一起。
- 修复深色模式中 DataGrid 选中行变白的问题。
- 保留角色扫描修复：支持 `userdata\账号\大区\服务器\角色`。

## 下载

推荐下载：

```text
JX3BackupSwitcher-win-x64-self-contained.zip
```

解压后双击 `剑3备份器.exe` 即可使用。

## 注意

通用档默认不会覆盖技能栏、动作按钮和宏。只有手动勾选高风险模块时，才可能覆盖这些内容。

---

# 剑网3备份器 v0.1.0

首个公开版本。

## 下载

推荐下载：

```text
JX3BackupSwitcher-win-x64-self-contained.zip
```

解压后双击 `剑3备份器.exe` 即可使用。

## 功能

- 自动扫描 `X:\JX3` 并定位 `userdata`。
- 识别 `账号\大区\服务器\角色` 角色目录。
- 用户 Profile 中 99 个保存档 Slot。
- 通用档和角色专用档。
- 恢复前自动快照。
- 模块化备份/恢复，默认避开宏、动作按钮和完整 dump。
- 支持 OneDrive/Google Drive 等网盘本地同步目录。
- 便携式 `data/` 运行数据目录。

## 注意

通用档默认不会覆盖技能栏、动作按钮和宏。只有手动勾选高风险模块时，才可能覆盖这些内容。
