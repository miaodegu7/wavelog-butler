# Wavelog 管家 · Windows 桌面版

C# / .NET 8 WPF + SQLite。无 Python、Electron 或独立数据库服务器。

## 本版功能

- 浅色卡片界面与侧栏导航。按账户分组，以我方呼号为卡片标题；未命中账户也保留。
- 通联列表显示台站名称，不显示账户名和子模式；台站名称缺失时显示来源台站 ID。CSV 同样包含台站列。普通通联显示波段/频率，卫星通联优先显示 SAT_NAME，缺失时明确提示。
- 邮寄地址按多行完整展示：保留接口返回的全部编号地址字段、姓名、州、省邮编和国家，以及字段内部换行；导出保留多行地址。接口未公开的信息无法补取。
- 搜索直接读取本地 SQLite 持久化日志；重启或离线后仍可查询。
- 多个 Wavelog 只读 API 来源，分页导入，按账户事务提交。失败不替换旧数据。
- 重复同步更新已有记录；远端未返回的记录保留并标记待核对，不静默删除。
- 自动迁移旧版 accounts.json，原文件保留。支持数据库在线备份。
- 搜索同时调用 QRZ 官方 XML 接口，查询国家、邮箱、地址和 QSL Manager；提供 QRZ / Club Log 网页入口和 CSV 寄卡清单导出。

## 下载

仓库 → Actions → 最新成功的 Windows desktop build → Artifacts：

- WavelogButler-win-x64-lightweight：需要 .NET 8 Desktop Runtime x64。
- WavelogButler-windows-installer：Windows 安装程序，推荐普通用户使用。
- WavelogButler-win-x64-standalone：自带运行库的便携版，解压运行 WavelogButler.exe。

适用于 Windows 10/11 x64。软件未签名，Windows 可能显示 SmartScreen 提示，请核对下载来源。

## 使用

1. 日志管理 → 添加账户，输入备注、HTTPS 网站根地址、只读 API Key；不要填 /api，子目录安装保留子目录。
2. 同步全部日志。Wavelog “仅活动日志”会限制台站列表，如需全部台站请关闭该选项。
3. 通联查询输入完整呼号。JA1XXX 和 JA1XXX/P 精确区分。
4. 首次需要联网资料时会提示 QRZ 登录。到“资料查询设置”填写 QRZ 用户名、密码。
5. QRZ 必须具备 XML 查询权限 / 相应订阅。Wavelog API Key 不能代替 QRZ 授权。未公开、无权限、网络失败会明确显示，不猜测邮箱或地址。
6. 未配置或查询失败时可显示日志中的资料，并注明来源。实时 QRZ 结果只在当前界面使用，不建立批量地址库。
7. 寄出前核对 QSL Manager 和对方主页指引：登记地址不一定是 QSL 收件地址。

## 数据与安全

数据库：%LOCALAPPDATA%\WavelogButler\logs.db。通联同步成功后写入 SQLite 存储，搜索从数据库读取，不依赖临时内存缓存；旧数据不会因为重新搜索或重启而被覆盖。同步时的单页数据会短暂驻留内存，写入后立即释放，这是网络分页解析所必需的。
旧 JSON：同目录 accounts.json，仅用于首次迁移，迁移成功后不再用于查询。
API 密钥和 QRZ 密码使用 Windows DPAPI 按当前用户加密；日志及地址字段未加密。
备份文件同样包含私人日志，请妥善保存。凭据换电脑或 Windows 用户后通常需重新填写。
恢复备份：关闭软件，先备份现有 logs.db，再将备份复制为该路径的 logs.db。
所有 API 连接必须使用 HTTPS，不跳过证书验证，不自动跟随重定向，不输出凭据日志。
Wavelog 旧 API 和 QRZ XML 登录协议使用 URL 参数/路径传递凭据，请保护代理与服务器访问日志。
GitHub 仅存代码和编译产物，不包含个人数据库或密钥。

## 同步边界

为了核对旧日志修改，本版使用全量分页读取。搜索不从缓存或网络获取日志，而是查 SQLite。
来源内部以台站、呼号、日期时间、波段、模式、频率和卫星等字段识别通联；同一组合的重复记录保留出现次数。
关键身份字段改变会将旧记录标为待核对并保存新记录；不会擅自判断两条记录一定是同一通联。
超大日志同步仍会占用内存并短暂等待写入，不适合百万级日志。台站接口返回范围和同步时间决定“未检索到”的含义。
当前不含寄卡批次、标签打印或状态回写；不修改 Wavelog 原始日志。

## 开发

安装 .NET 8 SDK：

    dotnet build -c Release
    dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/lightweight

GitHub Actions 推送 main、v 开头标签或手动触发时编译两种版本。
已进行隔离数据库和 WPF 渲染验证；真实 Wavelog / QRZ 授权账号需由用户配置后联调。

API 参考：
- https://github.com/wavelog/wavelog/blob/master/application/controllers/Api.php
- https://www.qrz.com/XML/current_spec.html
