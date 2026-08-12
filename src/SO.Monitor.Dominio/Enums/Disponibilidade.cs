namespace SO.Monitor.Dominio.Enums;

/// <summary>
/// O resultado de tentar ler um dado protegido do sistema operacional (ex.: campos de um PCB
/// de outro processo). Memória e informações de processo são protegidas pelo próprio SO — nem
/// toda leitura tem sucesso, e essa proteção é conteúdo didático, não um detalhe a esconder.
/// </summary>
public enum Disponibilidade
{
    /// <summary>
    /// Estado de uma <see cref="Leitura{T}"/> construída por <c>default</c> — não representa uma
    /// leitura de fato realizada (nem sucesso nem falha), só o valor que <c>default(Leitura&lt;T&gt;)</c>
    /// carrega antes de qualquer fábrica (<see cref="Leitura{T}.Ok"/>, <see cref="Leitura{T}.Negada"/>...)
    /// ser chamada. É deliberadamente o primeiro valor (0) do enum: antes, <see cref="Disponivel"/>
    /// ocupava o valor 0, o que fazia <c>default(Leitura&lt;T&gt;).TemValor</c> mentir como
    /// <c>true</c> — um <c>default</c> nunca deve ser confundido com uma leitura bem-sucedida.
    /// </summary>
    Indefinida,

    /// <summary>O valor foi lido com sucesso.</summary>
    Disponivel,

    /// <summary>O sistema operacional negou acesso ao dado (proteção entre processos).</summary>
    AcessoNegado,

    /// <summary>Esta plataforma não expõe essa métrica de forma alguma.</summary>
    NaoSuportadoNaPlataforma,

    /// <summary>
    /// O dado não faz sentido no estado atual — diferente de <see cref="NaoSuportadoNaPlataforma"/>,
    /// que é uma limitação real da plataforma. Exemplo: o motivo de bloqueio de uma thread que não
    /// está bloqueada — nenhuma plataforma "suportaria" essa leitura, porque a pergunta em si não
    /// se aplica.
    /// </summary>
    NaoSeAplica,

    /// <summary>O processo terminou antes (ou durante) a leitura do dado.</summary>
    ProcessoEncerrado
}
