#pragma once

#include <memory>
#include <shared_mutex>

// need non-const type
template<typename T>
concept is_not_const = !std::is_const_v<T>;

// need non_ref type
template<typename T>
concept is_not_ref = !std::is_reference_v<T>;

// need same type, const or non-const is not matter
template<typename T, typename U>
concept is_same_as = std::is_same_v<std::remove_const_t<T>, std::remove_const_t<U>>;

// copy assignable type
template<typename T>
concept copy_assignable = std::is_copy_assignable_v<T>;

// 執行緒安全物件包裝器，同時有 shared_ptr 特性以及在取得物件時會強制上讀寫鎖
// 不可帶入 const type
template<typename T> requires is_not_const<T> && is_not_ref<T>
class ThreadSafeObjectWrapper
{
public:
    // 鎖定訪問器，在其生命週期內會使原物件上鎖，不可複製、移動
    // 帶入 const type 會上讀鎖； non-const type 上寫鎖
    template<typename U> requires is_same_as<T, U>
    class LockAccessor
    {
    public:
        ~LockAccessor()
        {
            if constexpr (std::is_const_v<U>)
                m_spMtx->unlock_shared();
            else
                m_spMtx->unlock();
        }

        LockAccessor(const LockAccessor&) = delete;

        LockAccessor(LockAccessor&&) = delete;

        LockAccessor& operator=(const LockAccessor&) = delete;

        LockAccessor& operator=(LockAccessor&&) = delete;

        // 取值
        U& get()
        {
            return *m_spObj;
        }

        U& operator*()
        {
            return get();
        }

        U* operator->()
        {
            return m_spObj.get();
        }

    private:
        // 依據帶入的型別判斷是否為const，非const上寫鎖，const上讀鎖 (建構子放私有避免被誤建立)
        LockAccessor(std::shared_ptr<U> spObj, std::shared_ptr<std::shared_mutex> spMtx) :
            m_spObj(spObj), m_spMtx(spMtx)
        {
            if constexpr (std::is_const_v<U>)
                m_spMtx->lock_shared();
            else
                m_spMtx->lock();
        }

        std::shared_ptr<U> m_spObj;

        std::shared_ptr<std::shared_mutex> m_spMtx;

        friend class ThreadSafeObjectWrapper;
    };

public:
    // 可修改的鎖定訪問器 (寫鎖)
    using get_type = LockAccessor<T>;

    // 不可修改的鎖定訪問器 (讀鎖)
    using const_get_type = LockAccessor<const T>;

    template <typename... Args>
    ThreadSafeObjectWrapper(Args&&... args) :
        m_spObj(std::make_shared<T>(std::forward<Args>(args)...)),
        m_spMtx(std::make_shared<std::shared_mutex>())
    {}

    ThreadSafeObjectWrapper(const ThreadSafeObjectWrapper& other)
        requires std::copy_constructible<T> :
        m_spObj(std::make_shared<T>(*other.get_readonly())),
        m_spMtx(std::make_shared<std::shared_mutex>())
    {}

    ThreadSafeObjectWrapper& operator=(const ThreadSafeObjectWrapper& other)
        requires copy_assignable<T>
    {
        if (this != &other)
        {
            m_spObj = std::make_shared<T>(*other.get_readonly());
        }
        return *this;
    }

    ThreadSafeObjectWrapper(ThreadSafeObjectWrapper&&) = delete;

    ThreadSafeObjectWrapper& operator=(ThreadSafeObjectWrapper&&) = delete;

    ~ThreadSafeObjectWrapper() = default;

    // 上寫鎖取值
    get_type get() const
    {
        return get_type(m_spObj, m_spMtx);
    }

    // 上讀鎖取值 (唯讀)
    const_get_type get_readonly() const
    {
        return const_get_type(m_spObj, m_spMtx);
    }

private:
    // 避免obj生命週期比wrapper短，使用 shared_ptr 保存
    std::shared_ptr<T> m_spObj;

    std::shared_ptr<std::shared_mutex> m_spMtx;
};
