using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SO.Monitor.Infraestrutura.Windows;

/// <summary>
/// P/Invoke mínimos sobre a Win32 API para inspecionar o espaço de endereçamento virtual de
/// outro processo: <c>VirtualQueryEx</c> (o mesmo mecanismo por trás de ferramentas como VMMap)
/// para enumerar regiões, e <c>QueryWorkingSetEx</c> (o mesmo que sustenta a coluna "Working Set"
/// do Gerenciador de Tarefas) para saber quais páginas estão de fato residentes na RAM. Todos os
/// campos são tipos nativos sem ponteiro (<c>nuint</c>/<c>IntPtr</c>), então nada aqui exige
/// <c>unsafe</c> nem <c>AllowUnsafeBlocks</c> no projeto.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NativoWin32
{
    // ACCESS_MASK para OpenProcess: o mínimo necessário para ler memória de outro processo, sem
    // poder escrevê-la ou terminá-lo.
    internal const uint PROCESS_QUERY_INFORMATION = 0x0400;
    internal const uint PROCESS_VM_READ = 0x0010;

    /// <summary>Bit 0 de <see cref="PSAPI_WORKING_SET_EX_INFORMATION.VirtualAttributes"/>: 1 quando a página está residente na RAM.</summary>
    internal const nuint WorkingSetExValidBit = 0x1;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nuint VirtualQueryEx(
        IntPtr hProcess,
        nuint lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer,
        nuint dwLength);

    [DllImport("kernel32.dll")]
    internal static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryWorkingSetEx(
        IntPtr hProcess,
        [In, Out] PSAPI_WORKING_SET_EX_INFORMATION[] pv,
        uint cb);

    /// <summary>Espelha <c>MEMORY_BASIC_INFORMATION</c> (winnt.h): descreve uma região de memória de um processo.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORY_BASIC_INFORMATION
    {
        internal nuint BaseAddress;
        internal nuint AllocationBase;
        internal uint AllocationProtect;
        internal nuint RegionSize;
        internal uint State;
        internal uint Protect;
        internal uint Type;
    }

    /// <summary>Espelha <c>SYSTEM_INFO</c> (sysinfoapi.h): usamos só os limites do espaço de endereçamento de usuário.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_INFO
    {
        internal ushort ProcessorArchitecture;
        internal ushort Reserved;
        internal uint PageSize;
        internal nuint MinimumApplicationAddress;
        internal nuint MaximumApplicationAddress;
        internal nuint ActiveProcessorMask;
        internal uint NumberOfProcessors;
        internal uint ProcessorType;
        internal uint AllocationGranularity;
        internal ushort ProcessorLevel;
        internal ushort ProcessorRevision;
    }

    /// <summary>Espelha <c>PSAPI_WORKING_SET_EX_INFORMATION</c> (psapi.h): uma página consultada e seus atributos.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PSAPI_WORKING_SET_EX_INFORMATION
    {
        /// <summary>Entrada: o endereço da página a consultar.</summary>
        internal nuint VirtualAddress;

        /// <summary>Saída: bitfield de atributos — bit 0 (<see cref="WorkingSetExValidBit"/>) indica página residente.</summary>
        internal nuint VirtualAttributes;
    }
}
