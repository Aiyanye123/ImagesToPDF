# 项目协作说明

适用于整个仓库；`ImgsToPDF/AGENTS.md` 补充 WinForms 界面约束。

## 结构与阅读顺序

- 技术栈为 C# 7.3、.NET Framework 4.8、Windows x64。沿用现有依赖和四空格缩进。
- `ImgsToPDF` 是 WinForms 程序，负责输入队列、专业整理、预览及启动核心进程。
- `ImgsToPDFCore` 是 CLI 程序，负责 Lua 配置、压缩包预处理、图像编辑和 PDF 写入。
- 主调用链为 `StartButton_Click → ExportPath → RunCoreExport → RunProcess`；UI 实际运行输出目录中的 `Core/ImgsToPDFCore.exe`。
- `ExportController.cs` 管理导出快照、进度、命名停止事件和退出状态；`ExportSettings.cs` 管理持久化的全局输出目录配置。
- 核心调用链为 `Program.Run → IConfig.PreProcess/config.lua → PDFWrapper → IConfig.PostProcess`。
- `PageManifest.cs`、`ImageFileExtensions.cs` 和 `WebPWrapper.cs` 由 UI 工程链接核心目录下的源码。修改这些文件要同时考虑两个工程。
- 先读相关入口和直接调用，再定位实现。`ARCHITECTURE.md` 中的 `--fast`、`ButtonClickAction` 等早期描述已过时；当前参数以 `ImgsToPDFCore/Program.cs` 为准，历史设计按实际源码核实。

## 行为契约

- 整理操作只改变导出结果，保留原始图片。先旋转，再按旋转后画面的归一化坐标裁剪。
- 支持单页、双页从左到右、双页从右到左。封面及横图单独输出并中断配对；仅相邻纵图拼接，末尾单张保留。
- 图库预览和核心导出使用同一分组规则。页面清单严格保留传入顺序；普通目录和旧文件列表入口继续使用 Lua 文件比较器。
- `--page-manifest` 使用带版本头的页面清单，保存路径、旋转、裁剪和封面。它是导出协议，当前不保存完整编辑会话。
- 界面和 CLI 的质量选项为 75、85、90、原图，默认 85。CLI 的原图值 0 在内部转换为 `OriginalQuality = -1`。
- 原图模式中，未旋转、未裁剪的 JPEG 在单页及双页独立封面/横图路径直接嵌入；编辑页和拼接页继续使用位图处理。
- 统一宽度预扫描读取尺寸并计算编辑后的大小；与实际裁剪共用取整和边界规则，避免恢复为全图编辑预扫描。
- 逐行透明检测降低临时像素缓冲分配。其他 alpha 格式的处理、坏图跳过及清单校验均有实际用途，简化时先核实契约。
- CLI 参数、`IConfig` 和 Lua 配置入口可能被独立使用。删除前追踪命令行、Lua 和源码调用，不能只根据 UI 是否调用判断。
- 全局输出目录通过 `--output-directory` 传入，未设置时沿用 Lua 路径规则。同名文件自动编号；先写同目录临时文件，成功关闭并检查停止后才提交，保留已有 PDF。
- `--progress` 输出 `PROGRESS|processed|total|written` 和提交后的 `OUTPUT|path`；输入处理数与 PDF 页数分别统计。退出码 0 为成功，1 为错误，2 为合作停止。`--cancel-event` 指定 UI 创建的命名事件，停止检查包括预扫描、页边界和解压条目边界。
- 左右拆分后的逻辑页允许共用源路径，导出及图库恢复不得按路径去重。撤销/重做最多保存 50 步元数据，历史不持有位图。

## 构建与运行文件

- 解决方案为根目录的 `ImgsToPDF.sln`，日常使用 `Debug|x64`，分发使用 `Release|x64`。
- 核心工程的构建后事件会覆盖源码树中的 `ImgsToPDF/Core`；UI 工程随后把它复制到运行输出目录。修改前检查 `git status`，保留已有二进制、Lua 和生成物改动。
- 有待保留的 `Core` 改动时，禁用构建后事件。从已将 MSBuild 加入 PATH 的 Developer Shell 执行：

  ```powershell
  git-bash -lc 'MSYS_NO_PATHCONV=1 msbuild ImgsToPDF.sln /p:Configuration=Debug /p:Platform=x64 /p:PostBuildEvent= /p:RunPostBuildEvent=Never /verbosity:minimal /nologo'
  ```

  MSBuild 不在 PATH 时，使用 Visual Studio Installer 的 `vswhere.exe` 定位 `MSBuild.exe`，替换命令中的程序路径。

- 禁用事件后，要运行新 UI 和新核心，可仅更新生成的运行目录。以下 PowerShell 命令从仓库根目录执行，保留源码树中的 `ImgsToPDF/Core`：

  ```powershell
  $runtimeDirectory = Join-Path $PWD 'ImgsToPDF/bin/x64/Debug/Core'
  New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
  Copy-Item -Path 'ImgsToPDF/Core/*' -Destination $runtimeDirectory -Recurse -Force
  Copy-Item -Path 'ImgsToPDFCore/bin/x64/Debug/ImgsToPDFCore.*' -Destination $runtimeDirectory -Force
  ```

- UI 运行文件为 `ImgsToPDF/bin/x64/Debug/ImgsToPDF.exe`。构建 Release 时相应替换配置及输出路径。
- `bin/`、`obj/` 是生成物；业务修改应落在源码，不把运行目录中的补丁作为交付。

## 验证与性能实验

- 当前没有独立测试工程。文档、注释和纯展示修改仅回读；功能修改针对本次改变的核心行为做一次最小验证，通过即停止。
- UI 修改列出人工验收 Todo。禁止 UI 单元测试；仅在用户明确要求时运行 Computer Use 或浏览器控制验证。
- PDF 核对页数、尺寸、顺序及实际绘制的主图；透明遮罩可能增加图像资源，不能假设每页 XObject 恰好一个。
- JPEG 直通可比对原始压缩字节；清理前后可比对主图和遮罩字节。PDF 时间戳、文档 ID 不适合作为输出等价条件。禁止 SHA-256 验证。
- 性能或消融实验固定输入及构建条件，记录原始数据，并区分完整导出、尺寸预扫描和透明扫描耗时。托管分配计数不包含 GDI+ 原生像素内存，不能代替总内存指标。
- 只有用户要求实验时才安排变体对照和必要的重复测量；不因日常改动自动增加基准、兼容矩阵或全量测试。

## 修改与交付

- 优先直接复用现有实现。删除旧代码需有调用关系或不可达条件的证据；保留实际承担并发、输入校验、图片所有权和错误反馈的保护。
- 小型顺序任务直接完成；独立且收益高于协调成本的任务可以委派，文件写入范围须明确。
- 保留用户已有修改；文件级删除移入回收站。标识符使用英文，新增注释在关键处简短说明原因。
- 完成说明简述改动、实际验证和必要的人工验收。功能探索应说明现有缺口、最小实现范围和取舍；探索建议不作为已经实现的功能记录。
