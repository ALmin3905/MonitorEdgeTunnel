#include <atomic>
#include <string>
#include <type_traits>
#include <vector>
#include "ThreadSafeObjectWrapper.h"
#include "WaitEvent.h"
#include "TestSupport.h"

using Wrapper = ThreadSafeObjectWrapper<std::string>;
static_assert(!std::is_copy_constructible_v<Wrapper::get_type>);
static_assert(!std::is_move_constructible_v<Wrapper::get_type>);
static_assert(std::is_same_v<decltype(*std::declval<Wrapper>().get_readonly()), const std::string&>);

int main()
{
    return RunTests({
        { "wrapper construction and access operators", [] {
            Wrapper value(3, 'x');
            Check(value.get()->size() == 3, "forward constructor arguments");
            *value.get() = "changed";
            Check(value.get_readonly().get() == "changed", "write then read");
        } },
        { "wrapper deep copy and assignment", [] {
            const Wrapper source("original");
            Wrapper copy(source);
            *copy.get() = "copy";
            Wrapper assigned;
            assigned = source;
            assigned = assigned;
            Check(*source.get_readonly() == "original", "copy must not share the wrapped object");
            Check(*copy.get_readonly() == "copy", "copy independence");
            Check(*assigned.get_readonly() == "original", "assignment and self-assignment");
        } },
        { "accessor retains object after wrapper destruction", [] {
            auto held = [] { Wrapper local("alive"); return local.get_readonly(); }();
            Check(*held == "alive", "accessor owns the object and mutex");
        } },
        { "shared readers and exclusive writer", [] {
            Wrapper value("initial");
            std::atomic_bool readAcquired = false;
            std::atomic_bool writeEntered = false;
            std::atomic_bool writeAcquired = false;
            std::jthread reader;
            std::jthread writer;
            {
                auto held = value.get_readonly();
                reader = std::jthread([&] { auto read = value.get_readonly(); readAcquired = true; });
                Check(WaitUntil([&] { return readAcquired.load(); }), "readers must coexist");
                writer = std::jthread([&] {
                    writeEntered = true;
                    auto write = value.get();
                    writeAcquired = true;
                    *write = "updated";
                });
                Check(WaitUntil([&] { return writeEntered.load(); }), "writer started");
                Check(!writeAcquired, "writer cannot acquire while reader holds the object");
            }
            writer.join();
            Check(writeAcquired && *value.get_readonly() == "updated", "writer proceeds after read release");
        } },
        { "concurrent writes retain every update", [] {
            ThreadSafeObjectWrapper<int> value(0);
            std::vector<std::jthread> workers;
            for (int i = 0; i < 4; ++i)
                workers.emplace_back([&] { for (int n = 0; n < 1000; ++n) ++*value.get(); });
            workers.clear();
            Check(*value.get_readonly() == 4000, "exclusive writes must not lose updates");
        } },
        { "wait event notification and reuse", [] {
            SingleWaitEvent event(3000);
            for (int i = 0; i < 5; ++i)
            {
                std::jthread notifier;
                auto lock = event.GetLock();
                notifier = std::jthread([&] { event.NotifyOne(); });
                Check(event.Wait(lock), "notification must wake waiter");
                Check(lock.owns_lock(), "wait returns with caller lock held");
            }
        } },
        { "wait event timeout then recovery", [] {
            SingleWaitEvent event(500);
            {
                auto lock = event.GetLock();
                Check(!event.Wait(lock), "unsignaled event times out");
                Check(lock.owns_lock(), "timeout retains caller lock");
            }
            std::jthread notifier;
            auto lock = event.GetLock();
            notifier = std::jthread([&] { event.NotifyOne(); });
            Check(event.Wait(lock), "timeout must not poison subsequent waits");
        } },
        { "wait event rejects unrelated mutex", [] {
            SingleWaitEvent event;
            std::mutex other;
            std::unique_lock lock(other);
            CheckThrows<std::logic_error>([&] { event.Wait(lock); }, "wrong mutex must throw");
        } },
        { "wait event rejects simultaneous second waiter", [] {
            SingleWaitEvent event(3000);
            std::atomic_bool entered = false;
            std::atomic_bool notified = false;
            std::jthread first([&] {
                auto lock = event.GetLock();
                entered = true;
                notified = event.Wait(lock);
            });
            Check(WaitUntil([&] { return entered.load(); }), "first waiter entered");
            {
                auto lock = event.GetLock();
                CheckThrows<std::logic_error>([&] { event.Wait(lock); }, "second waiter must throw");
            }
            event.NotifyOne();
            first.join();
            Check(notified, "rejected second waiter must leave first waiter usable");
        } },
    });
}
