#pragma once

#include <vector>
#include <string>

/// <summary>
/// base64編碼
/// </summary>
/// <param name="buf">二進制資料</param>
/// <param name="bufLen">資料長度</param>
/// <returns>編碼字串</returns>
std::string base64_encode(const unsigned char* buf, unsigned int bufLen);

/// <summary>
/// base64解碼
/// </summary>
/// <param name="encoded_string">編碼字串</param>
/// <returns>解碼二進制資料</returns>
std::vector<unsigned char> base64_decode(const std::string& encoded_string);