# Gear S 验收清单

> 2026-10-31。**S 不是评分字母，是一组可复现的证据 + 可执行的验收命令。**
> 本清单把"全量 S"拆成可勾选项；未勾选项必须写明阻塞原因，不用主观分数掩盖。

## 1. 验收矩阵

| 维度 | S 标准 | 当前 | 证据 / 命令 | 阻塞 |
|---|---|---|---|---|
| 架构 | 单一访问路径；无 >1000 行文件；Editor 代码全部 asmdef 化 | **达成（1 项设计豁免）** | Editor 已完成（33 asmdef）；`.Instance` **22 处**（仅注册点 + Modules 独立使用）；`LoopScrollRect` 1336 登记为设计豁免 | 仅 LoopScrollRect 不拆 |
| 核心可靠性 | RPC 协议可协商；下载有完整性/迁移策略；网络有 loopback 回归 | **达成（除压力/混沌）** | 错误帧开关 + 保留 ID 测试；`.done` + 迁移开关；TCP 回环 RPC 成功/错误/自动重连 3 条 PlayMode | 压力/混沌测试未做 |
| UI 行为 | 状态机/ESC/返回栈/Static 有 PlayMode 回归 | **达成** | 210 EditMode；PlayMode 9/9；Back 模态、Static 重开、Open 重复、ESC 文本聚焦均已覆盖 | — |
| 测试与门禁 | PR 上编译 + 类型检查 + 覆盖率阈值；守卫单一实现 | **达成（同仓 PR）/ 部分（fork）** | 210 EditMode + 9 PlayMode 全绿；6 条守卫；语法/文档链接门禁；同仓 PR 走自托管 `unity-verify-pr.yml` | fork PR 仍需 `UNITY_LICENSE` 才能跑 Unity |
| 文档 | 计数/链接自动校验；历史文档有快照标识；中英对齐 | **接近** | 本地链接已由 `Tools/DocsCheck` 自动检查（103 个链接 0 缺失）；计数仍人工维护 | 计数自动校验 / 英文对齐未做 |
| 发布 | 远端 `upm` 与源码一致；空工程安装烟测进 CI | **达成** | 远端 `upm` 已为 `com.jackeastray.gear@1.3.0`；`upm-smoke-test.ps1` 通过；自托管 main 已接入 | — |
| 仓库卫生 | `.git` 可接受；LFS 历史迁移；大文件门禁 | **部分** | Git objects 109 MB；LFS 642 MB / 23 文件；clone 总量仍约 751 MB | 需外置 `Images/` 或字体才能继续降 |
| 平台 | WebGL 浏览器 / Android / iOS / mac / Linux 真实验证 | **未达** | 仅构建配置 | 需设备与 macOS |

## 2. 已通过的验收命令

```powershell
# 1. 代码 + 守卫（210 条 EditMode）
powershell -ExecutionPolicy Bypass -File Tools/unity-verify.ps1

# 1b. PlayMode（9 条：模块组合 + Profiler + Back 模态 + Static 重开 + 重复打开 + ESC 文本聚焦 + TCP RPC 成功/错误/自动重连）
Unity -batchmode -nographics -projectPath Unity/Gear `
  -runTests -testPlatform PlayMode `
  -testResults Unity/Gear/Logs/verify/playmode-results.xml `
  -logFile Unity/Gear/Logs/verify/playmode.log

# 2. 不依赖 Unity 的语法门禁
dotnet run --project Tools/SyntaxCheck -- Unity/Gear/Assets/Gear

# 2b. 文档本地链接
dotnet run --project Tools/DocsCheck -- .

# 3. 发布前包校验
powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1 -CheckOnly

# 4. 空工程安装烟测（需要已激活的 Unity）
powershell -ExecutionPolicy Bypass -File Tools/upm-smoke-test.ps1 -UnityPath <Unity.exe>
```

## 3. 剩余 S 项（按阻塞类型）

### 3.1 代码内可继续做

| 项 | 现状 | 下一步 |
|---|---|---|
| `.Instance` 收敛 | 22 处（仅注册点 + Modules 程序集独立使用） | 已给 11 个 Runtime 模块加 `GetOrInstance()`；Modules 内的 Http/Network/Timer 因不能引用 Runtime 保留直取 |
| Editor asmdef | ✅ 已完成：`UI/` → `Gear.UI`，`Editor/` 非嵌套目录 → `Gear.Editor.Framework` | — |
| `LoopScrollRect` | 1336 行 | **登记为设计豁免**：高内聚的虚拟化列表组件，拆分布局/滚动逻辑会降低可读性；S 验收接受该例外 |
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
