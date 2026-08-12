using SO.Monitor.Dominio.Enums;

namespace SO.Monitor.Dominio;

/// <summary>
/// Uma leitura de um dado do sistema operacional que pode legitimamente falhar: nem todo campo
/// de um PCB (Bloco de Controle de Processo) é acessível ao monitor — proteção de memória entre
/// processos, permissões do SO, ou a métrica simplesmente não existe nesta plataforma (ex.:
/// contagem de handles no macOS). É o antídoto do <c>catch</c> vazio: distingue "o valor é zero"
/// de "o kernel negou o acesso ao PCB" — proteção de memória como conteúdo didático, não ruído.
/// </summary>
public readonly record struct Leitura<T>(T? Valor, Disponibilidade Estado)
{
    /// <summary>Cria uma leitura bem-sucedida com o valor informado.</summary>
    public static Leitura<T> Ok(T valor) => new(valor, Disponibilidade.Disponivel);

    /// <summary>Cria uma leitura que falhou porque o sistema operacional negou o acesso.</summary>
    public static Leitura<T> Negada() => new(default, Disponibilidade.AcessoNegado);

    /// <summary>Cria uma leitura que falhou porque esta plataforma não expõe o dado.</summary>
    public static Leitura<T> NaoSuportada() => new(default, Disponibilidade.NaoSuportadoNaPlataforma);

    /// <summary>
    /// Cria uma leitura que não falhou por limitação de plataforma ou permissão, mas cujo dado
    /// simplesmente não se aplica no estado atual (ex.: motivo de bloqueio de uma thread que não
    /// está bloqueada) — ver <see cref="Disponibilidade.NaoSeAplica"/> para a distinção de
    /// <see cref="NaoSuportada"/>.
    /// </summary>
    public static Leitura<T> NaoSeAplica() => new(default, Disponibilidade.NaoSeAplica);

    /// <summary>Cria uma leitura que falhou porque o processo já havia terminado.</summary>
    public static Leitura<T> ProcessoEncerrado() => new(default, Disponibilidade.ProcessoEncerrado);

    /// <summary>
    /// Indica se o valor foi efetivamente lido (<see cref="Estado"/> é Disponivel). Como
    /// <see cref="Disponibilidade.Indefinida"/> é o valor 0 do enum, <c>default(Leitura&lt;T&gt;)</c>
    /// cai fora desta condição — nunca reporta um valor que não existe.
    /// </summary>
    public bool TemValor => Estado == Disponibilidade.Disponivel;

    /// <summary>Retorna o valor lido, ou <paramref name="padrao"/> quando a leitura falhou.</summary>
    public T ValorOu(T padrao) => TemValor ? Valor! : padrao;
}
