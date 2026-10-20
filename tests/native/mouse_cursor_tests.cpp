#include "MouseFixture.h"

int main()
{
    return RunTests({
        { "failed cursor lookup leaves manager uninitialized", [] {
            MouseEdgeManager manager;
            MouseFixture::cursorAvailable = false;
            CheckThrows<std::runtime_error>([&] { manager.UpdateMonitorInfo({ MouseFixture::Monitor(0) }); },
                "failed Win32 lookup must not initialize from undefined coordinates");
            Check(!manager.IsInit(), "failed cursor read clears initialization");
            MouseFixture::cursorAvailable = true;
            manager.UpdateMonitorInfo({ MouseFixture::Monitor(0) });
            Check(manager.IsInit(), "cursor lookup recovery");
        } },
    });
}
