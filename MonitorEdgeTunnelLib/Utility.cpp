#include "pch.h"
#include "Utility.h"
#include <stringapiset.h>
#include <limits>

namespace Utility
{
    std::wstring utf8_to_wchar(const std::string_view str)
    {
        if (str.empty() || str.size() > (std::numeric_limits<int>::max)())
            return {};

        const int length = static_cast<int>(str.size());
        int size_needed = MultiByteToWideChar(CP_UTF8, 0, str.data(), length, nullptr, 0);
        if (size_needed <= 0)
            return {};

        std::wstring result(size_needed, 0);
        if (!MultiByteToWideChar(CP_UTF8, 0, str.data(), length, result.data(), size_needed))
            return {};
        return result;
    }

    std::string wchar_to_utf8(const std::wstring_view wstr)
    {
        if (wstr.empty() || wstr.size() > (std::numeric_limits<int>::max)())
            return {};

        const int length = static_cast<int>(wstr.size());
        int size_needed = WideCharToMultiByte(CP_UTF8, 0, wstr.data(), length, nullptr, 0, nullptr, nullptr);
        if (size_needed <= 0)
            return {};

        std::string result(size_needed, 0);
        if (!WideCharToMultiByte(CP_UTF8, 0, wstr.data(), length, result.data(), size_needed, nullptr, nullptr))
            return {};
        return result;
    }

    std::string to_string(const RECT& rc)
    {
        return std::string("RECT(") +
            std::to_string(rc.left) + "," +
            std::to_string(rc.top) + "," +
            std::to_string(rc.right) + "," +
            std::to_string(rc.bottom) +
            ")";
    }
}
