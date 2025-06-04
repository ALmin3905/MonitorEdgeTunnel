#include "pch.h"
#include "Utility.h"
#include <stringapiset.h>

namespace Utility
{
    std::string wchar_to_ansi(const std::wstring_view wstr)
    {
        if (wstr.empty())
            return {};

        int size_needed = WideCharToMultiByte(CP_ACP, 0, wstr.data(), -1, nullptr, 0, nullptr, nullptr);
        if (size_needed <= 0) return {};

        std::string result(size_needed - 1, 0);
        WideCharToMultiByte(CP_ACP, 0, wstr.data(), -1, &result[0], size_needed, nullptr, nullptr);
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
