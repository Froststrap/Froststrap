// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Buffers.Binary;
using System.Text;
using Froststrap.Integrations.AccountManager;
using Xunit;

namespace Froststrap.Tests
{
    public class AccountVaultTests
    {
        private static readonly byte[] Password = Encoding.UTF8.GetBytes("correct horse battery staple");
        private static readonly byte[] Plain = Encoding.UTF8.GetBytes("{\"accounts\":[]}");

        private static byte[] SealOk(byte[] plain)
        {
            Assert.Equal(AccountVaultResult.Ok, AccountVault.Seal(Password, plain, out byte[] data));
            return data;
        }

        private static AccountVaultResult Open(byte[] data) => AccountVault.Open(Password, data, out _);

        [Fact]
        public void RoundTrip()
        {
            byte[] data = SealOk(Plain);

            Assert.Equal(AccountVaultResult.Ok, AccountVault.Open(Password, data, out byte[] plain));
            Assert.Equal(Plain, plain);
        }

        [Fact]
        public void RoundTripEmpty()
        {
            byte[] data = SealOk([]);

            Assert.Equal(AccountVaultResult.Ok, AccountVault.Open(Password, data, out byte[] plain));
            Assert.Empty(plain);
        }

        [Fact]
        public void RoundTripMaxSize()
        {
            byte[] big = new byte[AccountVault.MaxPlainLength];
            Random.Shared.NextBytes(big);
            byte[] data = SealOk(big);

            Assert.Equal(AccountVaultResult.Ok, AccountVault.Open(Password, data, out byte[] plain));
            Assert.Equal(big, plain);
        }

        [Fact]
        public void SealRejectsBadInput()
        {
            Assert.Equal(AccountVaultResult.InvalidArgument, AccountVault.Seal([], Plain, out _));
            Assert.Equal(AccountVaultResult.InvalidArgument, AccountVault.Seal(Password, new byte[AccountVault.MaxPlainLength + 1], out _));
        }

        [Fact]
        public void SameInputGivesDifferentOutput()
        {
            byte[] a = SealOk(Plain);
            byte[] b = SealOk(Plain);

            Assert.NotEqual(a, b);
            // salt and nonce
            Assert.NotEqual(a[9..(AccountVault.HeaderLength)], b[9..(AccountVault.HeaderLength)]);
        }

        [Fact]
        public void WrongPassword()
        {
            byte[] data = SealOk(Plain);

            Assert.Equal(AccountVaultResult.WrongPassword, AccountVault.Open("wrong"u8, data, out byte[] plain));
            Assert.Empty(plain);
            Assert.Equal(AccountVaultResult.InvalidArgument, AccountVault.Open([], data, out _));
        }

        [Theory]
        [InlineData(9)]   // salt
        [InlineData(30)]  // nonce
        [InlineData(-17)] // last ciphertext byte
        [InlineData(-1)]  // tag
        public void TamperedDataFailsAuthentication(int offset)
        {
            byte[] data = SealOk(Plain);
            data[offset < 0 ? data.Length + offset : offset] ^= 0x01;

            Assert.Equal(AccountVaultResult.WrongPassword, Open(data));
        }

        [Fact]
        public void ChangedIterationsAreRejected()
        {
            byte[] data = SealOk(Plain);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(5), 1);

            Assert.Equal(AccountVaultResult.NotAVault, Open(data));
        }

        [Fact]
        public void NotAVault()
        {
            byte[] data = SealOk(Plain);

            Assert.Equal(AccountVaultResult.NotAVault, Open([]));
            Assert.Equal(AccountVaultResult.NotAVault, Open("FSA"u8.ToArray()));
            Assert.Equal(AccountVaultResult.NotAVault, Open(data[..(AccountVault.HeaderLength + 15)]));
            Assert.Equal(AccountVaultResult.NotAVault, Open(new byte[AccountVault.MaxDataLength + 1]));

            data[0] = (byte)'X';
            Assert.Equal(AccountVaultResult.NotAVault, Open(data));
        }

        [Fact]
        public void TruncatedDataIsRejected()
        {
            byte[] data = SealOk(Plain);

            Assert.NotEqual(AccountVaultResult.Ok, Open(data[..^1]));
        }

        [Fact]
        public void Versions()
        {
            byte[] data = SealOk(Plain);

            data[4] = 1;
            Assert.Equal(AccountVaultResult.OldFormat, Open(data));

            data[4] = 3;
            Assert.Equal(AccountVaultResult.UnsupportedVersion, Open(data));
        }
    }
}
