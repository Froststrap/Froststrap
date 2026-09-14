// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System;

namespace Froststrap.Exceptions
{
    internal class ChecksumFailedException : Exception
    {
        public ChecksumFailedException() : base() { }

        public ChecksumFailedException(string message) : base(message) { }

        public ChecksumFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}