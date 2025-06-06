#pragma once

#include <Windows.h>
#include <string>
#include <string_view>

namespace Utility
{
    /// <summary>
    /// 將 UTF-8 字串轉換為 unicode 寬字元字串
    /// </summary>
    /// <param name="str">utf8字串</param>
    /// <returns>unicode字串</returns>
    std::wstring utf8_to_wchar(const std::string_view str);

    /// <summary>
    /// 將 unicode 字串轉換為 UTF-8 字串
    /// </summary>
    /// <param name="wstr">unicode字串</param>
    /// <returns>UTF-8字串</returns>
    std::string wchar_to_utf8(const std::wstring_view wstr);

    /// <summary>
    /// 輸出RECT字串
    /// </summary>
    std::string to_string(const RECT& rc);
}
