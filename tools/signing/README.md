# 代码签名

Windows 产物的数字签名使用 **Certum 云签名（SimplySign Desktop）**。
私钥在 Certum 云端、不可导出，SimplySign Desktop 登录后把云证书以「虚拟卡片」形式挂进本机证书库，
`signtool` 用证书指纹引用它签名，摘要 SHA256，时间戳走 Certum 的 TSA。

脚本移植自 [TPMSmartCard](https://github.com/PIKACHUIM/TPMSmartCard) 的 `Builds/` 目录，
只把环境变量前缀 `TPMSMART_*` 换成 `WINTOOLS_*`，因此两个项目可以共用同一批 Secrets。

| 文件 | 作用 |
| --- | --- |
| `CertumSigning.ps1` | 公共库（定位 signtool、枚举证书、签名、时间戳回退），只提供函数 |
| `Sign-SetupCertum.ps1` | 装 SimplySign Desktop → 写注册表 → TOTP 自动登录 → 校验证书 |
| `Sign-Windows.ps1` | 给文件或目录签名，可 `-ListOnly` 预览 |

## 环境变量

| 变量 | 说明 |
| --- | --- |
| `CERTUM_USERNAME` | SimplySign 登录账号（只有准备环境时用） |
| `CERTUM_OTP_URI` | `otpauth://totp/...?secret=...`，用于生成 TOTP 动态口令 |
| `CERTUM_CERTIFICATE_SHA1` | 云证书指纹；不填则自动挑 Certum 签发、未过期的证书 |
| `CERTUM_TIMESTAMP_URL` | 可选，覆盖默认时间戳服务 |
| `CERTUM_EXE_PATH` | 可选，SimplySign Desktop 路径 |
| `WINTOOLS_SIGNTOOL` | 可选，显式指定 `signtool.exe` |
| `WINTOOLS_SIGN_SKIP=1` | 跳过签名，构建继续 |
| `WINTOOLS_SIGN_REQUIRED=1` | 拿不到证书或签名失败就终止（CI 发布用） |

## 本地使用

```powershell
# 1) 准备环境（只需装一次；每次重启后要重新登录）
$env:CERTUM_USERNAME = 'your-account'
$env:CERTUM_OTP_URI  = 'otpauth://totp/Certum:your-account?secret=XXXX&algorithm=SHA256&digits=6&period=30'
$env:CERTUM_CERTIFICATE_SHA1 = '929F16F67222DCFA6A3C15A774F5F460FA79FED1'
.\tools\signing\Sign-SetupCertum.ps1

# 2) 看会签哪些文件、用哪张证书
.\tools\signing\Sign-Windows.ps1 -Path 'artifacts\publish' -Recurse -ListOnly

# 3) 签名
$env:WINTOOLS_SIGN_REQUIRED = '1'
.\tools\signing\Sign-Windows.ps1 -Path 'artifacts\publish\framework' -Extensions .exe,.dll
```

## CI 使用

`.github/workflows/build.yml` 会在构建完成后自动执行：

1. `Sign-SetupCertum.ps1 -Install -ConfigureRegistry` —— 装 SimplySign 并让登录框自动弹出
2. `Sign-SetupCertum.ps1 -Connect -Verify` —— TOTP 自动登录，轮询证书库确认私钥可用（失败重试 3 轮）
3. `Sign-Windows.ps1` —— 给 `WinTools.exe` 与三个自有 DLL 签名
4. `Get-AuthenticodeSignature` —— 逐个校验状态为 `Valid` 且指纹匹配

需要在仓库 **Settings → Secrets and variables → Actions** 里配置
`CERTUM_USERNAME`、`CERTUM_OTP_URI`、`CERTUM_CERTIFICATE_SHA1`。

- **正式发布（release）**：三个 Secrets 缺任何一个，构建直接失败——不会发布未签名产物。
- **beta 构建**：缺 Secrets 只提示警告，产物照常发布（不带签名）。

## 已知限制

- SimplySign 登录依赖窗口焦点与 `SendKeys`，GitHub 托管的 `windows-latest` 是交互式会话可以工作；
  自建 runner 若以服务方式运行（Session 0）会失败。
- 时间戳服务偶尔超时，脚本会按 Certum → DigiCert → Sectigo 顺序回退。
- 签名后文件哈希会变，因此 `SHA256SUMS.txt` 必须在签名之后生成（工作流已按此顺序）。
