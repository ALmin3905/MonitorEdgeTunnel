#include <concepts>
#include "ThreadSafeObjectWrapper.h"
#include "TestSupport.h"

int main()
{
    return RunTests({
        { "copy from non-const wrapper", [] {
            ThreadSafeObjectWrapper<std::string> original("original");
            ThreadSafeObjectWrapper<std::string> copy(original);
            *copy.get() = "independent";
            Check(*original.get_readonly() == "original", "non-const source uses deep-copy constructor");
            Check(*copy.get_readonly() == "independent", "copied object remains writable");
        } },
    });
}
