#include <Windows.h>
#include <atomic>
#include <cstdio>
#include <stdexcept>
#include "../../MonitorEdgeTunnelLib/WindowMessageManager.cpp"

namespace
{
    // Signal delivery without broadcasting a real display change or moving the cursor.
    HANDLE delivered = nullptr;
    std::atomic_int callbacks = 0;

    void OnDisplayChanged()
    {
        ++callbacks;
        SetEvent(delivered);
    }
}

int main()
{
    delivered = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    WindowMessageManager manager;
    if (!delivered || !manager.DisplayChangedDelegate.Add(&OnDisplayChanged) || !manager.Start())
        throw std::runtime_error("Could not start the hidden window test");

    if (!manager.RePostDisplayChangeEvent() || WaitForSingleObject(delivered, 4000) != WAIT_OBJECT_0 || callbacks != 1)
        throw std::runtime_error("A repost must reach the hidden window and invoke its callback once");

    if (!manager.Stop() || manager.RePostDisplayChangeEvent())
        throw std::runtime_error("Stopped windows must reject display change reposts");

    CloseHandle(delivered);
    std::puts("Hidden window repost delivered exactly once");
}
