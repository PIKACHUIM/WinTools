<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.de.md">Deutsch</a><br/>
  <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <b>Português</b> · <a href="README.it.md">Italiano</a> · <a href="README.ru.md">Русский</a> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="Versão"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="Plataforma"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="Licença"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="Documentação"></a>
</p>

## Apresentação

O WinTools reúne a manutenção comum do sistema em uma única janela: limpar caches e restos de atualização, desligar telemetria desnecessária, remover uma atualização problemática, ver o que inicia junto com o sistema, instalar runtimes que faltam, exportar drivers antigos, criar uma imagem antes de reinstalar.

Aplicativo WPF, totalmente local. Sem serviço, sem driver, sem inicialização automática, sem acesso à rede. Fechar a janela encerra o processo.

Dez módulos: **Limpeza, Otimização, Recursos, Atualizações, Inicialização, Componentes, Runtimes, Aplicativos, Drivers, Backup.**

## Requisitos

- Windows 10 1809 (17763) ou superior, ou Windows 11, x64
- A versão dependente do framework precisa do [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Permissões de administrador (declaradas no manifesto); DISM, drivers, serviços e imagens dependem delas
- Alguns recursos chamam componentes do Windows: DISM, Windows Update Agent, `pnputil`, WIMGAPI. Podem faltar em imagens reduzidas

## Download

| Versão | Tamanho | Observação | Link |
| --- | --- | --- | --- |
| Portátil | ~70 MB | Runtime incluído, descompacte e execute | [WinTools-*-portable-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Dependente do framework | ~22 MB | Requer .NET 10 Desktop Runtime | [WinTools-*-framework-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | idem | Recompilada a cada push em `main` | [releases/tag/beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

As versões oficiais têm assinatura em nuvem Certum com carimbo de tempo, verificável por `Get-AuthenticodeSignature`. Cada versão inclui `SHA256SUMS.txt`.

## Recursos

### Visão geral

Versão e número de build do sistema, uso de CPU e memória, espaço livre por disco, atividade recente. Todos os dados são lidos localmente.

<details>
<summary>Captura de tela</summary>

![Visão geral](../public/screenshots/dashboard.png)

</details>

### Limpeza

Analisa discos inteiros e cobre pastas temporárias do sistema, cache de atualizações, logs e despejos, caches de navegadores, caches de ferramentas de desenvolvimento (npm, pip, NuGet, Gradle, Docker) e as pastas de cache de mais de 150 aplicativos.

Os resultados são agrupados por categoria com tamanho e nível de segurança. Arquivos em uso são ignorados e registrados em vez de travar a análise. As regras são JSON em `src/WinTools.Infrastructure/Rules/`; adicionar um caminho não exige alterar código.

<details>
<summary>Captura de tela</summary>

![Limpeza](../public/screenshots/cleanup.png)

</details>

### Otimização

Privacidade e telemetria, desempenho, Explorador de Arquivos, comportamento do sistema, rede, atualizações, jogos.

Cada item permite detectar, aplicar e reverter, então o estado atual aparece antes de qualquer mudança. Os itens recomendados são limitados a efeitos bem conhecidos.

<details>
<summary>Captura de tela</summary>

![Otimização](../public/screenshots/optimize.png)

</details>

### Recursos

- **Recursos do sistema**: lista todos os recursos opcionais pela API DISM, agrupados por finalidade, com descrição e estado atual; as mudanças são aplicadas em lote.
- **Canal de edição**: lê o SKU, a versão e o estado de ativação. A troca usa o `changepk` com chave genérica e não ativa o Windows.

<details>
<summary>Captura de tela</summary>

![Recursos do sistema](../public/screenshots/features-systemfeatures.png)

</details>

### Atualizações

- **Instaladas**: enumerar e desinstalar individualmente.
- **Disponíveis**: disparar manualmente a busca do Windows Update com categoria, KB, tamanho e data, instalação em lote e resumo.
- **Sinalizadores de recurso**: gerenciar `FeatureManagement\Overrides` por ID com estado, prioridade e variante, além de importar, exportar e redefinir.

<details>
<summary>Captura de tela</summary>

![Atualizações disponíveis](../public/screenshots/updates-available.png)

</details>

### Inicialização

Tudo em uma página. Ícones reais dos programas, interruptores para o estado. Serviços mudam o tipo de inicialização na própria linha (Automático, Automático atrasado, Manual, Desativado).

<details>
<summary>Captura de tela</summary>

![Inicialização](../public/screenshots/startup-services.png)

</details>

### Componentes

- **Central de segurança**: 14 ajustes cobrindo proteção em tempo real, proteção na nuvem, monitoramento de comportamento, PUA, proteção de rede, acesso controlado a pastas, integridade de memória, UAC, SmartScreen, os três perfis de firewall e exclusões.
- **Componentes do sistema**: aplicativos integrados (Terminal, Calculadora, Bloco de Notas, Paint, Fotos e outros) para instalar, reparar ou desinstalar; distribuições WSL; subsistema Android a partir de pacote local; reparo da Store.

<details>
<summary>Captura de tela</summary>

![Central de segurança](../public/screenshots/components-securitycenter.png)

</details>

### Runtimes

Lista os runtimes .NET e Visual C++ instalados. As versões disponíveis são obtidas sob demanda: .NET do `releases-index.json` oficial com verificação SHA512, VC++ por links `aka.ms` com verificação Authenticode. Ambos instalam em silêncio e podem ser desinstalados.

<details>
<summary>Captura de tela</summary>

![Runtimes](../public/screenshots/runtimes-dotnet.png)

</details>

### Aplicativos

Mostra ícone, versão, tamanho, data de instalação e última atividade. A desinstalação chama o desinstalador do próprio programa; pacotes usam a API de implantação.

<details>
<summary>Captura de tela</summary>

![Aplicativos](../public/screenshots/apps-programs.png)

</details>

### Drivers

Gerencia todo o DriverStore. Selecione qualquer quantidade de pacotes para fazer backup, remover ou instalar a partir de uma pasta. As operações em lote mostram progresso por item e um resumo.

<details>
<summary>Captura de tela</summary>

![Drivers](../public/screenshots/drivers-systemdrivers.png)

</details>

### Backup

O volume de origem é escolhido numa lista; o formato (wim / esd / swm) define a extensão e a compactação; o tamanho de divisão vale apenas para swm; antes da captura é exibida a lista de exclusões padrão. Na aplicação é possível verificar a imagem e reparar a inicialização com `bcdboot`.

<details>
<summary>Captura de tela</summary>

![Backup](../public/screenshots/backup-capture.png)

</details>

## Compilação

Requer o SDK do .NET 10.

```powershell
# Build de depuração
dotnet build src/WinTools.App/WinTools.App.csproj

# Build Release
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# Gera os dois pacotes em artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

Arquitetura, detalhes dos módulos, formato das regras, localização e depuração estão em **[wtools.opkg.cn](https://wtools.opkg.cn/)** ou em [docs/guide/development.md](../guide/development.md).

## Referências

| Projeto | O que foi aproveitado |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | Agrupamento de ajustes, fluxo de selecionar e aplicar |
| [Optimizer](https://github.com/hellzerg/optimizer) | Categorias, texto de risco, organização de inicialização e serviços |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | Lista de ajustes e densidade de informação para usuários chineses |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | Uso do DISM e organização de recursos multilíngues |
| [Geek Uninstaller](https://geekuninstaller.com/) | Campos da lista de programas instalados |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | Modelo de dados dos sinalizadores e semântica de três estados |

As regras de limpeza começaram como uma lista pessoal de caminhos de cache e foram ampliadas com o tempo. Hoje são mantidas em `tools/rules/*.txt` e geradas por `tools/Generate-CleanupRules.ps1`.

## Aviso

- A ferramenta altera o registro, tipos de inicialização de serviços, recursos do sistema e drivers. Crie um ponto de restauração ou faça backup antes de usar.
- O autor não se responsabiliza por perda de dados ou danos ao sistema decorrentes do uso.
- Não use para contornar licenciamento, ativação ou qualquer finalidade contrária à legislação local.

## Licença

[AGPL-3.0](../../LICENSE) © WinTools

### Componentes de terceiros

| Componente | Licença | Uso |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | Fonte da interface, embutida como TTF |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | Geradores de código MVVM |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | Injeção de dependência |
| [VitePress](https://vitepress.dev/) | MIT | Apenas para o site de documentação |
