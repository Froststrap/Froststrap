// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System;

namespace Froststrap.Exceptions
{
    internal class InvalidHTTPResponseException : Exception
    {
        public InvalidHTTPResponseException() : base() { }

        public InvalidHTTPResponseException(string message) : base(message) { }

        public InvalidHTTPResponseException(string message, Exception innerException) : base(message, innerException) { }
    }
}