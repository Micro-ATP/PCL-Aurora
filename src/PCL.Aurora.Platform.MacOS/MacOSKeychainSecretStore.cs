using System.Runtime.InteropServices;
using System.Text;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

/// <summary>
/// 使用 macOS 登录钥匙串保存刷新令牌。
/// 直接调用 Keychain Services，避免将秘密作为子进程参数、环境变量或普通文件内容传递。
/// </summary>
public sealed class MacOSKeychainSecretStore : ISecureSecretStore
{
    private const int ErrSecSuccess = 0;
    private const int ErrSecItemNotFound = -25300;

    public Task<string?> GetAsync(string service, string account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateKey(service, account);
        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var accountBytes = Encoding.UTF8.GetBytes(account);
        var result = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            checked((uint)serviceBytes.Length),
            serviceBytes,
            checked((uint)accountBytes.Length),
            accountBytes,
            out var passwordLength,
            out var passwordData,
            out var item);
        try
        {
            if (result == ErrSecItemNotFound)
            {
                return Task.FromResult<string?>(null);
            }

            ThrowIfError(result, "读取钥匙串秘密失败。");
            if (passwordData == IntPtr.Zero || passwordLength == 0)
            {
                return Task.FromResult<string?>(string.Empty);
            }

            var bytes = new byte[passwordLength];
            Marshal.Copy(passwordData, bytes, 0, checked((int)passwordLength));
            return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes));
        }
        finally
        {
            if (passwordData != IntPtr.Zero)
            {
                SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            }

            Release(item);
        }
    }

    public Task SetAsync(string service, string account, string secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateKey(service, account);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("秘密不能为空。", nameof(secret));
        }

        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var accountBytes = Encoding.UTF8.GetBytes(account);
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var result = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            checked((uint)serviceBytes.Length),
            serviceBytes,
            checked((uint)accountBytes.Length),
            accountBytes,
            out _,
            out var existingPasswordData,
            out var item);
        try
        {
            if (result == ErrSecItemNotFound)
            {
                result = SecKeychainAddGenericPassword(
                    IntPtr.Zero,
                    checked((uint)serviceBytes.Length),
                    serviceBytes,
                    checked((uint)accountBytes.Length),
                    accountBytes,
                    checked((uint)secretBytes.Length),
                    secretBytes,
                    out var newItem);
                Release(newItem);
                ThrowIfError(result, "写入钥匙串秘密失败。");
                return Task.CompletedTask;
            }

            ThrowIfError(result, "读取钥匙串秘密失败。");
            result = SecKeychainItemModifyAttributesAndData(
                item,
                IntPtr.Zero,
                checked((uint)secretBytes.Length),
                secretBytes);
            ThrowIfError(result, "更新钥匙串秘密失败。");
            return Task.CompletedTask;
        }
        finally
        {
            if (existingPasswordData != IntPtr.Zero)
            {
                SecKeychainItemFreeContent(IntPtr.Zero, existingPasswordData);
            }

            Release(item);
        }
    }

    public Task DeleteAsync(string service, string account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateKey(service, account);
        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var accountBytes = Encoding.UTF8.GetBytes(account);
        var result = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            checked((uint)serviceBytes.Length),
            serviceBytes,
            checked((uint)accountBytes.Length),
            accountBytes,
            out _,
            out var passwordData,
            out var item);
        try
        {
            if (result == ErrSecItemNotFound)
            {
                return Task.CompletedTask;
            }

            ThrowIfError(result, "读取钥匙串秘密失败。");
            ThrowIfError(SecKeychainItemDelete(item), "删除钥匙串秘密失败。");
            return Task.CompletedTask;
        }
        finally
        {
            if (passwordData != IntPtr.Zero)
            {
                SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            }

            Release(item);
        }
    }

    private static void ValidateKey(string service, string account)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
    }

    private static void ThrowIfError(int status, string message)
    {
        if (status != ErrSecSuccess)
        {
            throw new InvalidOperationException($"{message} Keychain status: {status}。");
        }
    }

    private static void Release(IntPtr item)
    {
        if (item != IntPtr.Zero)
        {
            CFRelease(item);
        }
    }

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychainOrArray,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        out uint passwordLength,
        out IntPtr passwordData,
        out IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainAddGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        uint passwordLength,
        byte[] passwordData,
        out IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemModifyAttributesAndData(
        IntPtr itemRef,
        IntPtr attrList,
        uint length,
        byte[] data);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemDelete(IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);
}
