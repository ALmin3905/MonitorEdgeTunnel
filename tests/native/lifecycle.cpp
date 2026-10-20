#include <Windows.h>
#include <atomic>
#include <cstdio>
#include <stdexcept>
#include <thread>

namespace
{
    // Shorten only the production stop timeout; callback synchronization uses real events.
    DWORD TestWait(HANDLE handle, DWORD timeout) { return WaitForSingleObject(handle, timeout == 5000 ? 40 : timeout); }
    HANDLE entered = nullptr;
    HANDLE releaseCallback = nullptr;
    void BlockCallback() { SetEvent(entered); WaitForSingleObject(releaseCallback, INFINITE); }
    void Require(bool condition) { if (!condition) throw std::runtime_error("Window lifetime regression failed"); }
}

#define WaitForSingleObject TestWait
#include "../../MonitorEdgeTunnelLib/WindowMessageManager.cpp"
#undef WaitForSingleObject

int main()
{
    entered = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    releaseCallback = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    auto manager = new WindowMessageManager;
    manager->DisplayChangedDelegate.Add(&BlockCallback);
    Require(manager->Start() && manager->RePostDisplayChangeEvent());
    Require(WaitForSingleObject(entered, 4000) == WAIT_OBJECT_0);
    Require(!manager->Stop() && !manager->Start());

    std::atomic_bool destroyed = false;
    std::thread destroyer([&] { delete manager; destroyed = true; });
    Sleep(100);
    Require(!destroyed);
    SetEvent(releaseCallback);
    destroyer.join();
    Require(destroyed);

    WindowMessageManager restart;
    for (int index = 0; index < 3; ++index)
        Require(restart.Start() && restart.Stop());
    CloseHandle(entered);
    CloseHandle(releaseCallback);
    std::puts("Window stop timeout retains ownership; destruction waits and restart remains safe");
}
