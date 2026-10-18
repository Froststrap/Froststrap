// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Froststrap.Utility
{
    internal static class ReflectionHelper
    {
        [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "View and page types are resolved by name from the rooted Froststrap assembly, so the target types are always preserved.")]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "View and page types are resolved by name from the rooted Froststrap assembly, so the target types are always preserved.")]
        public static Type? FindType(string typeName)
            => Type.GetType(typeName) ?? Assembly.GetExecutingAssembly().GetType(typeName);

        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "View and page types are preserved by rooting the Froststrap assembly and always expose a public parameterless constructor.")]
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "View and page types are preserved by rooting the Froststrap assembly and always expose a public parameterless constructor.")]
        public static object? CreateInstance(Type type) => Activator.CreateInstance(type);
    }
}
