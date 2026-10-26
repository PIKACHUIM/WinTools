<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.de.md">Deutsch</a><br/>
  <a href="README.fr.md">Français</a> · <b>Español</b> · <a href="README.pt.md">Português</a> · <a href="README.it.md">Italiano</a> · <a href="README.ru.md">Русский</a> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="Versión"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="Plataforma"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="Licencia"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="Compilación"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="Documentación"></a>
</p>

## Introducción

Antes de reinstalar Windows toca guardar los controladores; algunos programas antiguos necesitan un runtime y la caché de actualizaciones acaba creciendo. Para estas tareas solemos ir saltando entre varias herramientas del sistema. WinTools reúne las más habituales en una aplicación de escritorio.

Es una aplicación WPF: no se queda ejecutándose en segundo plano ni se añade al inicio. Solo se conecta al buscar actualizaciones, descargar runtimes o consultar la lista en línea de distribuciones WSL. Al cerrar la ventana, el proceso termina.

Diez módulos: **Limpieza, Optimización, Funciones, Actualizaciones, Inicio, Componentes, Runtimes, Aplicaciones, Controladores, Copia de seguridad.**

## Requisitos

- Windows 10 1809 (17763) o posterior, o Windows 11, x64
- La versión dependiente del framework necesita [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Permisos de administrador (declarados en el manifiesto); DISM, controladores, servicios e imágenes dependen de ellos
- Algunas funciones usan componentes de Windows: DISM, Windows Update Agent, `pnputil`, WIMGAPI. Pueden faltar en imágenes recortadas

## Descarga

| Versión | Nota | Enlace |
| --- | --- | --- |
| Portátil | Runtime incluido, descomprimir y ejecutar | [WinTools-*-green-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Dependiente del framework | Requiere .NET 10 Desktop Runtime | [WinTools-*-frame-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | Se recompila en cada push a `main` | [Versión preliminar Beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

Las versiones oficiales llevan firma en la nube de Certum con sello de tiempo, verificable con `Get-AuthenticodeSignature`. Cada versión incluye `SHA256SUMS.txt`.

## Funciones

### Resumen del sistema

Versión e número de compilación, uso de CPU y memoria, espacio libre por disco, actividad reciente. Todo se lee localmente.

<details>
<summary>Captura de pantalla</summary>

![Resumen del sistema](../public/screenshots/dashboard.png)

</details>

### Limpieza

Analiza discos completos y cubre carpetas temporales del sistema, caché de actualizaciones, registros y volcados, cachés de navegadores, cachés de herramientas de desarrollo (npm, pip, NuGet, Gradle, Docker) y las carpetas de caché de más de 150 aplicaciones.

Los resultados se agrupan por categoría con tamaño y nivel de seguridad. Los archivos en uso se omiten y se registran en lugar de bloquear el análisis. Las reglas son JSON en `src/WinTools.Infrastructure/Rules/`; añadir una ruta no requiere tocar código.

<details>
<summary>Captura de pantalla</summary>

![Limpieza](../public/screenshots/cleanup.png)

</details>

### Optimización

Privacidad y telemetría, rendimiento, Explorador de archivos, comportamiento del sistema, red, actualizaciones, juegos.

Cada entrada admite detección, aplicación y reversión, así que el estado actual se ve antes de cambiar nada. Los elementos recomendados se limitan a efectos bien conocidos.

<details>
<summary>Captura de pantalla</summary>

![Optimización](../public/screenshots/optimize.png)

</details>

### Funciones

- **Funciones del sistema**: lista todas las características opcionales mediante la API DISM, agrupadas por uso, con descripción y estado; los cambios se aplican en lote.
- **Canal de edición**: lee el SKU, la versión y el estado de activación. El cambio usa `changepk` con una clave genérica y no activa Windows.

<details>
<summary>Captura de pantalla</summary>

![Funciones del sistema](../public/screenshots/features-systemfeatures.png)

</details>

### Actualizaciones

- **Instaladas**: enumerar y desinstalar individualmente.
- **Disponibles**: lanzar manualmente una búsqueda de Windows Update con categoría, KB, tamaño y fecha, instalación por lotes y resumen.
- **Indicadores de función**: gestionar `FeatureManagement\Overrides` por ID con estado, prioridad y variante, además de importar, exportar y restablecer.

<details>
<summary>Captura de pantalla</summary>

![Actualizaciones disponibles](../public/screenshots/updates-available.png)

</details>

### Inicio

Todo en una página. Iconos reales de los programas, interruptores para el estado. Los servicios cambian su tipo de inicio en la propia fila (Automático, Automático diferido, Manual, Deshabilitado).

<details>
<summary>Captura de pantalla</summary>

![Inicio](../public/screenshots/startup-services.png)

</details>

### Componentes

- **Centro de seguridad**: 14 ajustes que cubren protección en tiempo real, protección en la nube, supervisión de comportamiento, PUA, protección de red, acceso controlado a carpetas, integridad de memoria, UAC, SmartScreen, los tres perfiles de firewall y las exclusiones.
- **Componentes del sistema**: aplicaciones integradas (Terminal, Calculadora, Bloc de notas, Paint, Fotos y más) para instalar, reparar o desinstalar; distribuciones WSL; subsistema Android desde un paquete local; reparación de Store.

<details>
<summary>Captura de pantalla</summary>

![Centro de seguridad](../public/screenshots/components-securitycenter.png)

</details>

### Runtimes

Lista los runtimes .NET y Visual C++ instalados. Las versiones disponibles se obtienen a demanda: .NET desde el `releases-index.json` oficial con verificación SHA512, VC++ desde enlaces `aka.ms` con verificación Authenticode. Ambos se instalan en silencio y se pueden desinstalar.

<details>
<summary>Captura de pantalla</summary>

![Runtimes](../public/screenshots/runtimes-dotnet.png)

</details>

### Aplicaciones

Muestra iconos, versiones, tamaño estimado e información de instalación. Para desinstalar un programa ejecuta su propio desinstalador; los paquetes Appx se gestionan con las herramientas incluidas en Windows.

<details>
<summary>Captura de pantalla</summary>

![Aplicaciones](../public/screenshots/apps-programs.png)

</details>

### Controladores

Gestiona todo el DriverStore. Selecciona cualquier cantidad de paquetes para copiarlos, eliminarlos o instalarlos desde una carpeta. Las operaciones por lotes muestran progreso por elemento y un resumen.

<details>
<summary>Captura de pantalla</summary>

![Controladores](../public/screenshots/drivers-systemdrivers.png)

</details>

### Copia de seguridad

El volumen de origen se elige en una lista; el formato (wim / esd / swm) determina la extensión y la compresión; el tamaño de división solo aplica a swm; antes de capturar se muestra la lista de exclusiones predeterminada. Al aplicar se puede verificar la imagen y reparar el arranque con `bcdboot`.

<details>
<summary>Captura de pantalla</summary>

![Copia de seguridad](../public/screenshots/backup-capture.png)

</details>

## Compilación

Requiere el SDK de .NET 10.

```powershell
# Compilación de depuración
dotnet build src/WinTools.App/WinTools.App.csproj

# Compilación Release
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# Genera ambos paquetes en artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

La arquitectura, el interior de los módulos, el formato de reglas, la localización y la depuración están en **[wtools.opkg.cn](https://wtools.opkg.cn/)** o en [docs/guide/development.md](../guide/development.md).

## Referencias

| Proyecto | Qué se tomó |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | Agrupación de ajustes, flujo de seleccionar y aplicar |
| [Optimizer](https://github.com/hellzerg/optimizer) | Categorías, redacción de riesgos, organización de inicio y servicios |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | Lista de ajustes y densidad de información para usuarios chinos |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | Uso de DISM y organización de recursos multilingües |
| [Geek Uninstaller](https://geekuninstaller.com/) | Campos de la lista de programas instalados |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | Modelo de datos de indicadores y semántica de tres estados |

Las reglas de limpieza partieron de una lista personal de rutas de caché y se han ido ampliando. Se mantienen en `tools/rules/*.txt` y se generan con `tools/Generate-CleanupRules.ps1`.

## Aviso legal

- La herramienta modifica el registro, los tipos de inicio de servicios, las funciones del sistema y los controladores. Crea un punto de restauración o respalda los datos antes de usarla.
- El autor no se responsabiliza de la pérdida de datos ni de daños al sistema derivados del uso.
- No debe usarse para eludir licencias, activación ni con fines contrarios a la legislación local.

## Licencia

[AGPL-3.0](../../LICENSE) © WinTools

### Componentes de terceros

| Componente | Licencia | Uso |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | Tipografía de la interfaz, incrustada como TTF |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | Generadores de código MVVM |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | Inyección de dependencias |
| [VitePress](https://vitepress.dev/) | MIT | Solo para el sitio de documentación |
