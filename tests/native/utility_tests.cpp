#include <Windows.h>
#include "Utility.h"
#include "TestSupport.h"

int main()
{
    return RunTests({
        { "empty UTF conversions", [] {
            Check(Utility::utf8_to_wchar({}).empty(), "empty UTF-8 input");
            Check(Utility::wchar_to_utf8({}).empty(), "empty UTF-16 input");
        } },
        { "ASCII and Unicode round trips", [] {
            const std::string utf8 = "Monitor 中文\xF0\x9F\x98\x80";
            const std::wstring wide = L"Monitor 中文\xD83D\xDE00";
            Check(Utility::utf8_to_wchar(utf8) == wide, "UTF-8 to UTF-16, including surrogate pair");
            Check(Utility::wchar_to_utf8(wide) == utf8, "UTF-16 to UTF-8, including non-ASCII");
        } },
        { "bounded UTF-8 view", [] {
            const std::string backing = "prefix中文suffix";
            Check(Utility::utf8_to_wchar(std::string_view(backing).substr(6, 6)) == L"中文",
                "conversion must not read past the view");
            const char exact[] = { 'a', 'b', 'c' };
            Check(Utility::utf8_to_wchar(std::string_view(exact, 3)) == L"abc", "no terminator required");
        } },
        { "bounded UTF-16 view", [] {
            const std::wstring backing = L"prefix中文suffix";
            Check(Utility::wchar_to_utf8(std::wstring_view(backing).substr(6, 2)) == "中文",
                "conversion must respect UTF-16 view length");
            const wchar_t exact[] = { L'a', L'b', L'c' };
            Check(Utility::wchar_to_utf8(std::wstring_view(exact, 3)) == "abc", "no wide terminator required");
        } },
        { "embedded null characters", [] {
            const std::string utf8("a\0b", 3);
            const std::wstring wide(L"a\0b", 3);
            Check(Utility::utf8_to_wchar(utf8) == wide, "embedded UTF-8 null must survive");
            Check(Utility::wchar_to_utf8(wide) == utf8, "embedded UTF-16 null must survive");
        } },
        { "rectangle diagnostic", [] {
            const RECT rect{ -1920, -1080, 1919, 1079 };
            Check(Utility::to_string(rect) == "RECT(-1920,-1080,1919,1079)", "coordinate order and signed values");
            Check(Utility::to_string(RECT{}) == "RECT(0,0,0,0)", "zero rectangle");
        } },
    });
}
