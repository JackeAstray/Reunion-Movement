# Gear S 验收清单

> 2026-10-31。**S 不是评分字母，是一组可复现的证据 + 可执行的验收命令。**
> 本清单把"全量 S"拆成可勾选项；未勾选项必须写明阻塞原因，不用主观分数掩盖。

## 1. 验收矩阵

| 维度 | S 标准 | 当前 | 证据 / 命令 | 阻塞 |
|---|---|---|---|---|
| 架构 | 单一访问路径；无 >1000 行文件；Editor 代码全部 asmdef 化 | **部分** | Editor 已完成（`Gear.Editor.Framework` + `Gear.UI`，33 asmdef）；`.Instance` 仍 117；`LoopScrollRect` 1336 | `.Instance` 收敛 + LoopScrollRect 设计豁免 |
| 核心可靠性 | RPC 协议可协商；下载有完整性/迁移策略；网络有 loopback 混沌测试 | **部分** | 错误帧开关 + 保留 ID 测试；`.done` + 迁移开关；缺真实回环 | 需集成测试环境 |
| UI 行为 | 状态机/ESC/返回栈/Static 有 PlayMode 回归 | **部分** | 210 条 EditMode；PlayMode 2/2；`UIModalStack` 纯逻辑已测；UI 三条集成仍缺 | 需要真实 Canvas/预制体 |
| 测试与门禁 | PR 上编译 + 类型检查 + 覆盖率阈值；守卫单一实现 | **部分** | 210 EditMode 全绿；6 条守卫；Roslyn 语法门禁；类型检查仍靠 Unity | 需 Unity 授权或自托管 runner |
| 文档 | 计数/链接自动校验；历史文档有快照标识；中英对齐 | **接近** | 主要计数已校正；新增 LFS/烟测文档；`Docs/archive` 仍按历史保留 | 链接/计数检查未自动化 |
| 发布 | 远端 `upm` 与源码一致；空工程安装烟测进 CI | **达成** | 远端 `upm` 已为 `com.jackeastray.gear@1.3.0`；`upm-smoke-test.ps1` 通过；自托管 main 已接入 | — |
| 仓库卫生 | `.git` 可接受；LFS 历史迁移；大文件门禁 | **部分** | Git objects 109 MB；LFS 642 MB / 23 文件；clone 总量仍约 751 MB | 需外置 `Images/` 或字体才能继续降 |
| 平台 | WebGL 浏览器 / Android / iOS / mac / Linux 真实验证 | **未达** | 仅构建配置 | 需设备与 macOS |

## 2. 已通过的验收命令

```powershell
# 1. 代码 + 守卫（210 条 EditMode）
powershell -ExecutionPolicy Bypass -File Tools/unity-verify.ps1

# 1b. PlayMode（现有 2 条：模块组合 + Profiler 采集）
Unity -batchmode -nographics -projectPath Unity/Gear `
  -runTests -testPlatform PlayMode `
  -testResults Unity/Gear/Logs/verify/playmode-results.xml `
  -logFile Unity/Gear/Logs/verify/playmode.log

# 2. 不依赖 Unity 的语法门禁
dotnet run --project Tools/SyntaxCheck -- Unity/Gear/Assets/Gear

# 3. 发布前包校验
powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1 -CheckOnly

# 4. 空工程安装烟测（需要已激活的 Unity）
powershell -ExecutionPolicy Bypass -File Tools/upm-smoke-test.ps1 -UnityPath <Unity.exe>
```

## 3. 剩余 S 项（按阻塞类型）

### 3.1 代码内可继续做

| 项 | 现状 | 下一步 |
|---|---|---|
| `.Instance` 收敛 | 117 处（14 个类型） | 先迁移"引擎已启动后"的提供者访问；保留预初始化/编辑器/测试的显式豁免，并把基线继续下调 |
| Editor asmdef | ✅ 已完成：`UI/` → `Gear.UI`，`Editor/` 非嵌套目录 → `Gear.Editor.Framework` | — |
| `LoopScrollRect` | 1336 行，约定不拆 | 如必须满足"无 >1000"，按 partial 拆布局/滚动逻辑；否则在 S 清单里登记为"设计豁免" |
| PlayMode 回归 | 缺 UI 状态机/ESC/Back/Static | 用 `Tests/PlayMode` 加 3 条；需要 UI 预制体或运行时构造 Canvas |

### 3.2 需要授权 / 不可逆

| 项 | 需要的动作 | 影响 |
|---|---|---|
| 推送 `upm` | ✅ 已执行（2026-10-31）；旧包备份标签 `upm-backup-20261031` | 旧安装方从 `com.reunion.movement` 迁到 `com.jackeastray.gear` |
| 历史重写 | ✅ 已执行 `git lfs migrate import --include="*.gif,*.unitypackage" --everything` 并强推所有分支 | 所有提交哈希改变，协作者必须重新 clone；LFS 占用约 642 MB |
| PR 类型门禁 | 配 `UNITY_LICENSE` 或把自托管 runner 接到 PR | 公共仓库自托管 runner 有安全风险，需先加固 |
| 真机验证 | WebGL 浏览器、Android/iOS 设备、macOS | 代码仓库内无法替代 |

## 4. 结论

当前状态更准确的描述是 **"A- 的工程 + S- 的可复现验证 + 未完成的发布/仓库/平台"**。
代码内还能继续推进（`.Instance`、Editor asmdef、PlayMode），但**发布与仓库两项必须由维护者做不可逆操作**，
平台验证需要真实设备。把这三类混在一起说"全 S"会掩盖风险；本清单就是分解后的验收边界。
