#pragma once

#include <memory>
#include <shared_mutex>

// need non-const type
template<typename T>
concept is_not_const_v = !std::is_const_v<T>;

// need non_ref type
template<typename T>
concept is_not_ref_v = !std::is_reference_v<T>;

// ������w������]�˾��A�P�ɦ� shared_ptr �S�ʥH�Φb���o����ɷ|�j��WŪ�g��
// ���i�a�J const type
template<typename T> requires is_not_const_v<T> && is_not_ref_v<T>
class ThreadSafeObjectWrapper
{
public:
    // ��w�X�ݾ��A�b��ͩR�g�����|�ϭ쪫��W��A���i�ƻs�B����
    // �a�J const type �|�WŪ��F non-const type �W�g��
    template<typename U, typename = typename std::enable_if_t<std::is_same_v<T, std::remove_const_t<U>>>>
    class LockAccessor
    {
    public:
        ~LockAccessor()
        {
            if constexpr (std::is_const_v<U>)
                m_mtx.unlock_shared();
            else
                m_mtx.unlock();
        }

        LockAccessor(const LockAccessor&) = delete;

        LockAccessor(LockAccessor&&) = delete;

        LockAccessor& operator=(const LockAccessor&) = delete;

        LockAccessor& operator=(LockAccessor&&) = delete;

        // ����
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
        // �̾ڱa�J�����O�P�_�O�_��const�A�Dconst�W�g��Aconst�WŪ�� (�غc�l��p���קK�Q�~�إ�)
        LockAccessor(std::shared_ptr<U> spObj, std::shared_mutex& mtx) :
            m_spObj(spObj), m_mtx(mtx)
        {
            if constexpr (std::is_const_v<U>)
                m_mtx.lock_shared();
            else
                m_mtx.lock();
        }

        std::shared_ptr<U> m_spObj;

        std::shared_mutex& m_mtx;

        friend class ThreadSafeObjectWrapper;
    };

public:
    // �i�ק諸��w�X�ݾ� (�g��)
    using get_type = LockAccessor<T>;

    // ���i�ק諸��w�X�ݾ� (Ū��)
    using const_get_type = LockAccessor<const T>;

    template <typename... Args>
    ThreadSafeObjectWrapper(Args&&... args) : m_spObj(std::make_shared<T>(std::forward<Args>(args)...)) {}

    ~ThreadSafeObjectWrapper() = default;

    ThreadSafeObjectWrapper(const ThreadSafeObjectWrapper&) = delete;

    ThreadSafeObjectWrapper(ThreadSafeObjectWrapper&&) = delete;

    ThreadSafeObjectWrapper& operator=(const ThreadSafeObjectWrapper&) = delete;

    ThreadSafeObjectWrapper& operator=(ThreadSafeObjectWrapper&&) = delete;

    // �W�g�����
    get_type get() const
    {
        return get_type(m_spObj, m_mtx);
    }

    // �WŪ����� (��Ū)
    const_get_type get_readonly() const
    {
        return const_get_type(m_spObj, m_mtx);
    }

private:
    // �קKobj�ͩR�g����wrapper�u�A�ϥ� shared_ptr �O�s
    std::shared_ptr<T> m_spObj;

    mutable std::shared_mutex m_mtx;
};
