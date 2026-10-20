#include "WindowFixture.h"

int main()
{
    return RunTests({
        { "failed window creation releases its registered class", [] {
            WindowMessageManager manager;
            const int registered = WindowFixture::registrations;
            const int removed = WindowFixture::unregistrations;
            WindowFixture::failCreate = true;
            const bool started = manager.Start();
            WindowFixture::failCreate = false;
            Check(!started && !manager.IsRunning(), "window creation failure reported");
            Check(WindowFixture::registrations == registered + 1 && WindowFixture::unregistrations == removed + 1,
                "registered class must be released even when no HWND was created");
            Check(manager.Start() && manager.Stop(), "restart after window creation failure");
        } },
    });
}
