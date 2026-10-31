# 仓库体积与 LFS

> 现状快照：2026-10-31。结论先说：**当前 877 MB 的 `.git` 不是代码造成的，是历史里的大二进制造成的；
> 不重写历史就无法真正瘦身**。本文给出非破坏性止损方案与需要协调的历史重写步骤。
> （其中约 34 MB 来自本地重建的 `upm` 发布分支；推远端后远端也会增加这部分。）

## 1. 体积构成（实测）

| 位置 | 体积 | 说明 |
|---|---|---|
| `.git/objects` | 737 MB | 历史提交里的 GIF / 字体 / 旧 `.unitypackage` 等 |
| `.git/lfs` | 140 MB | 已进入 LFS 的对象 |
| `Images/` | 445 MB | 文档用示例 GIF/PNG |
| `Unity/Gear/Assets/Gear/Fonts/` | 130 MB | 思源 / HarmonyOS Sans / 第三方中文字体 |
| 工作区 `Assets/` | 198 MB | 含上述字体 |

`.gitattributes` 已为 `*.gif` / `*.otf` / `*.ttf` / `*.psd` / `*.fbx` / `*.mp4` / `*.unitypackage`
配置 LFS，但 `git lfs ls-files` 只有 1 个文件——说明**大部分大文件是在配置 LFS 之前提交的**，
它们仍在普通 object 里（配置只影响之后新提交的文件）。

## 2. 非破坏性止损（已默认采用）

这些操作不改历史、不需要协作者重新 clone：

1. **不再新增裸大文件**：新增 GIF/字体/视频先确认 `.gitattributes` 的 LFS 规则生效
   （`git check-attr filter -- Images/x.gif` 应输出 `filter: lfs`）。
2. **运行时包默认排除字体与示例**：UPM 的 Runtime profile 已排除 `Fonts/`、`Scenes/`、
   `3rd/Joystick Pack` 等，接入方不需要拉 130 MB 字体。
3. **文档图外置（可选）**：把 `Images/` 迁到 GitHub Release 附件/图床，README 用外链；
   工作区会立刻少 445 MB，但历史仍不变。
4. **定期检查**：`git lfs migrate info --everything` 只读，能列出各扩展名占用，不改仓库。

## 3. 历史重写（需要全员协调，默认不执行）

目标：把历史里的大文件迁进 LFS，或从历史中剔除。两种方案：

### 方案 A：迁进 LFS（保留文件，改指针）

```bash
# 先备份/打 tag，通知所有协作者
git lfs migrate info --everything
git lfs migrate import --include="*.gif,*.png,*.psd,*.otf,*.ttf,*.fbx,*.mp4,*.unitypackage" --everything
git push --force-with-lease --all
git push --force-with-lease --tags
```

代价：**所有提交哈希改变**，协作者必须重新 clone（不能简单 pull）。
GitHub 上需要临时允许 force push；LFS 配额会随历史增长。

### 方案 B：从历史剔除（不保留旧版本）

```bash
# 示例：剔除 Images/ 与 Release/（会改写历史）
git filter-repo --path Images --path Release --invert-paths
git push --force --all
```

代价同上，且旧提交里这些文件**永久消失**。只有在确认文档图可以外置后才做。

### 推荐顺序

1. 先做第 2 节的止损，冻结增长；
2. 把文档图迁到外置存储，确认 README 不再依赖 `Images/`；
3. 再执行方案 B（比 A 更能缩小仓库）；
4. 若必须保留历史图，选方案 A，并在发布说明里写清"需重新 clone"。

## 4. 验收

```bash
git count-objects -vH          # size-pack 应显著下降
git lfs ls-files | wc -l       # 迁移后应接近大文件数量
du -sh .git Images Unity/Gear/Assets/Gear/Fonts
```

> 注意：`git gc` 只能压缩，不会删除仍被历史引用的对象；不重写历史时 `.git` 不会明显变小。
