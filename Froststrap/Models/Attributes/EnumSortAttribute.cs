// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class EnumSortAttribute : Attribute
    {
        public int Order { get; set; }
    }
}