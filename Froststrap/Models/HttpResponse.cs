// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models
{
    internal class HttpResponse<T>
    {
        public T Data { get; set; } = default!;
        public List<string> Cookies { get; set; } = [];
    }
}