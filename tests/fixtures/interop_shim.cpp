// Leave nonzero upper return-register bits to distinguish C++ bool from Win32 BOOL.
static volatile unsigned int marker = 0x01010101;
__declspec(noinline) unsigned int DirtyRegister() { return marker; }
extern "C" __declspec(dllexport) bool __stdcall SaveSetting() { DirtyRegister(); return false; }
extern "C" __declspec(dllexport) bool __stdcall AddDisplayChangedCallback(void* callback) { DirtyRegister(); return false; }
extern "C" __declspec(dllexport) bool __stdcall RemoveDisplayChangedCallback(void* callback) { DirtyRegister(); return false; }
