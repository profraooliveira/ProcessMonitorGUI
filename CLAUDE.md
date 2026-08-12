# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build ProcessMonitorGUI.sln            # Build (0 warnings é o esperado — mantenha assim)
dotnet test ProcessMonitorGUI.sln             # Suíte xUnit completa
dotnet run --project MonitorGUI/MonitorGUI.csproj   # Roda o app Avalonia
```

## Tech Stack

- .NET 8.0, C# `LangVersion latest` (propriedades comuns em `Directory.Build.props`)
- Avalonia UI 11.3.x com compiled bindings (`AvaloniaUseCompiledBindingsByDefault=true`)
- CommunityToolkit.Mvvm 8.4.x (`[ObservableProperty]`, `[RelayCommand]`)
- Microsoft.Extensions.DependencyInjection (composition root em `MonitorGUI/App.axaml.cs`)
- xUnit em `tests/SO.Monitor.Testes`

## Arquitetura (DDD em camadas — fronteiras impostas pelo compilador)

```
src/SO.Monitor.Dominio          → NENHUMA referência (regra de ouro: mantenha assim)
src/SO.Monitor.Aplicacao        → Dominio          (ports + casos de uso)
src/SO.Monitor.Infraestrutura   → Aplicacao, Dominio (adapters por plataforma)
MonitorGUI (Apresentação)       → Aplicacao, Dominio + Infra (só como composition root)
tests/SO.Monitor.Testes         → todas
```

- **Dominio**: value objects (`Pid`, `TamanhoBytes`, `TamanhoPagina`, `EnderecoVirtual`, `FaixaDeEnderecos`, `Percentual`), enums canônicos de Tanenbaum (`EstadoProcesso`, `EstadoThread`, `MotivoDeBloqueio`, `PerfilDeExecucao`), `Leitura<T>`/`Disponibilidade` (leitura falível — distingue "0" de "acesso negado"), agregado `Processo` (raiz; `ThreadDoProcesso` interna), `MapaDeMemoria`/`RegiaoDeMemoria` com `OrigemDosDados` (MedidaReal|Simulada), políticas de classificação (Strategy: `PoliticaPorComportamentoDeBurst`, `PoliticaPorContagemDeHandles`, `PoliticaComposta`), `SimuladorDePaginacao`, `CalculadoraDeUsoDeCpu` (delta entre amostras).
- **Aplicacao**: ports `IFonteDeProcessos`, `IFonteDeMapaDeMemoria`, `IFonteDeDetalhesDeThreads`; casos de uso `ObterAmostraClassificada`, `ObterMapaDeMemoria` (fallback simulado ROTULADO com motivo), `MonitorDeProcessos` (`PeriodicTimer` + `IAsyncEnumerable`).
- **Infraestrutura**: `FonteDeProcessosDotNet` (baseline portátil, %CPU por delta com cache por PID), `FabricaDeFontes` (Factory Method, guardas `OperatingSystem.Is*` — padrão que o CA1416 reconhece), e por plataforma: `MacOs/` (parsing de `vmmap -interleaved` e `ps -M` via `ExecutorDeComandoExterno`), `Linux/` (`/proc/<pid>/smaps`, `/proc/<pid>/task/*/stat`), `Windows/` (P/Invoke `VirtualQueryEx` + `QueryWorkingSetEx`).
- **Apresentação**: DI por construtor; `DesignTimeMainWindowViewModel` alimenta o previewer (`Design.DataContext`); `ReconciliadorDeProcessos` atualiza a coleção por PID (nunca `Clear()` — preserva seleção/scroll); mapa de memória carregado por seleção + botão "Atualizar mapa" (nunca por tick); converters fazem a formatação (colunas fazem binding no valor numérico).

## Regras do projeto

- **Proibido `catch { }` vazio**: toda falha esperada vira `Leitura<T>` com a `Disponibilidade` correta; "acesso negado pelo kernel" é conteúdo didático exibível, nunca ruído engolido.
- **Honestidade didática**: dado simulado é SEMPRE rotulado como simulado (com motivo do fallback); dado que a plataforma não expõe vira `Indeterminado`/`NaoObservavelNestaPlataforma`, nunca chute.
- Parsers de infraestrutura são **funções puras** testadas com fixtures reais embutidas nos testes — mudanças neles exigem atualizar os testes de parsing.
- Build precisa continuar com **0 warnings**; suíte inteira verde antes de qualquer entrega.
- Testes ao vivo específicos de plataforma começam com `if (!OperatingSystem.IsMacOS()) return;` (idem Linux) para não quebrar em outros SOs.

## Idioma

Todo texto de UI, nomes, comentários e XML docs em **português brasileiro**. XML docs são didáticas: explicam o conceito de SO (Tanenbaum) que o tipo representa, em 1-3 linhas.
