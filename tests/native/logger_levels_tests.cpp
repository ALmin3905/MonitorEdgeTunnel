#include "LoggerCapture.h"
#include "TestSupport.h"

int main()
{
    return RunTests({
        { "all declared console severity names", [] {
            LogCapture capture;
            for (int level = 0; level <= 4; ++level) Logger::Log(level, "text");
            Check(capture.Text() == "Log [Debug]: text\nLog [Info]: text\nLog [Warning]: text\n"
                "Log [Error]: text\nLog [Fatal]: text\n", "each severity must keep its own name");
        } },
    });
}
