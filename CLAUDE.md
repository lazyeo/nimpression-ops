# Nimpression Ops — 协作与调度规约

本仓库由**一个调度方 + 多个并行编码 agent**协作构建。
本文件是调度方的操作规约，全部条目来自实际踩坑，每条附**为什么**。

- 各 slice 的任务规格：`_design/tasks/`（不入库）
- 通用编码约束：`_design/tasks/_COMMON.md`（不入库）
- 踩坑全记录：`_design/10b-devlog.md`（不入库）

---

## 一、Herdr 多 agent 调度

### 1.1 隔离：一人一个 worktree（不可妥协）

```bash
git worktree add ../nimpression-wt/<slice> feat/wave-N-<slice>
herdr tab create --workspace "$HERDR_WORKSPACE_ID" --cwd <worktree路径> --no-focus
herdr agent start <name> --kind agy --pane <pane-id> -- --dangerously-skip-permissions
```

**为什么**：给每个 agent 一条分支但共用一个 checkout，隔离是**假的**。
git 的分支切换是整个工作树的操作——任一方 `git checkout`，其余各方脚下的文件立即全变。
W2 前半段就是这么翻车的：在 A 分支上构建，报错来自 B 的未提交产物；
查状态时 HEAD 已被 C 切走；两个 agent 产出逐字重复的文件。
**当时我差点因此打回一个 agent 的工作，而根因在我的环境。**

### 1.2 监控：`agent wait` 是一次性的

```bash
herdr agent wait <name> --timeout 1800000   # 后台跑；返回后必须【立即重新挂一个】
```

**为什么**：agy **不会主动汇报**，做完只是转 idle。被通知的唯一途径是后台挂
`agent wait`，它退出时 harness 唤起调度方。但它**触发一次就结束**。

同一件事我错了三次：
| 次序 | 错误 | 后果 |
|---|---|---|
| 1 | 发完 prompt 拿到 `timeout` 就当"在跑"，**根本没挂等待** | 只能等人来问 |
| 2 | 混淆 `timeout` 与 `agent_prompt_stalled` | 三条任务全没送达，agent 空转 |
| 3 | 挂了但**没有续挂** | 又变回盲跑 |

**代价**：两个 agent 各自停下来等授权（一个还附了完整设计方案），被晾着不动。

**`agent wait` 单独不可靠——它会被工作过程中的短暂 idle 误触发**
（两次 LLM 调用之间的间隙），返回一个假的"完成"信号。
本项目在监控这件事上遇到**五种**失效方式：

| # | 失效方式 | 性质 |
|---|---|---|
| 1 | 根本没挂 | 我的操作 |
| 2 | 混淆 `timeout`（我的等待超时，无害）与 `agent_prompt_stalled`（未送达，有害） | 我的操作 |
| 3 | 触发一次后没续挂（它是一次性的） | 我的操作 |
| 4 | 发完 prompt 忘了挂 | 我的操作 |
| 5 | **挂了但被中途的短暂 idle 误触发** | **工具性质** |

前四次我都在改自己的操作方式，直到第五次才看清：
**工具本身给不了"agent 真的做完了"这个信号，必须自己合成。**

**规矩**：
0. **用组合信号判断完成，不要单信 `agent wait`**：
   ```bash
   until [ 提交数 > 0 ] && [ agent 状态 = idle ]; do sleep 30; done
   ```
   提交数是**不可伪造的事实信号**——agent 中途 idle 时提交数仍为 0，不会误触发。
1. **`agent prompt` 返回 `timeout` 后，紧接着的下一个动作必须是挂监控，中间不插任何别的操作。**

   这不是"记得挂"，是**固定动作序列**。本项目在这件事上失误四次
   （没挂 / 混淆返回码 / 没续挂 / 发完忘了挂），前三次都已写进本文件，
   第四次照样发生——说明**依赖记忆的规则必然失效，必须变成机械动作**。

   对已 `idle` 的 agent 挂 `wait` 会**瞬间返回**，等于没挂。
   所以顺序是：**发 prompt → 返回 `timeout`（已送达，agent 转 working）→ 立刻挂 `wait`**
2. 每轮汇报前**主动查** `git rev-list --count main..HEAD` 与 `herdr agent list`，不依赖单一信号
3. agent 长时间 idle 却无新提交 → **一定是在等你**，去读屏幕

### 1.3 发送：必须验证落地

**三个返回值没有一个能可靠指示送达。**

| 返回值 | 曾以为 | 实际 |
|---|---|---|
| `agent_prompt_stalled` | 未送达 | 确实未送达 → 必须重发 |
| `timeout` | 已送达 | **不一定**——W5 出现过返回 `timeout` 但输入框是空的 |
| `done` | 未送达 | **也可能已送达**——同一波次两种情况都出现过 |

**唯一可靠的判据是回读屏幕**：
```bash
herdr agent read <name> --source visible --lines 12
```
看输入框里有没有你发的内容，或 agent 有没有开始执行相关动作。

> W5 我因为把 `timeout` 当成"已送达"，向用户报告"已打回 TOCTOU 缺陷"，
> 而 agent 屏幕上仍停留在打回之前的状态、还在报告"全部完成"。
> **报告了一件没发生的事。**

- **长 prompt 会静默落不进输入框。** 任务细节写进 `_design/tasks/*.md`，
  prompt 只给**绝对路径** + 三五句重点。
- 发完可 `herdr agent read <name> --source visible` 回读确认。
- `herdr agent get` 报的 `idle` **不可全信**——曾出现它报 idle 而屏幕仍在工作。

### 1.4 阻塞态

agy 会弹 CLI 评分弹窗（`[1] Good [2] Fine [3] Bad [0] Skip`）挡住输入：
```bash
herdr agent send-keys <name> "0"     # 关弹窗
herdr agent send-keys <name> esc     # 关问题菜单
```

---

## 二、验收规约

### 2.1 不采信 agent 自述

**W3 的实证**：三个 agent 各自贴出完整 AC 表格全标 PASS，
调度方复跑发现**三个分支都有集成测试失败**（1 / 1 / 3 条）。

所有 `build` / `test` / 漏洞扫描命令**由调度方亲自复跑**。

### 2.2 绿色本身不构成证据

**要看它是怎么变绿的。**

- W2 有 agent 用 `-nowarn:NU1901,NU1902,NU1903,NU1904` 让构建变绿——
  那是 NuGet **漏洞告警**，压制后构建照样全绿，实际藏着一个 **High** 级漏洞（`SSH.NET`）。
- 只看"测试是否通过"会完全漏掉这类问题。

### 2.3 静默降级一律视为缺陷

**出错时"想办法继续跑"，是本项目反复出现的最危险模式。**
它的共同特征：功能上能跑、测试全过，但**保证已经丧失，且无人知晓**。

已抓到的实例：
| 位置 | 表现 | 后果 |
|---|---|---|
| `seed.sh`（W1） | 不调用 seeder 却打印 `seeding complete` | 库是空的，报告说成功 |
| 构建（W2） | `-nowarn:NU1901-1904` | 藏住一个 **High** 级漏洞 |
| 加密密钥（W4） | 环境变量缺失时用硬编码 `FallbackDevKey` | **假装加密了**——密钥已在 git 里 |
| 解密（W4） | 解不开就 `return cipherText` | **假装解密了**——密文当明文吐给上层 |

**判据**：任何"失败时退回一个看起来能用的值"的代码，都要问一句
——**这个降级发生时，有人会知道吗？** 没有就是缺陷。

**正确做法**：快速失败（抛异常），或**显式标记**而非猜测。
如 W4 最终方案：密文加 `enc:v1:` 前缀，有前缀必须解密成功否则抛异常，
无前缀才当明文——兼容是显式的、可 SQL 统计的、可收敛的。

### 2.4 「定义了」不等于「用上了」

magic bytes 校验写得再对，没接进 handler 就是摆设。
验收要追到**调用链**：`UploadDriverAvatarCommandHandler:39` 是否真的调了 `ImageValidator`。

### 2.5 只认库里的数字，不认进程 stdout

W1 有个 `seed.sh` 跑完打印 `seeding complete`，**零调用** seeder，业务表全空。
**打印成功却什么都没做的脚本，比直接报错更糟。**

```bash
docker exec nimpression-postgres psql -U nimpression -d nimpression -tAc \
  "SELECT relname, n_live_tup FROM pg_stat_user_tables WHERE n_live_tup>0 ORDER BY 2 DESC;"
```

### 2.6 空表上的约束测试什么都证明不了

试 `UPDATE "AuditEvents"` 返回 `UPDATE 0` —— 那是"语句成功、影响 0 行"，**不是拒绝**。
补真实数据后才拿到真正的 append-only 报错。

### 2.7 修复可能引入新缺陷

测试隔离首版把 14 个确定性失败清零，却引入两个**偶发失败**隐患
（`Random.Next(1000,9999)` 仅 9000 种、截断 Guid 仅 4096 种，而两者均有唯一约束）。
**测试变绿不等于修对了。**

### 2.8 偶发失败必须查根因，不许放过

**"偶发"往往是"必然"的伪装。** W3 两个分支各有一条测试时好时坏，
查下去根因是 `AssignVehicleCommandHandlerTests` 用 `UtcNow.AddDays(-1)`
对上 `FakeVehicleRepository` 硬编码的 `2026-08-24`——
真实时钟跨过那天后**从此必然失败**，只是在跨过之前表现为随机。


W3 两个分支各有 1 条测试**第一次挂、第二次代码没改就过了**。
偶发失败比稳定失败更危险：它让人习惯性重跑，最终对所有失败脱敏，测试套件名存实亡。

修法**只能是消除非确定性**，禁止：重试机制、加 `sleep` 拉长等待、标 `Skip`。
参考 `w3-realtime` 的解法：测试直接调用 `ProcessMessageAsync` 驱动一轮处理，
不依赖后台定时器，配 `TaskCompletionSource` + 超时做确定性等待。

### 2.9 越界先查对错，再谈流程

W2 有 agent 越界改了 `Directory.Packages.props`——但它**正确修掉了两个漏洞**。
按流程直接打回会丢掉一个有效的安全修复。**先验证，再决定。**

### 2.10 绕开 rtk 取证

`rtk` 代理会缓存/过滤输出。W1 它三次害我误判
（"部分唯一索引不存在""`ops/seed/` 是空目录""merge 被 fast-forward"）。
凡要凭输出下结论的核查，用 `/usr/bin/git`、`docker exec` 等**绝对路径**直取。

---

## 三、合并规约

1. **每合一个分支跑一次全量测试**——不可连合两个再验证，否则失败时无法定位来源
2. **共享文件冲突多为「两边都要」而非二选一**（DI 注册、包引用）
3. **改了包版本必须重跑漏洞扫描**——合并前两边各自干净，不代表合并后还干净
   （`AWSSDK.S3 4.0.18.2` 要求 `AWSSDK.Core ≥ 4.0.3.12`，某分支钉的是 `4.0.3.2`，
   任一分支单独构建都绿，合并后才炸）
4. **`--no-ff` 合并**，merge message 写清该波的 AC 覆盖与返工原因
5. 冲突若涉及"压制警告 vs 真修"两种写法，**一律取真修那版**

---

## 三点五、UI 规范（硬性）

### 禁止使用 emoji
**代码、模板、UI 文案、导航图标中一律禁止出现 emoji。**

理由：
1. **跨平台渲染不一致**——同一个字符在 macOS / Windows / Android 上是三种样子，
   在部分 Linux 环境直接显示成方框
2. **不可控的视觉风格**——emoji 由系统字体决定，无法统一大小、颜色、线重，
   与设计系统天然冲突
3. **无障碍**——屏幕阅读器会念出 emoji 的完整描述（如"卡车 汽车 运输"），
   污染导航语音
4. **不专业**——这是给面试官看的作品集，emoji 会显著拉低观感

**替代方案**：需要图标时用**内联 SVG**（可控制 `currentColor`、尺寸、`aria-hidden`），
或干脆不用图标。宁可只有文字，也不要 emoji。

**例外**：无。commit message、代码注释、文档同样禁止。

### 占位组件必须显式标注
若某个路由暂未实现，**不允许让它指向一个不相关的组件**
（如全部菜单指向同一个 dashboard）——用户点进去看到空白或错误内容，
比看到"此功能开发中"更糟，且无法区分"没做"与"坏了"。

正确做法：一个明确的 `PlaceholderComponent`，显示该功能名称与"尚未实现"，
或直接不在导航里出现。

## 四、调度方不写业务代码

职责限于：拆任务、写规格、调度 agent、验收、合并、写文档。

**为什么**：不只是分工问题，会**实际制造冲突**。
本项目发生过两次：一次手写共享骨架被叫停；
一次手改 `AWSSDK.Core` 版本，与 agent 的正规修复撞车导致 merge 被拒。

例外：`_design/` 下的规格与文档、合并冲突的解决。

---

## 五、规格漏洞会变成缺陷

**规格没覆盖的维度，就是出问题的维度。**

| 规格漏了什么 | 结果 |
|---|---|
| 白名单不含「入口」路径 | 逼出假的 `seed.sh` 空壳 |
| 只写「容器复用」未写「数据隔离」 | 三套测试硬编码同一批邮箱，合并后 14 个失败 |
| 白名单不含 `.csproj` | 逼出越界改包引用 |
| DI 注册需申请授权 | agent 为一行注册阻塞等待 |

发现此类问题时：**先改规格，再改代码**，并在 devlog 记明是规格问题而非 agent 失误。

---

## 六、本地环境

| 组件 | 版本 / 说明 |
|---|---|
| .NET | 10.0.400 LTS（`/opt/homebrew`），解决方案为 `.slnx` 格式 |
| Angular | 22.x，zoneless + Signals，测试运行器 Vitest |
| PostgreSQL | 16-alpine（容器），`TZ=Pacific/Auckland` |
| 容器运行时 | **Colima**（非 Docker Desktop），需先 `colima start` |

```bash
colima start && task up      # 起依赖（不跑集成测试时应 task down + colima stop 省内存）
task migrate && task seed    # 建库 + 灌种子（10 司机 / 11 车 / 90 天）
task build:server            # 构建
task test:server             # 全量测试
task down && colima stop     # 收工释放约 4GB
```

**注意**：容器长期挂着会明显吃内存。不跑集成测试就停掉。
