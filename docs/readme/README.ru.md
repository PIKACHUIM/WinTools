<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.de.md">Deutsch</a><br/>
  <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.it.md">Italiano</a> · <b>Русский</b> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="Версия"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="Платформа"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="Лицензия"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="Сборка"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="Документация"></a>
</p>

## Обзор

Перед переустановкой Windows стоит сохранить драйверы; старой программе может понадобиться среда выполнения, а кеш обновлений со временем разрастается. Такие мелочи обычно заставляют переходить между разными системными утилитами. WinTools собирает самые частые из них в одном настольном приложении.

Это приложение WPF: оно не работает постоянно в фоне и не добавляется в автозагрузку. К сети оно обращается только при поиске обновлений, загрузке сред выполнения или запросе онлайн-списка дистрибутивов WSL. Закрыли окно — процесс завершён.

Десять модулей: **Очистка, Оптимизация, Компоненты системы, Обновления, Автозапуск, Компоненты, Среды выполнения, Приложения, Драйверы, Резервное копирование.**

## Требования

- Windows 10 1809 (17763) или новее, либо Windows 11, x64
- Версии, зависящей от платформы, нужен [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Права администратора (объявлены в манифесте): от них зависят DISM, драйверы, службы и работа с образами
- Часть функций обращается к компонентам Windows: DISM, Windows Update Agent, `pnputil`, WIMGAPI. В урезанных образах их может не быть

## Загрузка

| Вариант | Примечание | Ссылка |
| --- | --- | --- |
| Портативный | Среда выполнения включена, распаковать и запустить | [WinTools-*-green-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Зависит от платформы | Нужен .NET 10 Desktop Runtime | [WinTools-*-frame-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | Пересобирается при каждом push в `main` | [Предварительная версия Beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

Официальные сборки подписаны облачным сертификатом Certum с отметкой времени; проверка через `Get-AuthenticodeSignature`. К каждой версии прилагается `SHA256SUMS.txt`.

## Возможности

### Обзор системы

Версия и номер сборки, загрузка ЦП и памяти, свободное место по дискам, последняя активность. Все данные читаются локально.

<details>
<summary>Скриншот</summary>

![Обзор системы](../public/screenshots/dashboard.png)

</details>

### Очистка

Сканирует диски целиком: временные папки системы, кеш обновлений, журналы и дампы, кеши браузеров, кеши инструментов разработки (npm, pip, NuGet, Gradle, Docker) и кеш-папки более 150 приложений.

Результаты группируются по категориям с размером и уровнем безопасности. Занятые процессами файлы пропускаются и записываются в отчёт, не останавливая всю очистку. Правила — обычный JSON в `src/WinTools.Infrastructure/Rules/`; добавление пути не требует правки кода.

<details>
<summary>Скриншот</summary>

![Очистка](../public/screenshots/cleanup.png)

</details>

### Оптимизация

Конфиденциальность и телеметрия, производительность, Проводник, поведение системы, сеть, обновления, игры.

Каждый пункт поддерживает проверку, применение и откат, поэтому текущее состояние видно до изменений. В рекомендуемые входят только пункты с понятным эффектом.

<details>
<summary>Скриншот</summary>

![Оптимизация](../public/screenshots/optimize.png)

</details>

### Компоненты системы

- **Компоненты системы**: список всех дополнительных компонентов через API DISM, сгруппированный по назначению, с описанием и текущим состоянием; изменения применяются пакетом.
- **Канал выпуска**: читает SKU, версию и состояние активации. Переключение использует штатный `changepk` с универсальным ключом и не активирует Windows.

<details>
<summary>Скриншот</summary>

![Компоненты системы](../public/screenshots/features-systemfeatures.png)

</details>

### Обновления

- **Установленные**: список и удаление по одному.
- **Доступные**: ручной поиск в Windows Update с категорией, KB, размером и датой, пакетная установка и сводка.
- **Флаги компонентов**: управление `FeatureManagement\Overrides` по ID с состоянием, приоритетом и вариантом, а также импорт, экспорт и сброс.

<details>
<summary>Скриншот</summary>

![Доступные обновления](../public/screenshots/updates-available.png)

</details>

### Автозапуск

Всё на одной странице. Настоящие значки программ, переключатели состояния. Службам можно менять тип запуска прямо в строке (авто, авто с задержкой, вручную, отключена).

<details>
<summary>Скриншот</summary>

![Автозапуск](../public/screenshots/startup-services.png)

</details>

### Компоненты

- **Центр безопасности**: 14 параметров — защита в реальном времени, облачная защита, мониторинг поведения, PUA, сетевая защита, контролируемый доступ к папкам, целостность памяти, UAC, SmartScreen, три профиля брандмауэра и исключения.
- **Системные компоненты**: встроенные приложения (Терминал, Калькулятор, Блокнот, Paint, Фотографии и другие) — установка, восстановление, удаление; дистрибутивы WSL; подсистема Android из локального пакета; восстановление Store.

<details>
<summary>Скриншот</summary>

![Центр безопасности](../public/screenshots/components-securitycenter.png)

</details>

### Среды выполнения

Показывает установленные среды .NET и Visual C++. Доступные версии загружаются по требованию: .NET из официального `releases-index.json` с проверкой SHA512, VC++ по ссылкам `aka.ms` с проверкой Authenticode. Обе ставятся в тихом режиме и могут быть удалены.

<details>
<summary>Скриншот</summary>

![Среды выполнения](../public/screenshots/runtimes-dotnet.png)

</details>

### Приложения

Показывает значки, версии, примерный размер и сведения об установке. Для обычных программ запускается их собственный деинсталлятор; Appx-пакеты обрабатываются встроенными средствами Windows.

<details>
<summary>Скриншот</summary>

![Приложения](../public/screenshots/apps-programs.png)

</details>

### Драйверы

Управление всем DriverStore. Отметьте любое число пакетов и сохраните их, удалите или установите из папки. Пакетные операции показывают прогресс по каждому элементу и итоговую сводку.

<details>
<summary>Скриншот</summary>

![Драйверы](../public/screenshots/drivers-systemdrivers.png)

</details>

### Резервное копирование

Исходный том выбирается из списка; формат образа (wim / esd / swm) определяет расширение и сжатие; размер разбиения действует только для swm; перед созданием показывается список исключений по умолчанию. При применении можно проверить образ и восстановить загрузку через `bcdboot`.

<details>
<summary>Скриншот</summary>

![Резервное копирование](../public/screenshots/backup-capture.png)

</details>

## Сборка

Требуется .NET 10 SDK.

```powershell
# Отладочная сборка
dotnet build src/WinTools.App/WinTools.App.csproj

# Сборка Release
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# Оба пакета в artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

Архитектура, устройство модулей, формат правил, локализация и отладка описаны на **[wtools.opkg.cn](https://wtools.opkg.cn/)** или в [docs/guide/development.md](../guide/development.md).

## Использованные проекты

| Проект | Что взято |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | Группировка настроек, схема «выбрать и применить» |
| [Optimizer](https://github.com/hellzerg/optimizer) | Категории, формулировки рисков, организация автозапуска и служб |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | Список настроек и плотность информации для китайской аудитории |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | Работа с DISM и организация многоязычных ресурсов |
| [Geek Uninstaller](https://geekuninstaller.com/) | Набор полей в списке установленных программ |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | Модель данных флагов и трёхсостоянийная логика |

Правила очистки выросли из личного списка путей кеша и постепенно дополнялись. Сейчас они ведутся в `tools/rules/*.txt` и генерируются скриптом `tools/Generate-CleanupRules.ps1`.

## Отказ от ответственности

- Программа изменяет реестр, типы запуска служб, компоненты системы и драйверы. Перед использованием создайте точку восстановления или резервную копию.
- Автор не несёт ответственности за потерю данных или повреждение системы в результате использования.
- Не используйте программу для обхода лицензирования, активации или в иных целях, нарушающих местное законодательство.

## Лицензия

[AGPL-3.0](../../LICENSE) © WinTools

### Сторонние компоненты

| Компонент | Лицензия | Назначение |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | Шрифт интерфейса, встроен как TTF |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | Генераторы кода MVVM |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | Внедрение зависимостей |
| [VitePress](https://vitepress.dev/) | MIT | Только для сайта документации |
