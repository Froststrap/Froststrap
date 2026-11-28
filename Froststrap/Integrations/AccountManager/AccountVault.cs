// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Froststrap.Integrations.AccountManager
{
    internal enum AccountVaultResult
    {
        Ok,
        InvalidArgument,
        WrongPassword,
        NotAVault,
        OldFormat,
        UnsupportedVersion,
        Unavailable,
        Internal,
    }

    /// Password-protected blobs: PBKDF2-HMAC-SHA256 + AES-256-GCM.
    /// Layout: magic, version, iterations (u32 LE), salt, nonce, ciphertext, tag.
    /// Everything before the ciphertext is authenticated as associated data.
    /// Slow on purpose, so call it off the UI thread.
    internal static class AccountVault
    {
        private static ReadOnlySpan<byte> Magic => "FSAV"u8;
        // 1 was the Argon2id format from pre-release builds of this feature
        private const byte Version = 2;
        private const int SaltLength = 16;
        private const int NonceLength = 12;
        private const int TagLength = 16;
        private const int KeyLength = 32;
        public const int HeaderLength = 4 + 1 + 4 + SaltLength + NonceLength;

        // OWASP's recommendation for PBKDF2-HMAC-SHA256
        public const int Iterations = 600_000;

        // an export of every account is a few KB
        public const int MaxPlainLength = 1024 * 1024;
        public const int MaxDataLength = HeaderLength + MaxPlainLength + TagLength;

        public static AccountVaultResult Seal(ReadOnlySpan<byte> password, ReadOnlySpan<byte> plain, out byte[] sealedData)
        {
            sealedData = [];

            if (password.IsEmpty || plain.Length > MaxPlainLength)
                return AccountVaultResult.InvalidArgument;

            if (!AesGcm.IsSupported)
                return AccountVaultResult.Unavailable;

            byte[] output = new byte[HeaderLength + plain.Length + TagLength];
            var header = output.AsSpan(0, HeaderLength);

            Magic.CopyTo(header);
            header[4] = Version;
            BinaryPrimitives.WriteUInt32LittleEndian(header[5..], Iterations);
            var salt = header.Slice(9, SaltLength);
            var nonce = header.Slice(9 + SaltLength, NonceLength);
            RandomNumberGenerator.Fill(salt);
            RandomNumberGenerator.Fill(nonce);

            Span<byte> key = stackalloc byte[KeyLength];

            try
            {
                Rfc2898DeriveBytes.Pbkdf2(password, salt, key, Iterations, HashAlgorithmName.SHA256);

                using var aes = new AesGcm(key, TagLength);
                aes.Encrypt(nonce, plain, output.AsSpan(HeaderLength, plain.Length), output.AsSpan(HeaderLength + plain.Length), header);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

            sealedData = output;
            return AccountVaultResult.Ok;
        }

        /// `plain` is pinned; the caller owns it and should wipe it with CryptographicOperations.ZeroMemory.
        public static AccountVaultResult Open(ReadOnlySpan<byte> password, ReadOnlySpan<byte> data, out byte[] plain)
        {
            plain = [];

            if (password.IsEmpty)
                return AccountVaultResult.InvalidArgument;

            if (data.Length < 5 || !data[..4].SequenceEqual(Magic))
                return AccountVaultResult.NotAVault;

            if (data[4] > Version)
                return AccountVaultResult.UnsupportedVersion;

            if (data[4] < Version)
                return AccountVaultResult.OldFormat;

            if (data.Length < HeaderLength + TagLength || data.Length > MaxDataLength)
                return AccountVaultResult.NotAVault;

            // the header is read before it can be authenticated, so only accept what Seal writes;
            // anything else would let a crafted file make us spin on the key derivation
            if (BinaryPrimitives.ReadUInt32LittleEndian(data[5..]) != Iterations)
                return AccountVaultResult.NotAVault;

            if (!AesGcm.IsSupported)
                return AccountVaultResult.Unavailable;

            var header = data[..HeaderLength];
            var salt = header.Slice(9, SaltLength);
            var nonce = header.Slice(9 + SaltLength, NonceLength);
            var cipher = data[HeaderLength..^TagLength];
            var tag = data[^TagLength..];

            // pinned so the GC can't leave moved copies of the plaintext behind before the caller wipes it
            byte[] output = GC.AllocateArray<byte>(cipher.Length, pinned: true);
            Span<byte> key = stackalloc byte[KeyLength];
            bool opened = false;

            try
            {
                Rfc2898DeriveBytes.Pbkdf2(password, salt, key, Iterations, HashAlgorithmName.SHA256);

                using var aes = new AesGcm(key, TagLength);
                aes.Decrypt(nonce, cipher, tag, output, header);
                opened = true;
            }
            catch (AuthenticationTagMismatchException)
            {
                return AccountVaultResult.WrongPassword;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);

                // also covers unexpected exceptions, not just a wrong password
                if (!opened)
                    CryptographicOperations.ZeroMemory(output);
            }

            plain = output;
            return AccountVaultResult.Ok;
        }
    }
}
