#include "PrivateMessage.h"
#include "PrivateMessageCommands.h"
#include "PrivateMessageConfig.h"
#include "PrivateMessageHooks.h"
#pragma comment(lib, "ArkApi.lib")

void Init()
{
	Log::Get().Init("PrivateMessage");
	InitConfig();
	InitCommands();
	InitHooks();
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		Init();
		break;
	case DLL_PROCESS_DETACH:
		RemoveCommands();
		RemoveHooks();
		break;
	}
	return TRUE;
}