#include <barrier>
#include <concepts>
#include "ThreadSafeObjectWrapper.h"
#include "TestSupport.h"

int main()
{
    return RunTests({
        { "opposite assignments do not deadlock", [] {
            ThreadSafeObjectWrapper<int> left(1), right(2);
            std::barrier rendezvous(2);
            auto assign = [&](auto& destination, auto& source) {
                for (int i = 0; i < 5000; ++i)
                {
                    rendezvous.arrive_and_wait();
                    destination = source;
                }
            };
            std::jthread first([&] { assign(left, right); });
            std::jthread second([&] { assign(right, left); });
            first.join();
            second.join();
            Check(*left.get_readonly() == *right.get_readonly(), "assignments complete with a coherent value");
        } },
    });
}
