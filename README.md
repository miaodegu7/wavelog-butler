# Wavelog 管家

轻量 Windows 桌面软件，使用 C# / .NET 8 WinForms，无 Python、Electron、Node.js 或服务器依赖。

## 功能

- 添加不同 Wavelog 网站或同一网站的多个账号，分别配置只读 API 密钥。
- 按台站分页读取 ADIF；每次全量刷新，成功后替换缓存，避免重复导入并更新旧日志。失败保留旧缓存。
- 精确搜索友台呼号，显示全部账号，包含未命中、未完成同步和同步失败状态。
- 展示我方台站、UTC 时间、波段、模式、报告、纸卡状态、QSL Manager 和日志中的地址。
- QRZ 和 Club Log 快捷链接；导出当前呼号的全部命中通联为 UTF-8 CSV 寄卡清单。
- 不向 Wavelog 写入或修改任何通联。

## 下载与使用

GitHub 仓库 → Actions → 最近成功的 Windows desktop build → Artifacts。

- lightweight：体积小，需要安装 **.NET 8 Desktop Runtime x64**（不是仅普通 .NET Runtime）。
- standalone：包含运行库，解压后直接运行 WavelogButler.exe。

适用于 Windows 10/11 x64。首次运行未签名软件可能显示 SmartScreen 提示，请核对来源。

1. 在每个 Wavelog 账号中创建只读 API Key。
2. 点击“添加账号”，填写备注、HTTPS 网站根地址、密钥。不要填写 /api 或接口地址；子目录安装请保留子目录。
3. 点击“同步全部”。如需所有台站，请关闭 Wavelog 的“仅活动日志”选项。
4. 输入友台呼号并搜索。JA1XXX 与 JA1XXX/P 独立匹配。
5. 打开 QRZ / Club Log 核对对方寄卡指引，导出清单。

## 数据与安全

本地数据位于 %LOCALAPPDATA%\WavelogButler\accounts.json，不上传 GitHub。
API 密钥使用 Windows DPAPI 按当前用户加密，复制到另一台电脑或其他用户后需重新填写。
日志缓存未加密，包含呼号、地址等信息，请保护自己的 Windows 账户和备份。
仅接受 HTTPS，不跳过证书检查，不自动跟随 API 重定向。
旧版 Wavelog 台站 API 的密钥在 URL 路径中，服务器管理员应避免记录此路径；本软件不输出密钥日志。
软件本身无登录系统，只连接用户授权的 Wavelog 账号。

## 边界说明

当前版本是搜索与清单 MVP，不含自动抓取 QRZ 地址、寄卡批次管理、标签打印、状态回写或多平台支持。
Club Log 链接不代表对方一定公开日志或支持 OQRS；QRZ 部分信息可能需要登录或订阅。
地址来自原始日志，可能缺失或过时，寄卡前必须人工核对。电子确认不等同纸卡寄出。
“未检索到”仅针对 API 返回的台站范围及最后同步时的数据；同步不是服务器数据库的事务快照。
每次全量同步适合个人及小团体。超大日志会增加内存和同步耗时，后续可迁移到 SQLite 与增量核对。
尚未使用真实 Wavelog 账号进行联调，需要实际服务器地址与只读密钥验证部署版本兼容性。

## 开发与编译

安装 .NET 8 SDK 后执行：

    dotnet build -c Release
    dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist/lightweight

.github/workflows/build.yml 在推送 main、v 开头的标签或手动触发时生成两种 Windows 构建产物。
API 依据：https://github.com/wavelog/wavelog/blob/master/application/controllers/Api.php
