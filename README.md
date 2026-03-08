# Process Monitor GUI 🚀 (Avalonia .NET)

Um monitor de processos C# nativo e multiplataforma com base em **Avalonia UI** e arquitetado com princípios diretos de **Clean Architecture (MVVM)**, **SOLID** e separação de lógica focada em desempenho em tempo real.

![Screenshot de Exemplo do Monitor (Recomendado adicionar depois)](https://via.placeholder.com/800x400.png?text=Monitor+de+Processos+(Avalonia))

## Recursos Principais 🔥

Este repositório foca em trazer a rastreabilidade interna do `System.Diagnostics` (antes disponível apenas em modo Console) para uma interface rica com **Dark Mode**:

* **Lista Mestre-Detalhe Independente:** 
  * Os processos são lidos no topo. Ao clicar, suas respectivas *Threads* são esmiuçadas no painel inferior.
* **Mapa de Memória Simulado:** 
  * Um widget visual renderizado em tempo real simulando a alocação de páginas (*Code Segments*, *Resident RAM*, *Paged Out*) baseado na heurística do Processo alvo.
* **Cálculos Reativos:**  
  * Filtros por estado **CPU-Bound** (Maior uso de cálculos em processador) vs **I/O-Bound** (Alto enfileiramento de disco/rede - Handles).
  * Informações de Endereços base de Memória (Virtual Fim e Virtual Início) exibidas em Hexadecimal Limpo (`0x...`).
  * Atualização dinâmica sem travamento da UI via `Dispatcher.UIThread` e `CancellationToken` (Polling customizável entre 500ms e 10s).
* **Tratamento Específico para macOS/Unix:**
  * Lógica resiliente para _WaitReasons_ blindada com as respostas limitantes nativas do Kernel _Darwin_ ao tentar ler perfis protegidos em modo Usuário.

## Tecnologias e Padrões 📚

* **C# 12 / .NET 8.0**
* **Avalonia UI (v11+)** para o renderizador gráfico (Suporte nativo pra macOS, Windows e Linux).
* **CommunityToolkit.Mvvm** para binds reativos eficientes nas *ViewModels*.
* **Clean Architecture** (Simplificada para camada de visão por hora, organizada solidamente em `Models`, `Services`, `ViewModels` e `Views`).

## Configurando e Executando Localmente 🛠

Como utilizamos Avalonia UI, a execução é indolor em qualquer SO.

1. Instale o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Clone o repositório:
   ```bash
   git clone https://github.com/profraooliveira/ProcessMonitorGUI.git
   cd ProcessMonitorGUI
   ```
3. Restaure as dependências e rode o projeto:
   ```bash
   dotnet run
   ```

*NOTA PARA USUÁRIOS UNIX (macOS/Linux):* Lembre-se que alguns processos de Sistema (Kernel Task, etc.) possuem bloqueio de _SIP/Ring 0_, então suas informações de `Private Memory` e _Threads Wait Reason_ retornarão mensagens tratadas de "Sem Permissão" a não ser que você execute o processo como _sudo_ e eleve a _App_. 

## Autor
Mantido e desenvolvido por Prof. Raoni Oliveira ([@profraonioliveira](https://github.com/profraooliveira)).
