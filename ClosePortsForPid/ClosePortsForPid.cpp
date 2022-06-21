// ClosePortsForPid.cpp
//
// Given a process ID, close its TCP ports.
//
// This acts as an instant logout for some games
//
// The SetTcpEntry call will only work if this program is run with admin rights.
//
// Usage:
//   ClosePortsForPid <processId>
//     - close TCP ports for the given process ID
//   ClosePortsForPid <dwLocalAddr> <dwLocalPort> <dwRemoteAddr> <dwRemotePort> [processId]
//     - close the specified TCP port
//     - if that fails and the process ID is given, close ports for that process
//     - this should be faster since it can skip the GetExtendedTcpTable calls
//   ClosePortsForPid <processId> print
//     - echos <dwLocalAddr> <dwLocalPort> <dwRemoteAddr> <dwRemotePort> for the above call

#include <winsock2.h>
#include <iphlpapi.h>
#include <stdio.h>

#pragma comment( lib, "Iphlpapi" )

int main(int argc, char* argv[])
{
    if (argc == 5 || argc == 6)
    {
        MIB_TCPROW toClose{
            .State = MIB_TCP_STATE_DELETE_TCB,
            .dwLocalAddr = strtoul(argv[1], nullptr, 0),
            .dwLocalPort = strtoul(argv[2], nullptr, 0),
            .dwRemoteAddr = strtoul(argv[3], nullptr, 0),
            .dwRemotePort = strtoul(argv[4], nullptr, 0),
        };
        auto result = SetTcpEntry(&toClose);
        if (result == 0)
            return 0;
        if (argc == 5)
        {
            printf("*** Error: SetTcpEntry on port %u returned %u\n", toClose.dwLocalPort, result);
            return 2;
        }
    }

    if (argc < 2)
    {
        printf("Usage:\n"
            "  ClosePortsForPid <processId>\n"
            "    - close TCP ports for the given process ID\n"
            "  ClosePortsForPid <dwLocalAddr> <dwLocalPort> <dwRemoteAddr> <dwRemotePort> [processId]\n"
            "    - close the specified TCP port\n"
            "    - if that fails and the process ID is given, close ports for that process\n"
            "    - this should be faster since it can skip the GetExtendedTcpTable calls\n"
            "    ClosePortsForPid <processId> print\n"
            "    - echos <dwLocalAddr> <dwLocalPort> <dwRemoteAddr> <dwRemotePort> for the above call\n");
        return 1;
    }

    auto processIdArg = argc == 6 ? 5 : 1;
    auto processId = atoi(argv[processIdArg]);

    DWORD dwSize = 0;
#pragma warning (disable: 28020)
    DWORD x = GetExtendedTcpTable(nullptr, &dwSize, FALSE, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
#pragma warning (default: 28020)

    MIB_TCPTABLE_OWNER_PID* table = (MIB_TCPTABLE_OWNER_PID*)malloc(dwSize);

    x = GetExtendedTcpTable(table, &dwSize, FALSE, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
#pragma warning (disable: 6011)
    PMIB_TCPROW_OWNER_PID row = table->table;
#pragma warning (default: 6011)
    auto numFound = 0;
    for (DWORD i = 0; i < table->dwNumEntries; ++i, ++row)
    {
        if (row->dwOwningPid != processId) continue;
        ++numFound;

        if (argc > 2 && argc < 6)
        {
            printf(" %u %u %u %u\n", row->dwLocalAddr, row->dwLocalPort, row->dwRemoteAddr, row->dwRemotePort);
            continue;
        }
        
        MIB_TCPROW toClose {
            .State = MIB_TCP_STATE_DELETE_TCB,
            .dwLocalAddr = row->dwLocalAddr,
            .dwLocalPort = row->dwLocalPort,
            .dwRemoteAddr = row->dwRemoteAddr,
            .dwRemotePort = row->dwRemotePort,
        };
        
        auto result = SetTcpEntry(&toClose);
        if (result != 0)
        {
            printf("*** Error: SetTcpEntry on port %u for pid %i returned %u\n", row->dwLocalPort, processId, result);
            return 2;
        }
    }

    free(table);

    if (numFound == 0)
    {
        printf("*** Error: Pid %i not found\n", processId);
        return 3;
    }
    return 0;
}
