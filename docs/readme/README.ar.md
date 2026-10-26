<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.de.md">Deutsch</a><br/>
  <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.it.md">Italiano</a> · <a href="README.ru.md">Русский</a> · <b>العربية</b>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="الإصدار"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="المنصة"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="الترخيص"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="البناء"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="التوثيق"></a>
</p>

## نظرة عامة

يجمع WinTools أعمال الصيانة الشائعة في نافذة واحدة: تنظيف الذاكرة المؤقتة وبقايا التحديثات، وإيقاف القياسات غير اللازمة، وإزالة تحديث مشكّل، ومراجعة ما يبدأ مع النظام، وتثبيت بيئات التشغيل الناقصة، وتصدير برامج التشغيل القديمة، وإنشاء صورة قبل إعادة التثبيت.

تطبيق WPF يعمل محليًا بالكامل. لا خدمة، ولا برنامج تشغيل، ولا تشغيل تلقائي، ولا اتصال بالشبكة. إغلاق النافذة ينهي العملية.

عشرة أقسام: **التنظيف، التحسين، ميزات النظام، التحديثات، بدء التشغيل، المكونات، بيئات التشغيل، التطبيقات، برامج التشغيل، النسخ الاحتياطي.**

## المتطلبات

- Windows 10 إصدار 1809 (17763) أو أحدث، أو Windows 11، بمعمارية x64
- النسخة المعتمدة على إطار العمل تحتاج [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- صلاحيات المسؤول (معلنة في البيان)؛ تعتمد عليها عمليات DISM وبرامج التشغيل والخدمات والصور
- بعض الميزات تستدعي مكونات من Windows: DISM وWindows Update Agent و`pnputil` وWIMGAPI. قد تكون محذوفة في النسخ المقلّمة

## التنزيل

| النسخة | الحجم | ملاحظة | الرابط |
| --- | --- | --- | --- |
| محمولة | نحو 70 ميغابايت | تتضمن بيئة التشغيل، فك الضغط والتشغيل | [WinTools-*-portable-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| معتمدة على إطار العمل | نحو 22 ميغابايت | تحتاج .NET 10 Desktop Runtime | [WinTools-*-framework-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | نفسه | تُعاد بناؤها مع كل دفع إلى `main` | [releases/tag/beta](https://github.com/PIKACHUIM/WinTools/releases/latest) |

النسخ الرسمية موقّعة سحابيًا من Certum مع ختم زمني، ويمكن التحقق منها عبر `Get-AuthenticodeSignature`. يُرفق ملف `SHA256SUMS.txt` مع كل إصدار.

## الميزات

اضغط على العنوان لتوسيع القسم؛ وعند الطي يشغل سطرًا واحدًا.

<details open>
<summary><b>نظرة عامة على النظام</b> — الشاشة الأولى</summary>

إصدار النظام ورقم البناء، واستخدام المعالج والذاكرة، والمساحة الحرة لكل قرص، والنشاط الأخير. تُقرأ جميع البيانات محليًا.

![نظرة عامة على النظام](../public/screenshots/dashboard.png)

</details>

<details>
<summary><b>التنظيف</b> — 23 فئة و1623 قاعدة</summary>

يفحص الأقراص كاملة ويغطي مجلدات النظام المؤقتة وذاكرة التحديثات والسجلات وملفات التفريغ وذاكرة المتصفحات وذاكرة أدوات التطوير (npm وpip وNuGet وGradle وDocker) ومجلدات الذاكرة المؤقتة لأكثر من 150 تطبيقًا.

تُجمّع النتائج حسب الفئة مع الحجم ومستوى الأمان. تُتجاوز الملفات المشغولة وتُسجَّل بدل تعطيل الفحص كله. القواعد ملفات JSON في `src/WinTools.Infrastructure/Rules/`، وإضافة مسار لا تتطلب تعديل الشيفرة.

![التنظيف](../public/screenshots/cleanup.png)

</details>

<details>
<summary><b>التحسين</b> — 153 بندًا في 7 مجموعات</summary>

الخصوصية والقياسات، والأداء، ومستكشف الملفات، وسلوك النظام، والشبكة، والتحديثات، والألعاب.

يدعم كل بند الفحص والتطبيق والتراجع، فتبدو الحالة الحالية قبل أي تغيير. تقتصر البنود الموصى بها على ذات الأثر المعلوم.

![التحسين](../public/screenshots/optimize.png)

</details>

<details>
<summary><b>ميزات النظام</b> — ميزات النظام وقناة الإصدار</summary>

- **ميزات النظام**: يعرض كل الميزات الاختيارية عبر واجهة DISM، مجمّعة حسب الغرض، مع الوصف والحالة الحالية، وتُطبَّق التغييرات دفعة واحدة.
- **قناة الإصدار**: يقرأ SKU والإصدار وحالة التنشيط. يعتمد التبديل على `changepk` بمفتاح عام ولا ينشّط Windows.

![ميزات النظام](../public/screenshots/features-systemfeatures.png)

</details>

<details>
<summary><b>التحديثات</b> — المثبتة والمتاحة وأعلام الميزات</summary>

- **المثبتة**: سرد وإزالة تحديث واحد.
- **المتاحة**: تشغيل بحث Windows Update يدويًا مع الفئة وKB والحجم والتاريخ، وتثبيت دفعي وملخص للنتائج.
- **أعلام الميزات**: إدارة `FeatureManagement\Overrides` حسب المعرّف مع الحالة والأولوية والمتغيّر، إضافة إلى الاستيراد والتصدير وإعادة الضبط.

![التحديثات المتاحة](../public/screenshots/updates-available.png)

</details>

<details>
<summary><b>بدء التشغيل</b> — عناصر البدء والمهام والخدمات</summary>

كل ذلك في صفحة واحدة. أيقونات البرامج الحقيقية ومفاتيح للحالة. يمكن تغيير نوع بدء الخدمة داخل السطر (تلقائي، تلقائي متأخر، يدوي، معطّل).

![بدء التشغيل](../public/screenshots/startup-services.png)

</details>

<details>
<summary><b>المكونات</b> — مركز الأمان ومكونات النظام</summary>

- **مركز الأمان**: 14 إعدادًا تشمل الحماية الفورية والسحابية ومراقبة السلوك وPUA وحماية الشبكة والوصول المتحكّم بالمجلدات وسلامة الذاكرة وUAC وSmartScreen وملفات تعريف جدار الحماية الثلاثة والاستثناءات.
- **مكونات النظام**: التطبيقات المدمجة (Terminal وآلة الحاسبة والمفكرة وPaint والصور وغيرها) للتثبيت والإصلاح والإزالة؛ توزيعات WSL؛ النظام الفرعي لأندرويد من حزمة محلية؛ إصلاح المتجر.

![مركز الأمان](../public/screenshots/components-securitycenter.png)

</details>

<details>
<summary><b>بيئات التشغيل</b> — تثبيت .NET وVC++ عبر الإنترنت</summary>

يعرض بيئات .NET وVisual C++ المثبتة. تُجلب الإصدارات المتاحة عند الطلب: .NET من `releases-index.json` الرسمي مع التحقق من SHA512، وVC++ عبر روابط `aka.ms` مع التحقق من Authenticode. كلاهما يُثبَّت بصمت ويمكن إزالته.

![بيئات التشغيل](../public/screenshots/runtimes-dotnet.png)

</details>

<details>
<summary><b>التطبيقات</b> — البرامج والحزم المثبتة</summary>

يعرض الأيقونة والإصدار والمساحة المشغولة وتاريخ التثبيت وآخر استخدام. تستدعي الإزالة برنامج إزالة الخاص بالتطبيق، أما الحزم فتمر عبر واجهة النشر.

![التطبيقات](../public/screenshots/apps-programs.png)

</details>

<details>
<summary><b>برامج التشغيل</b> — عمليات دفعية في صفحة واحدة</summary>

يدير DriverStore بالكامل. اختر أي عدد من الحزم لنسخها احتياطيًا أو إزالتها أو تثبيتها من مجلد. تُظهر العمليات الدفعية التقدّم لكل عنصر وملخصًا للنتائج.

![برامج التشغيل](../public/screenshots/drivers-systemdrivers.png)

</details>

<details>
<summary><b>النسخ الاحتياطي</b> — إنشاء صورة WIM وتطبيقها</summary>

يُختار مجلد المصدر من قائمة؛ ويحدّد تنسيق الصورة (wim / esd / swm) الامتداد وطريقة الضغط؛ وحجم التقسيم ينطبق على swm فقط؛ وتُعرض قائمة الاستثناءات الافتراضية قبل الإنشاء. عند التطبيق يمكن التحقق من الصورة وإصلاح الإقلاع عبر `bcdboot`.

![النسخ الاحتياطي](../public/screenshots/backup-capture.png)

</details>

## البناء

يتطلب .NET 10 SDK.

```powershell
# بناء التصحيح
dotnet build src/WinTools.App/WinTools.App.csproj

# بناء Release
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# إنشاء الحزمتين في artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

تفاصيل البنية الداخلية والأقسام وتنسيق القواعد والتعريب والتنقيح في **[wtools.opkg.cn](https://wtools.opkg.cn/)** أو [docs/guide/development.md](../guide/development.md).

## مشاريع مرجعية

| المشروع | ما استُفيد منه |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | تجميع بنود التحسين وتدفّق الاختيار ثم التطبيق |
| [Optimizer](https://github.com/hellzerg/optimizer) | فئات التحسين وصياغة المخاطر وتنظيم البدء والخدمات |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | قائمة التحسين وكثافة المعلومات للمستخدمين الصينيين |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | استخدام DISM وتنظيم الموارد متعددة اللغات |
| [Geek Uninstaller](https://geekuninstaller.com/) | اختيار حقول قائمة البرامج المثبتة |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | بنية بيانات أعلام الميزات ودلالة الحالات الثلاث |

بدأت قواعد التنظيف من قائمة شخصية لمسارات الذاكرة المؤقتة ثم توسّعت تدريجيًا. تُصان الآن في `tools/rules/*.txt` وتُولَّد عبر `tools/Generate-CleanupRules.ps1`.

## إخلاء المسؤولية

- الأداة تعدّل التسجيل وأنواع بدء الخدمات وميزات النظام وبرامج التشغيل. أنشئ نقطة استعادة أو انسخ بياناتك قبل الاستخدام.
- لا يتحمّل المؤلف مسؤولية فقدان البيانات أو تلف النظام الناتج عن الاستخدام.
- لا تستخدم الأداة للالتفاف على التراخيص أو التنشيط أو أي غرض يخالف القوانين المحلية.

## الترخيص

[AGPL-3.0](../../LICENSE) © WinTools

### مكونات طرف ثالث

| المكوّن | الترخيص | الاستخدام |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | خط الواجهة، مضمَّن بصيغة TTF |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | مولّدات شيفرة MVVM |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | حقن التبعيات |
| [VitePress](https://vitepress.dev/) | MIT | لموقع التوثيق فقط |
