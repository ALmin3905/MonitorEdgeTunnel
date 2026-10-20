#include "base64.h"
#include "../../MonitorEdgeTunnelLib/base64.cpp"
#include "TestSupport.h"
#include <array>
#include <numeric>

int main()
{
    return RunTests({
        { "standard encoding vectors and padding", [] {
            const std::array<std::pair<std::string, std::string>, 7> vectors{{
                { "", "" }, { "f", "Zg==" }, { "fo", "Zm8=" }, { "foo", "Zm9v" },
                { "foob", "Zm9vYg==" }, { "fooba", "Zm9vYmE=" }, { "foobar", "Zm9vYmFy" },
            }};
            for (const auto& [plain, encoded] : vectors)
            {
                Check(base64_encode(reinterpret_cast<const unsigned char*>(plain.data()),
                    static_cast<unsigned int>(plain.size())) == encoded, "known encoding vector");
                const auto decoded = base64_decode(encoded);
                Check(std::string(decoded.begin(), decoded.end()) == plain, "known decoding vector");
            }
        } },
        { "all byte values and partial final blocks", [] {
            std::array<unsigned char, 256> binary{};
            std::iota(binary.begin(), binary.end(), static_cast<unsigned char>(0));
            for (unsigned int length = 0; length <= binary.size(); ++length)
            {
                const auto encoded = base64_encode(binary.data(), length);
                Check(encoded.size() == 4 * ((length + 2) / 3), "encoded block length");
                const auto decoded = base64_decode(encoded);
                Check(decoded == std::vector<unsigned char>(binary.begin(), binary.begin() + length),
                    "binary round trip must preserve nulls and high bytes");
            }
        } },
        { "unpadded decode and legacy stop at terminator", [] {
            Check(base64_decode("Zg") == std::vector<unsigned char>{ 'f' }, "one-byte unpadded input");
            Check(base64_decode("Zm8") == std::vector<unsigned char>{ 'f', 'o' }, "two-byte unpadded input");
            Check(base64_decode("Zm9v!ignored") == std::vector<unsigned char>{ 'f', 'o', 'o' },
                "decoder stops at first non-alphabet character");
            Check(base64_decode("=ignored").empty(), "leading padding yields no bytes");
        } },
    });
}
